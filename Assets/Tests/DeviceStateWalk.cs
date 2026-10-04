#if UNITY_IOS && DEVELOPMENT_BUILD && ECHOVISION_DEVICETEST
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using HoloKit;
using HoloKit.iOS;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Only the dedicated recovery test profile enables this walker.
public class DeviceStateWalk : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
#if HOLOKIT_RECORDER_TESTS
        if (HoloKit.Tests.RecorderStopProbe.Requested ||
            HoloKit.Tests.RecorderStopProbe.RequestedMode == "manual") return;
#endif
        Debug.Log("[STATE] START automatic recovery test profile");
        new GameObject(nameof(DeviceStateWalk)).AddComponent<DeviceStateWalk>();
    }

    HoloKitCameraManager manager;
    HoloKitVideoRecorder recorder;
    MicrophoneAPI microphone;
    Camera cameraToTest;
    readonly float[] view = new float[16], projection = new float[16], display = new float[8];
    readonly List<XRSessionSubsystem> sessions = new();
    int failures;
    AudioProcessor audioProcessor;
    SoundWaveEmitter emitter;
    FieldInfo waveIndex;
    int lastWaveIndex;
    float audioLogTime;

    void Update()
    {
        if (audioProcessor == null) return;
        int current = (int)waveIndex.GetValue(emitter);
        if (current != lastWaveIndex)
        {
            Debug.Log($"[AUDIO-TRIGGER] EMIT volume={audioProcessor.AudioVolume:F4} recording={recorder.IsRecording} mode={manager.ScreenRenderMode}");
            lastWaveIndex = current;
        }
        if (Time.unscaledTime >= audioLogTime)
        {
            audioLogTime = Time.unscaledTime + .25f;
            Debug.Log($"[AUDIO-TRIGGER] volume={audioProcessor.AudioVolume:F4} pitch={audioProcessor.AudioPitch:F4} micReady={audioProcessor.MicrophoneInput.IsReady} micRate={audioProcessor.MicrophoneInput.SampleRate} recording={recorder.IsRecording} mode={manager.ScreenRenderMode}");
        }
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(5);
        manager = FindFirstObjectByType<HoloKitCameraManager>();
        recorder = FindFirstObjectByType<HoloKitVideoRecorder>();
        microphone = FindFirstObjectByType<MicrophoneAPI>();
        cameraToTest = recorder.GetComponent<Camera>();
        audioProcessor = FindFirstObjectByType<AudioProcessor>();
        emitter = FindFirstObjectByType<SoundWaveEmitter>();
        waveIndex = typeof(SoundWaveEmitter).GetField("nextEmitIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        typeof(AudioProcessor).GetField("_logAnalysis", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(audioProcessor, true);
        SubsystemManager.GetSubsystems(sessions);
        foreach (var orientation in new[] { ScreenOrientation.Portrait, ScreenOrientation.LandscapeLeft,
            ScreenOrientation.PortraitUpsideDown, ScreenOrientation.LandscapeRight })
        {
            manager.ScreenRenderMode = ScreenRenderMode.Mono;
            Screen.orientation = orientation;
            yield return new WaitForSeconds(2);
            Debug.Log($"[STATE] requested={orientation} effective={Screen.orientation}");
            yield return Snapshot("idle");
            microphone.StartRecording();
            yield return new WaitForSeconds(3);
            Check(recorder.IsRecording, "recording remains active");
            yield return Snapshot("record");
            microphone.StopRecording();
            yield return FinishMovie();
        }
        manager.ScreenRenderMode = ScreenRenderMode.Stereo;
        yield return new WaitForSeconds(2);
        yield return Snapshot("stereo-idle");
        Check(Application.targetFrameRate is > 0 and <= 60, $"stereo frame rate is at most 60 (target {Application.targetFrameRate})");
        Check(manager.StereoBar != null && manager.StereoBar.isActiveAndEnabled && manager.StereoBar.HasControls, "stereo bar is shown in Stereo");
        microphone.StartRecording();
        yield return new WaitForSeconds(3);
        Check(recorder.IsRecording, "stereo recording remains active");
        yield return Snapshot("stereo-record");
        manager.ScreenRenderMode = ScreenRenderMode.Mono;
        Screen.orientation = ScreenOrientation.Portrait;
        yield return new WaitForSeconds(2);
        yield return Snapshot("record-stereo-to-portrait");
        Check(Application.targetFrameRate is > 0 and <= 60, $"mono frame rate is at most 60 (target {Application.targetFrameRate})");
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        yield return new WaitForSeconds(2);
        yield return Snapshot("record-portrait-to-landscape");
        manager.ScreenRenderMode = ScreenRenderMode.Stereo;
        yield return new WaitForSeconds(2);
        Check(recorder.IsRecording, "recording survives mode and orientation changes");
        microphone.StopRecording();
        yield return FinishMovie();

        // RECORD on the stereo bar takes its own start path, which drops the microphone when it
        // thinks the permission is missing.
        string barStart = null;
        bool noPermission = false;
        void Watch(string message, string stack, LogType type)
        {
            if (message.StartsWith("[Recorder] START")) barStart = message;
            if (message.Contains("No microphone permission")) noPermission = true;
        }
        Application.logMessageReceived += Watch;
        var toggle = typeof(HoloKit.UI.HoloKitStereoBar).GetMethod("ToggleRecording", BindingFlags.Instance | BindingFlags.NonPublic);
        toggle.Invoke(manager.StereoBar, null);
        yield return new WaitForSeconds(3);
        toggle.Invoke(manager.StereoBar, null);
        yield return FinishMovie();
        Application.logMessageReceived -= Watch;
        Check(!noPermission && barStart != null && barStart.Contains("sources=2"), $"stereo bar recording includes the microphone ({barStart})");

        manager.ScreenRenderMode = ScreenRenderMode.Mono;
        Screen.orientation = ScreenOrientation.AutoRotation;
        Debug.Log($"[STATE] COMPLETE failures={failures}");
    }

    IEnumerator FinishMovie()
    {
        float deadline = Time.realtimeSinceStartup + 15;
        while (recorder.IsFinishing && Time.realtimeSinceStartup < deadline) yield return null;
        Check(!recorder.IsFinishing, "writer finalized");
        Check(File.Exists(recorder.LastRecordingPath) && new FileInfo(recorder.LastRecordingPath).Length > 1024, "movie exists");
        Check(recorder.SubmittedFrames > 0, "video frames submitted");
        Check(!recorder.AudioFailed, "recorded audio drained without loss");
        Check(microphone.MicrophoneInput.IsReady && microphone.MicrophoneInput.ConsumerCount == 1,
            "analysis microphone remains active after recording");
        Debug.Log($"[STATE] MOVIE frames={recorder.SubmittedFrames} dropped={recorder.DroppedFrames} path={recorder.LastRecordingPath}");
        yield return new WaitForSeconds(1);
    }

    void Check(bool pass, string assertion)
    {
        if (!pass) failures++;
        Debug.Log($"[STATE] {(pass ? "PASS" : "FAIL")} {assertion}");
    }

    IEnumerator Snapshot(string label)
    {
        // Sample after TrackedPoseDriver and the compatibility callback rendered this frame.
        yield return new WaitForEndOfFrame();
        Debug.Log($"[STATE] {label} orientation={Screen.orientation} physical={Input.deviceOrientation} mode={manager.ScreenRenderMode} recording={recorder.IsRecording} fps={Application.targetFrameRate} gpu={recorder.UsesGpuPath} size={Screen.width}x{Screen.height} volume={FindFirstObjectByType<AudioProcessor>().AudioVolume:F4}");
        Check(microphone.MicrophoneInput.IsReady &&
            microphone.MicrophoneInput.ConsumerCount == (recorder.IsRecording ? 2 : 1),
            "analysis and recorder hold independent microphone leases");
        var bg = cameraToTest.GetComponent<ARCameraBackground>();
        Debug.Log($"[BACKGROUND] matrix={bg.material.GetMatrix("_UnityDisplayTransform")} textureY={bg.material.GetTexture("_textureY")?.width}x{bg.material.GetTexture("_textureY")?.height}");
        if (sessions.Count > 0 && HoloKit_GetARCameraReference(sessions[0].nativePtr, (int)Screen.orientation,
            Screen.width, Screen.height, view, projection, display) != 0)
        {
            var nativeView = new Matrix4x4();
            for (int i=0;i<16;i++) nativeView[i] = view[i];
            var flip = Matrix4x4.Scale(new Vector3(1,1,-1));
            var referencePose = flip * nativeView.inverse * flip;
            float angle = Quaternion.Angle(referencePose.rotation, cameraToTest.transform.localRotation);
            Check(angle < 2f, $"{label} rendered pose matches ARKit ({angle:F2} degrees)");
            // The virtual camera must take ARKit's projection for the current capture format
            // (4K changes the intrinsics), not keep the 1920x1440 field of view.
            Matrix4x4 unityProjection = cameraToTest.projectionMatrix;
            float Fov(float focal) => 2 * Mathf.Atan(1 / focal) * Mathf.Rad2Deg;
            float unityH = Fov(unityProjection.m00), unityV = Fov(unityProjection.m11);
            float nativeH = Fov(projection[0]), nativeV = Fov(projection[5]);
            if (manager.ScreenRenderMode == ScreenRenderMode.Mono)
                Check(Mathf.Abs(unityH - nativeH) < 0.5f && Mathf.Abs(unityV - nativeV) < 0.5f,
                    $"{label} virtual camera FOV follows ARKit (unity {unityH:F2}x{unityV:F2}, native {nativeH:F2}x{nativeV:F2} deg)");
            Debug.Log($"[FOV] {label} texture={display[6]}x{display[7]} mode={manager.ScreenRenderMode} unityH={unityH:F2} unityV={unityV:F2} nativeH={nativeH:F2} nativeV={nativeV:F2}");
            if (recorder.IsRecording)
            {
                Check(recorder.UsesGpuPath, "GPU capture selected");
                Check(display[6] == 3840 && display[7] == 2160, "4K camera input active");
            }
            Debug.Log($"[REFERENCE] {label} poseErrorDegrees={angle:F2} nativeRotation={referencePose.rotation.eulerAngles} unityRotation={cameraToTest.transform.localEulerAngles} texture={display[6]}x{display[7]} display={string.Join(",",display)}");
        }
        var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        var path = Path.Combine(Application.persistentDataPath, $"state-{Screen.orientation}-{label}.png");
        File.WriteAllBytes(path, screenshot.EncodeToPNG());
        Destroy(screenshot);
        Debug.Log($"[STATE] SCREENSHOT path={path}");
        // Encoding a full-resolution PNG blocks the main thread. Let video
        // capture resume before measuring or stopping the recording.
        yield return new WaitForSeconds(.25f);
    }

    [DllImport("__Internal")]
    static extern int HoloKit_GetARCameraReference(IntPtr session, int orientation, int width, int height,
        [Out] float[] view, [Out] float[] projection, [Out] float[] display);
}
#endif
