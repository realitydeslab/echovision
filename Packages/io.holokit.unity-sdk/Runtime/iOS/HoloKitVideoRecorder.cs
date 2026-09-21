// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <botao.a.hu@gmail.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections.LowLevel.Unsafe;
using System;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Collections;
using Unity.Collections;

namespace HoloKit.iOS
{

    public interface IClock
    {

        /// <summary>
        /// Current timestamp in seconds;
        /// </summary>
        double timestamp { get; }
    }

    static class PathUtil
    {
        public static string TemporaryDirectoryPath
            => Application.platform == RuntimePlatform.IPhonePlayer
                ? Application.temporaryCachePath : ".";

        public static string GetTimestampedFilename()
            => $"Record_{DateTime.Now:MMdd_HHm_ss}.mp4";

        public static string GetTemporaryFilePath()
            => TemporaryDirectoryPath + "/" + GetTimestampedFilename();
    }

    sealed class TimeQueue
    {
        Queue<double> _queue = new Queue<double>();
        double _start;
        double _last;

        public void Clear()
        {
            _queue.Clear();
            _start = 0;
        }

        public double StartTime => _start;

        public double Dequeue()
            => _queue.Dequeue();

        public bool TryEnqueueNow(double timestamp)
        {
            if (_start == 0)
            {
                _queue.Enqueue(timestamp);
                _start = timestamp;
                _last = 0;
                return true;
            }
            else
            {
                var time = timestamp;

                // Reject it if it falls into the same frame.
                if ((int)(time * 60) == (int)(_last * 60)) return false;

                _queue.Enqueue(time);
                _last = time;
                return true;
            }
        }
    }

    /// <summary>
    /// Realtime clock for generating timestamps
    /// </summary>
    public sealed class RealtimeClock : IClock
    {

#region --Client API--
        /// <summary>
        /// Current timestamp in seconds.
        /// The very first value reported by this property will always be zero.
        /// </summary>
        public double timestamp
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                var time = stopwatch.Elapsed.TotalSeconds;
                if (!stopwatch.IsRunning)
                    stopwatch.Start();
                return time;
            }
        }

        /// <summary>
        /// Is the clock paused?
        /// </summary>
        public bool paused
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get => !stopwatch.IsRunning;
            [MethodImpl(MethodImplOptions.Synchronized)]
            set => (value ? (Action)stopwatch.Stop : stopwatch.Start)();
        }

        /// <summary>
        /// Create a realtime clock.
        /// </summary>
        public RealtimeClock() => this.stopwatch = new System.Diagnostics.Stopwatch();
#endregion


#region --Operations--
        private readonly System.Diagnostics.Stopwatch stopwatch;
#endregion
    }

    internal sealed class SharedSignal
    {

#region --Client API--
        /// <summary>
        /// Whether the shared signal has been triggered.
        /// </summary>
        public bool signaled
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get;
            private set;
        }

        /// <summary>
        /// Event raised when the shared signal is triggered.
        /// </summary>
        public event Action OnSignal;

        /// <summary>
        /// Create a shared signal.
        /// </summary>
        /// <param name="count">Number of unique signals required to trigger the shared signal.</param>
        public SharedSignal(int count)
        {
            this.record = new HashSet<object>();
            this.count = count;
        }

        /// <summary>
        /// Send a signal.
        /// </summary>
        /// <param name-"key">Key to identify signal.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Signal(object key)
        {
            if (signaled)
                return;
            record.Add(key);
            if (record.Count == count)
            {
                OnSignal?.Invoke();
                signaled = true;
            }
        }
#endregion


#region --Operations--
        private readonly int count;
        private readonly HashSet<object> record;
