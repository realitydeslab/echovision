using System;
using HoloKit;
using UnityEngine;
#if UNITY_IOS
using HoloKit.iOS;
#endif

// Mono renders at 60 fps and, like the SDK's Stereo, steps down as the phone heats up: 40 fps at
// Serious, 30 at Critical. iOS's own "Adjust FPS based on thermal state" stays off because the SDK's
// project validation rejects it. Stereo is left to HoloKitCameraManager, and PowerSaver owns the
// rate while asleep.
public class FrameRateGovernor : MonoBehaviour
{
    public const int MonoFrameRate = 60;

    public static int MonoFrameRateFor(int thermalState) =>
        HoloKitCameraManager.StereoFrameRate(MonoFrameRate, thermalState, true);

    HoloKitCameraManager cameraManager;
    PowerSaver powerSaver;
    Func<int> readThermalState = () => 0;
    float nextCheck;

#if UNITY_IOS && !UNITY_EDITOR
    AppleNativeProvider apple;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        Application.targetFrameRate = MonoFrameRate;
        var governor = new GameObject(nameof(FrameRateGovernor)).AddComponent<FrameRateGovernor>();
        DontDestroyOnLoad(governor.gameObject);
        governor.apple = new AppleNativeProvider();
        governor.readThermalState = () => (int)governor.apple.GetThermalState();
    }

    void OnDestroy() => apple?.Dispose();
#endif

    void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1;
        if (cameraManager == null) cameraManager = FindFirstObjectByType<HoloKitCameraManager>();
        if (powerSaver == null) powerSaver = FindFirstObjectByType<PowerSaver>();
        Govern();
    }

    void Govern()
    {
        if (cameraManager == null || cameraManager.ScreenRenderMode != ScreenRenderMode.Mono) return;
        if (powerSaver != null && powerSaver.IsAsleep) return;
        int target = MonoFrameRateFor(readThermalState());
        if (Application.targetFrameRate != target) Application.targetFrameRate = target;
    }
}
