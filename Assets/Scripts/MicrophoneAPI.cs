using UnityEngine;
#if UNITY_IOS
using HoloKit.iOS;
#endif

// EchoVision's scene adapter. Capture and ownership belong to the SDK.
public class MicrophoneAPI : MonoBehaviour
{
#if UNITY_IOS
    [SerializeField] HoloKitVideoRecorder videoRecorder;
    [SerializeField] HoloKitMicrophoneCapture microphoneInput;
    public HoloKitMicrophoneCapture MicrophoneInput => microphoneInput;

    void OnEnable()
    {
        if (microphoneInput != null)
        {
            bool ready = microphoneInput.Acquire(this, MicrophoneProcessing.Environment);
            if (!ready && !microphoneInput.IsPreparing) Debug.LogError("[EchoVision] " + microphoneInput.LastError);
        }
        else Debug.LogError("[EchoVision] Assign the shared microphone input.");
    }
    void OnDisable()
    {
        if (microphoneInput != null) microphoneInput.Release(this);
    }
    public void ToggleRecording() => videoRecorder.ToggleRecording();
    public void StartRecording() => videoRecorder.StartRecording();
    public void StopRecording() => videoRecorder.EndRecording();
#endif
}