#endregion
    }

    [DisallowMultipleComponent]
    public sealed class HoloKitVideoRecorder : MonoBehaviour
    {
        private Camera _recordingCamera = null;
        private AudioListener _audioListener = null;

        public AudioSource _microphoneAudioSource = null;

        [SerializeField]
        private bool _recordMicrophone = false;

        [Tooltip("Turn ARKit's 4K/HDR background video on for the duration of the recording. " +
                 "Leaving it on all the time costs battery and heat, and only the recording " +
                 "benefits from the extra detail, so it is switched on at StartRecording and " +
                 "restored at EndRecording.")]
        [SerializeField]
        private bool _recordIn4K = true;

        /// <summary>
        /// Turn ARKit's 4K/HDR background video on while recording. Without it a
        /// high-resolution recording is an upscale of the standard background
        /// rather than real detail.
        /// </summary>
        public bool RecordIn4K
        {
            get => _recordIn4K;
            set => _recordIn4K = value;
        }

        private ARBackgroundVideoEnhancementManager _videoEnhancement;
        private bool _videoEnhancementWasEnabled;
        private bool _videoEnhancementOverridden;

        //public MicInput _micInput;
        //public TMPro.TextMeshProUGUI _buttonText;

        private RealtimeClock _clock = null;
        private RenderTexture _cameraRt;
        private RenderTexture _frameRt;
        private TimeQueue _timeQueue = new TimeQueue();
        private bool _useGpuPath = false;

        // Slot-based GPU path. Unity copies each frame into one of these
        // externally-created textures, which are backed by IOSurface pixel
        // buffers the writer can consume directly -- no CPU readback.
        //
        // A slot is submitted only after k_SlotLatency further frames, giving the
        // GPU time to finish the copy. Submitting immediately would race the
        // copy and write a partially-updated frame.
        const int k_SlotCount = 3;
        const int k_SlotLatency = 2;
        Texture2D[] _gpuSlots;
        double[] _gpuSlotTimes;
        bool[] _gpuSlotPending;
        int _gpuSlotCursor;
        private HoloKitCameraManager _holokitCameraManager;

        // private readonly RingBuffer<float> __deviceRingBuffer;
        // private readonly RingBuffer<float> __unityRingBuffer;
        private readonly float[] _deviceSampleBuffer;
        private readonly float[] _unitySampleBuffer;
        private readonly float[] _mixedBuffer;
        private readonly SharedSignal _signal;
        private readonly object __deviceFence;
        private readonly object __unityFence;
        private const int RingBufferSize = 16384;
        private const int MixBufferSize = 1024;

        public bool IsRecording
        {
            get;
            private set;
        }

        void Start()
        {
            _recordingCamera = GetComponent<Camera>();
            _audioListener = GetComponent<AudioListener>();

            _holokitCameraManager = GetComponentInParent<HoloKitCameraManager>();
            _holokitCameraManager.OnScreenRenderModeChanged += OnHoloKitRenderModeChanged;

            if (_recordMicrophone && Microphone.devices != null)
            {
                if (Application.HasUserAuthorization(UserAuthorization.Microphone))
                {
                    Debug.Log("Microphone access granted");
                }
                else
                {
                    Debug.Log("Microphone access denied");
                }

                //if (_microphoneAudioSource == null)
                //{
                //    _microphoneAudioSource = gameObject.AddComponent<AudioSource>();
                //}
                //_microphoneAudioSource = GetComponent<AudioSource>();

                //_microphoneAudioSource.clip = Microphone.Start(null, true, 1, 44100);
                //_microphoneAudioSource.loop = true;
                
                //while (!(Microphone.GetPosition(null) > 0))
                //{
                //}
                //_microphoneAudioSource.Play();
            }
        }

        public void StartRecording()
        {
            // StartRecording is public, so a caller can reach it without going
            // through ToggleRecording. Starting twice would orphan both
            // full-screen RenderTextures and leave a second CommitFrames
            // coroutine running against the same writer.
            if (IsRecording)
            {
                Debug.LogWarning("[HoloKitVideoRecorder] Already recording; ignoring StartRecording.");
                return;
            }

            //_microphoneAudioSource.loop = true;
            //while (!(Microphone.GetPosition(null) > 0))
            //{
            //}
            _microphoneAudioSource.Play();

            _clock = new RealtimeClock();
            // var sampleRate = _audioDevice != null ? _audioDevice.sampleRate : 24000;
            // var channelCount = _audioDevice != null ? _audioDevice.channelCount : 2;

            BeginVideoEnhancementOverride();

            var path = PathUtil.GetTemporaryFilePath();
            var sampleRate = AudioSettings.outputSampleRate;
            var channelCount = 2;
            // Round down to multiples of 16 for HEVC hardware encoder compatibility
            var width = Screen.width & ~15;
            var height = Screen.height & ~15;

            // ARGBHalf will make sure the camera renders post-processing.
            var _descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, 32);
            _cameraRt = new RenderTexture(_descriptor);
            // The GPU slot path copies raw bytes straight into a 32BGRA CVPixelBuffer,
            // so the source must already be in BGRA order. ARGB32 is RGBA-ordered on
            // Metal, which showed up on device as a red/blue swap in the recording.
            // The CPU fallback is unaffected: it asks AsyncGPUReadback for RGBA32
            // explicitly and native permutes to BGRA with vImage.
            bool bgraSupported = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.BGRA32);
            _frameRt = new RenderTexture(width, height, 0,
                bgraSupported ? RenderTextureFormat.BGRA32 : RenderTextureFormat.ARGB32);
            Debug.Log($"Camera Render Texture: {width}x{height}");
            int status = HoloKitVideoRecorder_StartRecording(path, width, height, sampleRate, channelCount);
            _timeQueue.Clear();

            HoloKitVideoRecorder_ResetGPUCounters();
            _useGpuPath = bgraSupported && TryAllocateGpuSlots(width, height);

            Debug.Log($"[HoloKitVideoRecorder] GPU zero-copy path: {(_useGpuPath ? "enabled" : "unavailable, falling back to CPU readback")}");

            if (status != 0)
            {
                // Everything allocated above has to go back, or a failed start
                // leaks two full-screen RenderTextures and the GPU slots, and
                // leaves 4K enhancement switched on with nothing to switch it off.
                Debug.LogError("Cannot Start Video");
                ReleaseGpuSlots();
                EndVideoEnhancementOverride();
                ReleaseRenderTextures();
                return;
            }

            IsRecording = true;

            OnHoloKitRenderModeChanged(_holokitCameraManager.ScreenRenderMode);

            StartCoroutine(CommitFrames());
        }

        public void EndRecording()
        {
            if (!IsRecording)
            {
                return;
            }

            if (_recordMicrophone && _microphoneAudioSource != null)
            {
                _microphoneAudioSource.Stop();
                //Microphone.End(null);
            }

            AsyncGPUReadback.WaitAllRequests();


            // The slot path submits each frame k_SlotLatency frames late, so the
            // last few are still pending here. Flush them before the writer
            // finishes, or the tail of every recording is silently lost.
            FlushGpuSlots();

            // Read the counters only after the flush: the tail slots are
            // submitted there, so logging earlier always under-reported by up
            // to k_SlotLatency frames.
            if (_useGpuPath)
            {
                Debug.Log($"[HoloKitVideoRecorder] GPU frames submitted={HoloKitVideoRecorder_GetSubmittedFrameCount()} " +
                          $"dropped={HoloKitVideoRecorder_GetDroppedFrameCount()}");
            }

            HoloKitVideoRecorder_EndRecording();
            ReleaseGpuSlots();
            EndVideoEnhancementOverride();
            IsRecording = false;

            OnHoloKitRenderModeChanged(_holokitCameraManager.ScreenRenderMode);

            ReleaseRenderTextures();
        }

        void ReleaseRenderTextures()
        {
            if (_frameRt != null)
            {
                Destroy(_frameRt);
                _frameRt = null;
            }
            if (_cameraRt != null)
            {
                Destroy(_cameraRt);
                _cameraRt = null;
            }
        }

        unsafe void OnSourceReadback(AsyncGPUReadbackRequest request)
        {
            if (!IsRecording) return;
            var data = request.GetData<byte>(0);
            var ptr = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(data);
            var timestamp = _timeQueue.Dequeue();
            int status = HoloKitVideoRecorder_AppendVideoFrame(ptr, data.Length, timestamp);

            if (status != 0)
            {
                Debug.LogError($"Cannot Append Video Frame at time {timestamp}");
            }
        }

        private IEnumerator CommitFrames()
        {
            var yielder = new WaitForEndOfFrame();
            while (IsRecording)
            {
                // Check frame index
                yield return yielder;
                // Render cameras
                CommitFrame(_recordingCamera);
                // Commit
            }
        }

        void Update()
        {

        }

        void OnDestroy()
        {
            if (IsRecording)
            {
                EndRecording();
            }

            if (_holokitCameraManager)
                _holokitCameraManager.OnScreenRenderModeChanged -= OnHoloKitRenderModeChanged;
        }

        void OnHoloKitRenderModeChanged(ScreenRenderMode mode)
        {
            if (IsRecording)
            {
                if (mode == ScreenRenderMode.Stereo)
                {
                    GetComponent<ARCameraBackground>().enabled = true;
                    _recordingCamera.enabled = true;
                }
            }
            else
            {
                if (mode == ScreenRenderMode.Stereo)
                {
                    GetComponent<ARCameraBackground>().enabled = false;
                    _recordingCamera.enabled = false;
                }
            }
        }

        unsafe void OnAudioFilterRead(float[] data, int channels)
        {
            if (IsRecording)
                CommitSamples(data, channels);

            // Always silence the listener output, even when not recording.
            // The microphone is routed into the global mix via audiosourceForRecording
            // (volume 0.5 -> Master group at 0 dB), so leaving it audible feeds the mic
            // back into itself through the speaker and self-oscillates. Recording is
            // unaffected: CommitSamples above already captured the mix.
            System.Array.Clear(data, 0, data.Length);
        }

        // The sample buffer MUST be a linear PCM buffer interleaved by channel.
        unsafe void CommitSamples(float[] data, int channels)
        {
            var nativeArray = new NativeArray<float>(data, Allocator.Temp);
            var ptr = (IntPtr)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(nativeArray);
            int length = nativeArray.Length * sizeof(float);
            double timestamp = _clock.timestamp;

            HoloKitVideoRecorder_AppendAudioFrame(ptr, length, timestamp);
            nativeArray.Dispose();
        }

        /// <summary>
        /// Switches ARKit's 4K/HDR background video on for the recording, keeping
        /// the previous setting so it can be put back afterwards.
        /// </summary>
        void BeginVideoEnhancementOverride()
        {
            _videoEnhancementOverridden = false;

            if (!_recordIn4K)
            {
                return;
            }

            // The enhancement manager drives the native provider through
            // HoloKitARKitManager, which is a plain scene singleton: without it
            // the setter dereferences null.
            if (HoloKitARKitManager.Instance == null)
            {
                Debug.LogWarning("[HoloKitVideoRecorder] RecordIn4K needs a HoloKitARKitManager in the scene; recording at the standard background resolution.");
                return;
            }

            if (_videoEnhancement == null)
            {
                _videoEnhancement = FindFirstObjectByType<ARBackgroundVideoEnhancementManager>();
            }

            if (_videoEnhancement == null)
            {
                Debug.LogWarning("[HoloKitVideoRecorder] RecordIn4K needs an ARBackgroundVideoEnhancementManager in the scene; recording at the standard background resolution.");
                return;
            }

            _videoEnhancementWasEnabled = _videoEnhancement.IsEnabled;
            _videoEnhancementOverridden = true;
            _videoEnhancement.IsEnabled = true;
            Debug.Log($"[HoloKitVideoRecorder] 4K background enabled for recording (was {_videoEnhancementWasEnabled}).");
        }

        void EndVideoEnhancementOverride()
        {
            if (!_videoEnhancementOverridden || _videoEnhancement == null)
            {
                return;
            }

            _videoEnhancementOverridden = false;
            if (HoloKitARKitManager.Instance != null)
            {
                _videoEnhancement.IsEnabled = _videoEnhancementWasEnabled;
            }
        }

        /// <summary>
        /// Allocates native IOSurface-backed slots and wraps each as an external
        /// Texture2D that Graphics.CopyTexture can write into.
        /// </summary>
        bool TryAllocateGpuSlots(int width, int height)
        {
            if ((SystemInfo.copyTextureSupport & UnityEngine.Rendering.CopyTextureSupport.RTToTexture) == 0)
            {
                Debug.LogWarning("[HoloKitVideoRecorder] CopyTexture RT->Texture unsupported; using CPU readback.");
                return false;
            }

            int created = HoloKitVideoRecorder_AllocateGPUSlots(k_SlotCount, width, height);
            if (created <= k_SlotLatency)
            {
                Debug.LogWarning($"[HoloKitVideoRecorder] Only {created} GPU slots; using CPU readback.");
                HoloKitVideoRecorder_ReleaseGPUSlots();
                return false;
            }

            _gpuSlots = new Texture2D[created];
            _gpuSlotTimes = new double[created];
            _gpuSlotPending = new bool[created];
            _gpuSlotCursor = 0;

            for (int i = 0; i < created; i++)
            {
                IntPtr native = HoloKitVideoRecorder_GetGPUSlotTexture(i);
                if (native == IntPtr.Zero)
                {
                    Debug.LogWarning("[HoloKitVideoRecorder] Null slot texture; using CPU readback.");
                    ReleaseGpuSlots();
                    return false;
                }
                _gpuSlots[i] = Texture2D.CreateExternalTexture(
                    width, height, UnityEngine.TextureFormat.BGRA32, false, false, native);
            }

            Debug.Log($"[HoloKitVideoRecorder] GPU slots ready: {created} at {width}x{height}");
            return true;
        }

        /// <summary>
        /// Submits any slots still waiting out their latency, oldest first, so
        /// timestamps stay monotonic.
        /// </summary>
        void FlushGpuSlots()
        {
            if (_gpuSlots == null)
            {
                return;
            }

            // _gpuSlotCursor points at the next slot to write, so the oldest
            // pending one sits k_SlotLatency places behind it.
            for (int i = 0; i < _gpuSlots.Length; i++)
            {
                int slot = (_gpuSlotCursor + _gpuSlots.Length - k_SlotLatency + i) % _gpuSlots.Length;
                if (!_gpuSlotPending[slot])
                {
                    continue;
                }
                HoloKitVideoRecorder_SubmitGPUSlot(slot, _gpuSlotTimes[slot]);
                _gpuSlotPending[slot] = false;
            }
        }

        void ReleaseGpuSlots()
        {
            if (_gpuSlots != null)
            {
                // CreateExternalTexture returns ordinary Unity objects. Dropping
                // the array frees nothing, so every record/stop cycle would leak
                // one wrapper per slot. Destroy them before native frees the
                // textures underneath them.
                for (int i = 0; i < _gpuSlots.Length; i++)
                {
                    if (_gpuSlots[i] != null)
                    {
                        Destroy(_gpuSlots[i]);
                    }
                }
            }

            HoloKitVideoRecorder_ReleaseGPUSlots();
            _gpuSlots = null;
            _gpuSlotTimes = null;
            _gpuSlotPending = null;
            _gpuSlotCursor = 0;
        }

        void CommitFrameToGpuSlot()
        {
            int slot = _gpuSlotCursor;
            _gpuSlotCursor = (_gpuSlotCursor + 1) % _gpuSlots.Length;

            // Submit the slot written k_SlotLatency frames ago: by now the GPU has
            // certainly finished copying into it.
            int ripe = (slot + _gpuSlots.Length - k_SlotLatency) % _gpuSlots.Length;
            if (_gpuSlotPending[ripe])
            {
                int status = HoloKitVideoRecorder_SubmitGPUSlot(ripe, _gpuSlotTimes[ripe]);
                _gpuSlotPending[ripe] = false;
                if (status != 0)
                {
                    Debug.LogWarning($"[HoloKitVideoRecorder] Slot {ripe} submit returned {status}");
                }
            }

            // GPU-side copy, scheduled by Unity, so no command buffer of ours is
            // needed and ordering after the render is guaranteed.
            Graphics.CopyTexture(_frameRt, _gpuSlots[slot]);
            _gpuSlotTimes[slot] = _timeQueue.Dequeue();
            _gpuSlotPending[slot] = true;
        }

        void CommitFrame(Camera source)
        {
            if (!IsRecording) return;
            if (!_timeQueue.TryEnqueueNow(_clock.timestamp)) return;

            var prevTarget = source.targetTexture;
            source.targetTexture = _cameraRt;
            source.Render();
            source.targetTexture = prevTarget;
            Graphics.Blit(_cameraRt, _frameRt, new Vector2(1, -1), new Vector2(0, 1));

            if (_useGpuPath && _gpuSlots != null)
            {
                CommitFrameToGpuSlot();
                return;
            }

            // Fallback for any backend that is not Metal (the plugin reports this at
            // StartRecording). Reads the frame back to the CPU and copies it.
            AsyncGPUReadback.Request(_frameRt, 0, UnityEngine.TextureFormat.RGBA32, OnSourceReadback);
        }
        public void ToggleRecording()
        {
            if (IsRecording)
                EndRecording();
            else
                StartRecording();
        }

