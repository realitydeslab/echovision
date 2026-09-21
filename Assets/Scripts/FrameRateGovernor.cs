using UnityEngine;

public static class FrameRateGovernor
{
#if UNITY_IOS && !UNITY_EDITOR
    // Keep rendering at 60 fps; 4K camera capture independently runs at 30 fps.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply() => Application.targetFrameRate = 60;
#endif
}
