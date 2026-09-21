using UnityEngine;

/// <summary>
///
/// Caps the render frame rate to match ARKit's output.
///
/// ARKit delivers camera frames at 60 Hz. On a ProMotion device (iPhone 13 Pro
/// and later) the display runs at up to 120 Hz, so without a cap the renderer
/// draws roughly twice as many frames as there is new AR data for. Those extra
/// frames cost GPU time, battery and heat while showing nothing new.
///
/// vSyncCount must be 0 for this to take effect: with vSync enabled Unity
/// ignores targetFrameRate entirely and follows the display refresh rate
/// instead. The iOS quality level ("Balanced") ships with vSyncCount = 1, which
/// is why setting targetFrameRate alone would silently do nothing.
///
/// </summary>
public static class FrameRateGovernor
{
    /// <summary>Matches ARKit's camera frame rate.</summary>
    public const int TargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        // Order matters: targetFrameRate is only honoured while vSync is off.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;

        Debug.Log($"[FrameRate] capped at {Application.targetFrameRate} fps " +
                  $"(vSyncCount={QualitySettings.vSyncCount}, " +
                  $"display={Screen.currentResolution.refreshRateRatio.value:F0}Hz)");
    }
}