#if !UNITY_EDITOR && UNITY_IOS
            const string DllName = "__Internal";
#else
        const string DllName = "UnityHoloKit";
#endif

        /// <param name="width">Video width.</param>
        /// <param name="height">Video height.</param>
        /// <param name="sampleRate">Audio sample rate. Pass 0 for no audio.</param>
        /// <param name="channelCount">Audio channel count. Pass 0 for no audio.</param>
        /// <param name="videoBitRate">Video bit rate in bits per second.</param>
        /// <param name="audioBitRate">Audio bit rate in bits per second.</param>
        [DllImport(DllName)]
        public static extern int HoloKitVideoRecorder_StartRecording(string filePath,
            int width, int height, float audioSampleRate, int audioChannelCount);

        [DllImport(DllName)]
        public static extern int HoloKitVideoRecorder_AppendVideoFrame(IntPtr data, int size, double time);

        [DllImport(DllName)]
        public static extern int HoloKitVideoRecorder_AppendAudioFrame(IntPtr data, int size, double time);

        [DllImport(DllName)]
        public static extern void HoloKitVideoRecorder_EndRecording();

        // Zero-copy GPU path. Implemented in VideoRecorderMetalBridge.mm, built for
        // both iOS and macOS so the Editor runs the same code that ships.
        //
        // MarshalAs(I1) matters: C++ bool is 1 byte, but the .NET default for bool is
        // the 4-byte Win32 BOOL, which would read three bytes of garbage.
        [DllImport(DllName)]
        private static extern int HoloKitVideoRecorder_GetSubmittedFrameCount();

        [DllImport(DllName)]
        private static extern int HoloKitVideoRecorder_GetDroppedFrameCount();

        [DllImport(DllName)]
        private static extern void HoloKitVideoRecorder_ResetGPUCounters();

        // Slot-based GPU path (iOS): no IUnityInterfaces needed.
        [DllImport(DllName)]
        private static extern int HoloKitVideoRecorder_AllocateGPUSlots(int count, int width, int height);

        [DllImport(DllName)]
        private static extern IntPtr HoloKitVideoRecorder_GetGPUSlotTexture(int index);

        [DllImport(DllName)]
        private static extern int HoloKitVideoRecorder_SubmitGPUSlot(int index, double time);

        [DllImport(DllName)]
        private static extern void HoloKitVideoRecorder_ReleaseGPUSlots();
    }
}
#endif
