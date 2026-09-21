# HoloKit SDK — iOS 27 / Xcode 27 compatibility audit

Scope: `Packages/io.holokit.unity-sdk` (embedded local fork, v0.5.1).
Toolchain checked against: Xcode 27.0 (27A266a), iOS SDK 27.0, Unity 6000.3.24f1.

## Verdict
Mostly compatible. **One hard build breaker**, verified by running the linker. Everything else
is stale metadata or cosmetic.

---

## P0 — BUILD BREAKER: `-ld64` linker flag

`Editor/HoloKitBuildProcessor.cs:31`
```csharp
project.AddBuildProperty(unityFrameworkTargetGuid, "OTHER_LDFLAGS", "-ld64");
```
`-ld64` selected Apple's classic linker. That selector no longer exists in Xcode 27, so the
driver now parses it as `-l d64` — "link a library named d64".

Verified empirically, not from memory:
```
$ xcrun -sdk iphoneos clang -target arm64-apple-ios26.0 ld_probe.c -o ok        # control
(exit 0, links fine)

$ xcrun -sdk iphoneos clang -target arm64-apple-ios26.0 -ld64 ld_probe.c -o x
ld: library 'd64' not found
clang: error: linker command failed with exit code 1
```
Same failure with `-Wl,-ld64`.

Impact: every iOS build from this project fails at link time for the UnityFramework target.
Fix: delete that one line. The classic linker was a workaround for an Xcode 15-era bug and has
no purpose on Xcode 27.
Status: **NOT yet applied** — the file is unmodified from the fork (`git log` -> "first commit"),
so this is an upstream fix affecting all HoloKit users, not just this project.

---

## P2 — stale package metadata — FIXED 2026-09-21

`package.json` declared a Unity/ARFoundation baseline two majors behind what the project runs.
Now aligned with the resolved versions:
```diff
-  "unity": "2022.3",
+  "unity": "6000.0",
-    "com.unity.xr.arfoundation": "5.1.5",
-    "com.unity.xr.arkit": "5.1.5"
+    "com.unity.xr.arfoundation": "6.4.1",
+    "com.unity.xr.arkit": "6.4.1"
```
`6000.0` is the Unity 6 baseline (broadest Unity 6 compatibility) rather than `6000.3`, so the
package is not needlessly restricted to 6.3+. Verified: JSON parses, and packages-lock.json
resolves both AR packages to exactly 6.4.1.

This was always metadata-only — embedded packages are not constraint-enforced, and the SDK already
compiled clean against 6.4.1 (0 errors; definitely compiled, since its files emit CS0618 warnings).
The value is for anyone republishing the fork.

## P2 — pre-existing duplicate in the phone model list
`iPhone14,5` appears twice, as "iPhone 13" and as "iPhone 13 Mini". Pre-existing (present in the
file before this task). Lookup is first-match-wins so the correct "iPhone 13" specs are used, but
the second row is dead and mislabelled — iPhone 13 mini is actually `iPhone14,4`, which is absent.
Not fixed; separate concern.

## P3 — `ENABLE_BITCODE = NO`
`HoloKitBuildProcessor.cs:29-30`. Bitcode was removed in Xcode 14. Setting it to NO is a no-op,
not an error. Leave it.

## P3 — native memory leak (not an iOS 27 issue)
`Runtime/iOS/NativeCode/HoloKitARKitNativeProvider.mm:21` `getUnityMatrix:` does
`return new float[16]{...}`, called at `:90` per ARKit frame. The C# side
(`Runtime/iOS/HoloKitARKitNativeProvider.cs:132`) does `Marshal.Copy` but never frees, and no
`Marshal.FreeHGlobal`/`free` exists anywhere in `Runtime/`. That leaks 64 bytes per frame
(~14 KB/s at 60 fps). Pre-existing; unrelated to this upgrade.

---

## Checked and CLEAN
- No removed UIKit APIs: no `UIScreen.main`, `statusBarOrientation`, `keyWindow`,
  `UIApplication.windows`, `UIGraphicsBeginImageContext`.
- Vision: `VNDetectHumanHandPoseRequest` / `VNImageRequestHandler`
  (`AppleVisionHandPoseDetector.m:11,26,48`) are soft-deprecated in favour of the Swift Vision
  API but still present and functional in the iOS 27 SDK.
- `VideoRecorder.mm` (452 lines, locally modified): uses `AVAssetWriter`,
  `AVAssetWriterInputPixelBufferAdaptor`, `CVPixelBuffer` — all current, none deprecated. The
  `AVCaptureSession` / `AVAudioSession` block is commented out.
- Availability guards are used properly, e.g. `@available(iOS 16, *)` around
  `recommendedVideoFormatFor4KResolution` (`HoloKitARKitNativeProvider.mm:52`).
- `NSPhotoLibraryAddUsageDescription` injected by the post-processor;
  `NSMicrophoneUsageDescription` comes from Unity PlayerSettings (`microphoneUsageDescription:
  Used for AR`) — both present.

## Not verifiable statically
Whether Unity 6000.3.24f1's iOS trampoline itself compiles against the iOS 27 SDK. Its guards
top out at `@available(iOS 26` / `__IPHONE_18_0`, which is a current toolchain, but only a real
build proves it. That build is still blocked by the visionOS licence issue.
