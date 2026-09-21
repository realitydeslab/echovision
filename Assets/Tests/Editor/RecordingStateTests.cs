#if UNITY_IOS
using HoloKit;
using HoloKit.iOS;
using NUnit.Framework;
using UnityEngine;

public class RecordingStateTests
{
    [TestCase(9f / 16, 16f / 9)]
    [TestCase(16f / 9, 9f / 16)]
    [TestCase(3f / 4, 4f / 3)]
    [TestCase(4f / 3, 3f / 4)]
    [TestCase(9f / 16, 9f / 16)]
    [TestCase(16f / 9, 16f / 9)]
    public void RotateWhileRecording_PreservesAspectAndCentersImage(float source, float output)
    {
        Rect rect = HoloKitVideoRecorder.FitViewport(source, output);
        Assert.That(rect.width * output / rect.height, Is.EqualTo(source).Within(0.0001));
        Assert.That(rect.center, Is.EqualTo(new Vector2(.5f, .5f)));
        Assert.That(rect.width, Is.InRange(0.0001f, 1f));
        Assert.That(rect.height, Is.InRange(0.0001f, 1f));
        Assert.That(Mathf.Max(rect.width, rect.height), Is.EqualTo(1f));
    }

    [TestCase(false, ScreenRenderMode.Mono)]
    [TestCase(true, ScreenRenderMode.Mono)]
    [TestCase(false, ScreenRenderMode.Stereo)]
    [TestCase(true, ScreenRenderMode.Stereo)]
    public void FourK_StartSwitchModeStop_RestoresPreference(bool preferred, ScreenRenderMode initial)
    {
        var other = initial == ScreenRenderMode.Mono ? ScreenRenderMode.Stereo : ScreenRenderMode.Mono;
        Assert.That(ARBackgroundVideoEnhancementManager.ShouldEnhance(preferred, true, initial, true), Is.True);
        Assert.That(ARBackgroundVideoEnhancementManager.ShouldEnhance(preferred, true, other, true), Is.True);
        Assert.That(ARBackgroundVideoEnhancementManager.ShouldEnhance(preferred, false, ScreenRenderMode.Mono, true), Is.EqualTo(preferred));
        Assert.That(ARBackgroundVideoEnhancementManager.ShouldEnhance(preferred, false, ScreenRenderMode.Stereo, true), Is.False);
    }

    [TestCase(ScreenRenderMode.Mono, false)]
    [TestCase(ScreenRenderMode.Mono, true)]
    [TestCase(ScreenRenderMode.Stereo, false)]
    [TestCase(ScreenRenderMode.Stereo, true)]
    public void FourK_OptOut_RespectsExistingModePreference(ScreenRenderMode mode, bool preferred)
    {
        Assert.That(ARBackgroundVideoEnhancementManager.ShouldEnhance(preferred, false, mode, false),
            Is.EqualTo(mode == ScreenRenderMode.Stereo || preferred));
    }
}
#endif
