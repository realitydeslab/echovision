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

`ARCameraBackground` can be driven with a custom material (`useCustomMaterial` +
`customMaterial`), and it keeps feeding that material the camera textures, the shader keywords
and `_UnityDisplayTransform` exactly as it feeds its own. So the shader is the stock
`Unlit/ARKitBackground` with one change: the UV remap may come from `_EchoDisplayTransform`
instead. `_EchoDisplayTransformValid` selects between them, so the material still renders
correctly on its own if the script is absent.

## What the transform actually is

**On iOS 27 ARKit hands back a camera texture that is already oriented to the interface.** The
correct display transform therefore applies *no rotation at all*, in any orientation — only a
V-axis flip, because the texture's V runs opposite to Unity's, plus an aspect-fill crop:

```
a = 1, b = 0, c = 0, d = -1        (then scaled by the aspect-fill factors)
```

There is nothing orientation-dependent left. AR Foundation still applies its own rotation on
top of an already-rotated image, which is why its matrix is wrong — and wrong in **portrait as
well as landscape**.

### How this was established, and the mistake before it

An earlier version of this fix assumed AR Foundation's *portrait* matrix was correct, because
portrait "looked fine", and derived the other three orientations by rotating that basis. That
reproduced AR Foundation's portrait error exactly, bit for bit, and the exactness was then
mistaken for confirmation. Landscape happened to come out right, which masked it.

The question was settled by measurement instead: a calibration switch cycled the four candidate
bases on device, six seconds each, holding portrait, and each was photographed.

| basis | linear part | result in portrait |
|---|---|---|
| 0 quarter turn (the derived one) | `0,-1,-1,0` | bookshelf on its side |
| 1 quarter turn, other way | `0,1,1,0` | bookshelf on its side |
| **2 no rotation** | **`1,0,0,-1`** | **upright, AR overlay registered** |
| 3 half turn | `-1,0,0,1` | upside down |

Basis 2 is also what landscape independently needed, which is the point: one basis serves every
orientation.

Verified after the change, with the interface pose and the accelerometer pose agreeing in each
case: portrait shows books vertical and shelves horizontal; landscape likewise, with the AR
particles registered on the shelves.

## Removing it

Delete the three files, and drop the `CorrectedDisplayMatrix` branch in
`DepthImageProcessor.OnCameraFrameEventReceived`. The component restores
`useCustomMaterial = false` in `OnDestroy`, and the shader falls back to
`_UnityDisplayTransform`, so nothing else needs unwinding.

Note it is safe to leave in place: the transform is computed from the orientation, the texture
size and the viewport, and never composed with AR Foundation's, so a fixed AR Foundation would
not double-rotate it. An earlier version of this note claimed it would over-rotate; that was
wrong. There is no iOS-version gate for the same reason -- the computed transform is correct on
iOS 26 as well.

## Tooling that made this cheap

- `xcrun devicectl device orientation set --device <udid> <portrait|landscapeLeft|landscapeRight|portraitUpsideDown>`
- `xcrun devicectl device capture screenshot --device <udid> --destination <path>`
- `xcodegen` is installed, so a throwaway Swift comparison app takes minutes.
- Do not use `UIScreen.main` or a manual `UIWindow` in a probe app -- deprecated, and traps
  with SIGTRAP on iOS 27. Use SwiftUI and let the framework own the window.
