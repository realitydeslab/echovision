# iOS 27 AR background rotation: diagnosis and workaround

## Status: worked around in the HoloKit SDK, verified on device

`Packages/io.holokit.unity-sdk/Runtime/iOS/ARBackgroundOrientationFix.cs` plus
`Runtime/iOS/Shaders/ARKitBackgroundOrientationFix.shader` and
`Runtime/Resources/ARKitBackgroundOrientationFix.mat`.

---

## The bug

On iOS 27 the AR Foundation camera background renders rotated 90 degrees in landscape while
the UI stays upright. ARKit itself is fine: a pure Swift app rotates correctly on the same
device. The fault is in AR Foundation's conversion of ARKit's `displayTransform` into
`displayMatrix`, and every version tested (6.1.0 and 6.4.3) is affected, so it is not a
package regression -- iOS 27 breaks all of them. Upstream report:
https://discussions.unity.com/t/ar-content-is-rotated-90-degrees-in-ios-27-beta/1735923

Two things damn AR Foundation's matrix, both visible in the device log:

```
                 a       b       tx   |   c       d       ty
Portrait  ours   0.000  -1.000   1.000 |  -0.615   0.000   0.808
Portrait  arf    0.000  -1.000   1.000 |  -0.615   0.000   0.808   <- correct
LL        ours   1.000   0.000   0.000 |   0.000  -0.615   0.808
LL        arf    0.000  -0.346   0.673 |  -1.000   0.000   1.000   <- anti-diagonal, wrong
LR        ours  -1.000   0.000   1.000 |   0.000   0.615   0.192
LR        arf    0.000  -0.346   0.673 |  -1.000   0.000   1.000   <- identical to LL
```

It stays anti-diagonal in landscape where ARKit goes diagonal, and it returns a bit-identical
matrix for LandscapeLeft and LandscapeRight despite their being 180 degrees apart.

## The workaround

`ARCameraBackground` can be driven with a custom material
(`useCustomMaterial` + `customMaterial`), and it keeps feeding that material the camera
textures, the shader keywords and `_UnityDisplayTransform` exactly as it feeds its own. So the
shader is the stock `Unlit/ARKitBackground` with one change: the UV remap may come from
`_EchoDisplayTransform` instead. `_EchoDisplayTransformValid` selects between them, so the
material still renders correctly on its own if the script is absent.

The transform is rebuilt from the screen orientation, the camera texture size and the viewport
size:

1. An unrotated basis per orientation. Portrait is AR Foundation's own (correct) portrait
   matrix with the scale divided out: texture +U runs down the screen, +V runs left. The other
   three are that basis rotated. The determinant is -1 throughout because the texture's V axis
   is opposite to Unity's.
2. Aspect fill: cover the viewport and crop the overhang, never letterbox.
3. Recentre the cropped window.

It follows the stock row-vector convention, `texX = a*u + b*v + tx`, so it is a drop-in.

## How it was verified

1. **Portrait is bit-identical to AR Foundation's**, translation terms included. Portrait is
   the one orientation AR Foundation gets right, so this checks the convention, the property
   plumbing and the aspect maths in one shot.
2. **Landscape verified upright on device.** The accelerometer pose and the interface pose were
   logged together (`device=LandscapeLeft orientation=LandscapeLeft`); when they agree the
   render must look upright whatever way the phone is being held. It did -- books vertical,
   shelves horizontal. This is the case that was broken.
3. LandscapeRight and PortraitUpsideDown are the two verified cases rotated 180 degrees by
   construction. LandscapeRight was observed 180 degrees from LandscapeLeft, which is what a
   correct implementation gives when the physical pose does not change. Neither was
   independently confirmed against a matching physical pose.

Note that `devicectl device orientation set` simulates the accelerometer, so forcing an
orientation rotates the interface but not the real camera. "Is the scene upright" is only a
valid test when the interface pose and the physical pose agree, which is why both are logged.

## Removing it

Delete the three files. The component restores `useCustomMaterial = false` in `OnDestroy`, and
the shader falls back to `_UnityDisplayTransform`, so nothing else needs unwinding. Leaving it
in place after Unity ships a fix would over-rotate the background.

## Tooling that made this cheap

- `xcrun devicectl device orientation set --device <udid> <portrait|landscapeLeft|landscapeRight|portraitUpsideDown>`
- `xcrun devicectl device capture screenshot --device <udid> --destination <path>`
- `xcodegen` is installed, so a throwaway Swift comparison app takes minutes.
- Do not use `UIScreen.main` or a manual `UIWindow` in a probe app -- deprecated, and traps
  with SIGTRAP on iOS 27. Use SwiftUI and let the framework own the window.
