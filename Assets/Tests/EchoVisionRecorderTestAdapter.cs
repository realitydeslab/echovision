#if UNITY_IOS && DEVELOPMENT_BUILD && ECHOVISION_DEVICETEST && HOLOKIT_RECORDER_TESTS
using System.Reflection;
using HoloKit.Tests;
using UnityEngine;

// Only EchoVision's scene wiring and mask sleep behavior belong in this project.
static class EchoVisionRecorderTestAdapter
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        RecorderStopProbe.PrepareScene = recorder =>
        {
            var microphone = Object.FindFirstObjectByType<MicrophoneAPI>();
            recorder.MicrophoneInput = microphone.MicrophoneInput;
            // Physical placement must not put an automated benchmark to sleep.
            Object.FindFirstObjectByType<PowerSaver>().enabled = false;
        };
        RecorderAudioProbe.PrepareScene = recorder =>
        {
            // The audio fixture owns capture to compare raw and AEC modes.
            // EchoVision's environmental-analysis lease must be released first.
            var microphone = Object.FindFirstObjectByType<MicrophoneAPI>();
            recorder.MicrophoneInput = microphone.MicrophoneInput;
            var processor = Object.FindFirstObjectByType<AudioProcessor>();
            var power = Object.FindFirstObjectByType<PowerSaver>();
            bool microphoneEnabled = microphone.enabled, processorEnabled = processor.enabled, powerEnabled = power.enabled;
            RecorderAudioProbe.RestoreScene = () =>
            {
                microphone.enabled = microphoneEnabled;
                processor.enabled = processorEnabled;
                power.enabled = powerEnabled;
            };
            microphone.enabled = false;
            processor.enabled = false;
            power.enabled = false;
        };
        RecorderStopProbe.SetSleeping = sleeping =>
        {
            var power = Object.FindFirstObjectByType<PowerSaver>();
            typeof(PowerSaver).GetMethod(sleeping ? "EnterSleepMode" : "ExitSleepMode", Private)
                .Invoke(power, null);
            if (sleeping) typeof(PowerSaver).GetMethod("SetObjectListState", Private)
                .Invoke(power, new object[] { false });
        };
    }
}
#endif
