using NUnit.Framework;
using UnityEditor;

// EchoVision shows Portrait and LandscapeLeft only. Turning the phone upside down is the gesture
// that puts it to sleep (PowerSaver), which only works if iOS never rotates to the upside-down
// orientations.
public class OrientationSettingsTests
{
    [Test] public void UpsideDownOrientationsAreNotAllowed()
    {
        Assert.That(PlayerSettings.allowedAutorotateToPortraitUpsideDown, Is.False, "upside-down portrait");
        Assert.That(PlayerSettings.allowedAutorotateToLandscapeRight, Is.False, "upside-down landscape (LandscapeRight)");
    }

    [Test] public void PortraitAndLandscapeLeftAreAllowed()
    {
        Assert.That(PlayerSettings.allowedAutorotateToPortrait, Is.True);
        Assert.That(PlayerSettings.allowedAutorotateToLandscapeLeft, Is.True);
    }
}
