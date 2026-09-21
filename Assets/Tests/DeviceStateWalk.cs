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
            Debug.Log($"[AUDIO-TRIGGER] volume={audioProcessor.AudioVolume:F4} pitch={audioProcessor.AudioPitch:F4} micPosition={Microphone.GetPosition(null)} analysisPosition={audioProcessor.AudioSource.timeSamples} recording={recorder.IsRecording} mode={manager.ScreenRenderMode}");
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
        microphone.StartRecording();
        yield return new WaitForSeconds(3);
        Check(recorder.IsRecording, "stereo recording remains active");
        yield return Snapshot("stereo-record");
        manager.ScreenRenderMode = ScreenRenderMode.Mono;
        Screen.orientation = ScreenOrientation.Portrait;
        yield return new WaitForSeconds(2);
        yield return Snapshot("record-stereo-to-portrait");
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        yield return new WaitForSeconds(2);
        yield return Snapshot("record-portrait-to-landscape");
        manager.ScreenRenderMode = ScreenRenderMode.Stereo;
        yield return new WaitForSeconds(2);
        Check(recorder.IsRecording, "recording survives mode and orientation changes");
        microphone.StopRecording();
        yield return FinishMovie();
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
        Debug.Log($"[STATE] {label} orientation={Screen.orientation} physical={Input.deviceOrientation} mode={manager.ScreenRenderMode} recording={recorder.IsRecording} gpu={recorder.UsesGpuPath} size={Screen.width}x{Screen.height} volume={FindFirstObjectByType<AudioProcessor>().AudioVolume:F4}");
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
