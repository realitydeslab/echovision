// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-License-Identifier: MIT
#if UNITY_IOS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;

namespace HoloKit.iOS
{
    [DisallowMultipleComponent]
    public sealed class HoloKitVideoRecorder : MonoBehaviour
    {
        public AudioSource _microphoneAudioSource;
        [SerializeField] bool _recordIn4K = true;
        public bool RecordIn4K { get => _recordIn4K; set => _recordIn4K = value; }
        ARBackgroundVideoEnhancementManager _enhancement;

        public bool IsRecording { get; private set; }
        public string LastRecordingPath { get; private set; }
        public bool IsFinishing => HoloKitVideoRecorder_IsFinishing() != 0;
        public bool UsesGpuPath => _useGpuPath;
        public int SubmittedFrames { get; private set; }
        public int DroppedFrames { get; private set; }

        Camera _camera;
        ARCameraBackground _background;
        HoloKitCameraManager _cameraManager;
        RenderTexture _cameraRt, _frameRt;
        Coroutine _capture;
        readonly System.Diagnostics.Stopwatch _clock = new();
        readonly object _audioLock = new();
        bool _acceptAudio;
        int _audioChannels, _sampleRate;
        long _audioFrames;
        double _audioStart;
        volatile bool _audioFailed;
        double _lastFrameTime;
        bool _useGpuPath;
        const int SlotCount = 3;
        Texture2D[] _slots;
        readonly GraphicsFence[] _fences = new GraphicsFence[SlotCount];
        readonly double[] _timestamps = new double[SlotCount];
        readonly Queue<int> _pending = new();
        int _cursor;

        void Start()
        {
            _camera = GetComponent<Camera>();
            _background = GetComponent<ARCameraBackground>();
            _cameraManager = GetComponentInParent<HoloKitCameraManager>();
            _cameraManager.OnScreenRenderModeChanged += ApplyRenderMode;
        }

        public void ToggleRecording()
        {
            if (IsRecording) EndRecording();
            else StartRecording();
        }

