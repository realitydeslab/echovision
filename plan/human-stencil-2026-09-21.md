# Human stencil registration investigation

## Live hand test, 2026-09-21

The user put a hand/forearm in view and rotated the phone. The diagnostic shader samples the **same** human-stencil texture twice: red uses the production `DepthImageProcessor.DisplayRotatioMatrix`; cyan uses the complete matrix on the actual AR background material. White is overlap, not a third detection.

18 screenshots are preserved in ignored `Build_RecoveryArtifacts/hand-stencil-20260921/`. `stencil-LandscapeLeft-2-170418.png` shows a large orientation mismatch: red runs across the display while cyan broadly follows the actual upright arm. `stencil-Portrait-2-170439.png` shows a smaller but clear scale/offset mismatch. `stencil-LandscapeLeft-2-170453.png` also shows that physical upside-down does not imply the iPhone interface accepted PortraitUpsideDown. The user independently reported the same landscape/portrait behavior.

QuickTime USB screen capture included both overlays, unlike the app GPU recorder (which excludes ScreenSpaceOverlay UI). It stopped unexpectedly; only 3.683916 seconds / 222 H.264 frames and AAC audio were recoverable. `hand-screen-partial.mov` was saved and fully decoded successfully. This is **not** a complete recording of the gesture sequence. No cause for QuickTime's interruption has been established.

## Cause and scoped fix

The old processor copied a picture-in-picture depth sample: it normalized affine axes and rounded translation, discarding the viewport crop and scale. It also consumed the provider frame matrix, while the iOS 27 camera compatibility component corrects the rendered background's orientation with the native ARKit display transform. The result is two different image-to-screen mappings.

The proposed fix shares the complete actual background matrix, retaining a full frame-matrix fallback; removes the picture-in-picture normalization/rounding; refreshes shader/VFX matrices after the compatibility component's before-render update; and clamps stencil sampling at texture edges. No extra orientation offset or accumulated rotation is added.

## Verification status

Live **before-fix** evidence establishes the mapping defect and supports the cyan mapping as the repair direction. The fix is committed as `1169500`. All 27 EditMode tests pass, including four orientation/crop regressions and first-frame readiness. Four after-fix rapid sleep/wake cycles pass in Mono portrait, both landscapes and Stereo; background, production stencil and VFX matrices agree exactly after wake. The user had removed their hand before those captures, so they validate the transform/state path, not a second live contour-quality assessment. The final full orientation/4K state walker subsequently passed with failures=0 and five fully decoded GPU movies. See `release-validation-2026-09-21.md`. Small cyan contour discrepancies are not yet attributed: segmentation resolution, time lag and geometric mapping must be evaluated separately.