        public void StartRecording()
        {
            if (IsRecording || IsFinishing) return;
            if (_camera == null || _microphoneAudioSource == null || _microphoneAudioSource.clip == null)
            {
                Debug.LogWarning("[Recorder] Camera and microphone clip must be ready before recording.");
                return;
            }

            var speakerMode = AudioSettings.speakerMode;
            _audioChannels = speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            if (speakerMode != AudioSpeakerMode.Mono && speakerMode != AudioSpeakerMode.Stereo)
            {
                Debug.LogError($"[Recorder] Unsupported audio layout: {speakerMode}");
                return;
            }
            _sampleRate = AudioSettings.outputSampleRate;
            int width = Screen.width & ~15, height = Screen.height & ~15;
            LastRecordingPath = Path.Combine(Application.temporaryCachePath,
                $"Record_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.mp4");
            if (HoloKitVideoRecorder_StartRecording(LastRecordingPath, width, height, _sampleRate, _audioChannels) != 0)
            {
                Debug.LogError("[Recorder] Native writer could not start.");
                return;
            }

            _cameraRt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf);
            bool bgra = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.BGRA32);
            _frameRt = new RenderTexture(width, height, 0, bgra ? RenderTextureFormat.BGRA32 : RenderTextureFormat.ARGB32);
            _cameraRt.Create();
            _frameRt.Create();
            _pending.Clear();
            _cursor = 0;
            _useGpuPath = bgra && SystemInfo.supportsGraphicsFence &&
                (SystemInfo.copyTextureSupport & CopyTextureSupport.RTToTexture) != 0 &&
                HoloKitVideoRecorder_AllocateGPUSlots(SlotCount, width, height) == SlotCount;
            if (_useGpuPath) _slots = new Texture2D[SlotCount];
            else HoloKitVideoRecorder_ReleaseGPUSlots();
            SubmittedFrames = DroppedFrames = 0;
            _lastFrameTime = -1;
            _audioFrames = 0;
            _audioFailed = false;
            _microphoneAudioSource.Play();
            _clock.Restart();
            IsRecording = true;
            _enhancement = FindFirstObjectByType<ARBackgroundVideoEnhancementManager>();
            if (_enhancement != null) _enhancement.RecordingOverride = _recordIn4K;
            lock (_audioLock) _acceptAudio = true;
            ApplyRenderMode(_cameraManager.ScreenRenderMode);
            _capture = StartCoroutine(CaptureFrames());
            Debug.Log($"[Recorder] START {width}x{height} GPU={_useGpuPath} audio={_sampleRate}/{_audioChannels} path={LastRecordingPath}");
        }

        public void EndRecording()
        {
            if (!IsRecording) return;
            if (_capture != null) StopCoroutine(_capture);
            _capture = null;
            lock (_audioLock) _acceptAudio = false;
            _microphoneAudioSource.Stop();

            // Read one pixel from each destination at stop to wait for that
            // exact copy. Fence.passed can remain cached until the next frame.
            while (_pending.Count > 0)
            {
                int index = _pending.Dequeue();
                var request = AsyncGPUReadback.Request(_slots[index], 0, 0, 1, 0, 1, 0, 1);
                request.WaitForCompletion();
                if (request.hasError)
                {
                    DroppedFrames++;
                    Debug.LogError("[Recorder] GPU copy could not be finalized.");
                }
                else SubmitSlot(index);
            }
            AsyncGPUReadback.WaitAllRequests();
            IsRecording = false;
            if (_enhancement != null) _enhancement.RecordingOverride = false;
            HoloKitVideoRecorder_EndRecording();
            if (_slots != null)
                foreach (var slot in _slots) if (slot != null) Destroy(slot);
            _slots = null;
            HoloKitVideoRecorder_ReleaseGPUSlots();
            _pending.Clear();
            Destroy(_cameraRt);
            Destroy(_frameRt);
            _cameraRt = _frameRt = null;
            ApplyRenderMode(_cameraManager.ScreenRenderMode);
            Debug.Log($"[Recorder] STOP submitted={SubmittedFrames} dropped={DroppedFrames} path={LastRecordingPath}");
        }

        IEnumerator CaptureFrames()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (IsRecording)
            {
                yield return endOfFrame;
                if (IsRecording) CaptureFrame();
            }
        }

        void CaptureFrame()
        {
            DrainSlots();
            double timestamp = _clock.Elapsed.TotalSeconds;
            if (timestamp - _lastFrameTime < 1.0 / 60.0) return;
            _lastFrameTime = timestamp;
            if (_useGpuPath && _pending.Count == SlotCount) { DroppedFrames++; return; }

            // Preserve the live viewport aspect when the phone rotates during
            // a movie: fit it inside the movie's fixed dimensions, without stretch.
            var target = _camera.targetTexture;
            var rect = _camera.rect;
            try
            {
                var previousActive = RenderTexture.active;
                RenderTexture.active = _cameraRt;
                GL.Clear(true, true, Color.black);
                RenderTexture.active = previousActive;
                _camera.targetTexture = _cameraRt;
                _camera.rect = FitViewport((float)Screen.width / Screen.height,
                    (float)_cameraRt.width / _cameraRt.height);
                _camera.Render();
            }
            finally { _camera.targetTexture = target; _camera.rect = rect; }
            Graphics.Blit(_cameraRt, _frameRt, new Vector2(1, -1), new Vector2(0, 1));
            if (!_useGpuPath)
            {
                AsyncGPUReadback.Request(_frameRt, 0, TextureFormat.RGBA32,
                    request => SubmitReadback(request, timestamp));
                return;
            }

            int index = _cursor;
            IntPtr texture = HoloKitVideoRecorder_AcquireGPUSlot(index);
            if (texture == IntPtr.Zero) { DroppedFrames++; return; }
            if (_slots[index] == null)
                _slots[index] = Texture2D.CreateExternalTexture(_frameRt.width, _frameRt.height,
                    TextureFormat.BGRA32, false, false, texture);
            else _slots[index].UpdateExternalTexture(texture);
            Graphics.CopyTexture(_frameRt, _slots[index]);
            _fences[index] = Graphics.CreateGraphicsFence(GraphicsFenceType.CPUSynchronisation,
                SynchronisationStageFlags.AllGPUOperations);
            _timestamps[index] = timestamp;
            _pending.Enqueue(index);
            _cursor = (index + 1) % SlotCount;
        }

        public static Rect FitViewport(float sourceAspect, float outputAspect)
        {
            if (sourceAspect > outputAspect)
            {
                float height = outputAspect / sourceAspect;
                return new Rect(0, (1 - height) / 2, 1, height);
            }
            float width = sourceAspect / outputAspect;
            return new Rect((1 - width) / 2, 0, width, 1);
        }

        void DrainSlots()
        {
            while (_pending.Count > 0 && _fences[_pending.Peek()].passed)
            {
                int index = _pending.Dequeue();
                SubmitSlot(index);
            }
        }

        void SubmitSlot(int index)
        {
            if (HoloKitVideoRecorder_SubmitGPUSlot(index, _timestamps[index]) == 0) SubmittedFrames++;
            else DroppedFrames++;
        }

        unsafe void SubmitReadback(AsyncGPUReadbackRequest request, double timestamp)
        {
            if (!IsRecording) return;
            if (request.hasError) { DroppedFrames++; return; }
            var data = request.GetData<byte>();
            int result = HoloKitVideoRecorder_AppendVideoFrame(
                (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(data), data.Length, timestamp);
            if (result == 0) SubmittedFrames++; else DroppedFrames++;
        }

        unsafe void OnAudioFilterRead(float[] data, int channels)
        {
            lock (_audioLock)
            {
                if (_acceptAudio)
                {
                    if (channels != _audioChannels || data.Length % channels != 0) _audioFailed = true;
                    else
                    {
                        if (_audioFrames == 0) _audioStart = _clock.Elapsed.TotalSeconds;
                        double timestamp = _audioStart + (double)_audioFrames / _sampleRate;
                        fixed (float* samples = data)
                        {
                            int result = HoloKitVideoRecorder_AppendAudioFrame((IntPtr)samples,
                                data.Length * sizeof(float), timestamp);
                            if (result == -1) _audioFailed = true;
                        }
                        _audioFrames += data.Length / channels;
                    }
                }
            }
            Array.Clear(data, 0, data.Length); // prevent microphone-to-speaker feedback
        }

        void Update()
        {
            if (_audioFailed && IsRecording)
            {
                Debug.LogError("[Recorder] Audio append failed or output layout changed; stopping recording.");
                EndRecording();
            }
        }

        void ApplyRenderMode(ScreenRenderMode mode)
        {
            bool renderMono = mode == ScreenRenderMode.Mono || IsRecording;
            _background.enabled = renderMono;
            _camera.enabled = renderMono;
        }

        void OnApplicationPause(bool paused) { if (paused) EndRecording(); }
        void OnDisable() { EndRecording(); }
        void OnDestroy()
        {
            EndRecording();
            if (_cameraManager != null) _cameraManager.OnScreenRenderModeChanged -= ApplyRenderMode;
        }

#if UNITY_EDITOR
        const string DllName = "UnityHoloKit";
#else
        const string DllName = "__Internal";
#endif
        [DllImport(DllName)] public static extern int HoloKitVideoRecorder_StartRecording(string path, int width, int height, float sampleRate, int channels);
        [DllImport(DllName)] public static extern int HoloKitVideoRecorder_AppendVideoFrame(IntPtr data, int size, double time);
        [DllImport(DllName)] public static extern int HoloKitVideoRecorder_AppendAudioFrame(IntPtr data, int size, double time);
        [DllImport(DllName)] public static extern void HoloKitVideoRecorder_EndRecording();
        [DllImport(DllName)] static extern int HoloKitVideoRecorder_IsFinishing();
        [DllImport(DllName)] static extern int HoloKitVideoRecorder_AllocateGPUSlots(int count, int width, int height);
        [DllImport(DllName)] static extern IntPtr HoloKitVideoRecorder_AcquireGPUSlot(int index);
        [DllImport(DllName)] static extern int HoloKitVideoRecorder_SubmitGPUSlot(int index, double time);
        [DllImport(DllName)] static extern void HoloKitVideoRecorder_ReleaseGPUSlots();
    }
}
#endif
