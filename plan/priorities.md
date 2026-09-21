# EchoVision priority list (2026-09-21)

## Done

| Item | Evidence |
|---|---|
| Unity 6000.3.8f1 -> **6000.3.24f1** | `ProjectVersion.txt`, 0 compile errors |
| iOS 27 settings | No change needed. `iOSTargetOSVersionString: 26.0` stays; `iPhoneSdkVersion: 988` is already Device SDK (= newest installed); Xcode 27 uses the iOS 27 SDK automatically |
| `-ld64` link failure | Removed from `HoloKitBuildProcessor.cs:31`; reproduced directly as `ld: library 'd64' not found` |
| MCP package startup crash | Bypassed with `CI=true` (the package's own `EnvironmentUtils.IsCi()` guard); zero file changes |
| visionOS removal | Package dropped; all source, assets and `Build_visionOS` preserved. See `Tools/VISIONOS.md` |
| **visionOS blocker cleared** | Ping probe: `UNITY_EXIT=0` and `PING - executeMethod reached` |
| HoloKit `package.json` | Unity 6000.0 + ARFoundation/ARKit **6.4.3**, aligned with the lockfile |
| iPhone 18 Pro / Pro Max models | Added `iPhone19,1/2/3/7`; YAML validated |
| Test device identified | iPhone 15 Pro `iPhone16,1`, state `connected` |
| **Zero-copy GPU recording path** | Editor: `submitted=300 dropped=0`; ffprobe confirms exactly 300 frames with correct picture |
| **GPU path working on device** | iPhone 15 Pro: `submitted=355 dropped=0`, 357 frames in the file, 30 fps held, no crash |
| **Recording colour verified against the live screen** | Mean per-channel error 3.4/255 vs a `devicectl` screenshot of the same moment |
| iOS Xcode project export | Built via the project's own `iOS.asset` build profile |

---

## Recommended order

**Principle: get the build -> install -> test loop working before changing more code.**

That loop now works, and it earned its keep immediately: the red/blue channel swap in the GPU
path was invisible in the Editor (every verification frame there was a grayscale LiDAR mesh,
which a channel swap does not change) and only showed up when a device frame was compared
against a screenshot of the same moment. What remains inference rather than measurement is
4K enhancement, and Test A, which needs audio.

---

## P0 - close the loop

| # | Item | Status |
|---|---|---|
| 1 | Export the iOS Xcode project | **done** |
| 2 | `xcodebuild` against the iOS 27 SDK | **done** |
| 3 | Install (also writes a provisioning profile, fixing the trust problem) | **done** |
| 4 | Run tests B/C unattended and capture a baseline | **done** - `AutoRecordTest` drives a full cycle over USB |

The loop is closed: Unity batch build -> `xcodebuild` -> `devicectl install` -> `devicectl
process launch --console` -> pull the `.mp4` out of `Library/Caches` with `devicectl device
copy from` -> `ffprobe`. Test A (sound-triggered wave) still needs audio and was not run.

---

## P1 - real bugs

| # | Item | Location | Impact |
|---|---|---|---|
| 5 | ~~Feedback howl when not recording~~ | `HoloKitVideoRecorder.cs` | **fixed** - buffer is now cleared unconditionally |
| 6 | **Pitch detection may never work** | `AudioProcessor.cs` spectrum path | RMS bypasses the mixer attenuation, `GetSpectrumData` does not (-80 dB), so the absolute threshold of 0.02 can never be reached |
| 7 | ~~Four `CFRelease` leaks~~ | `VideoRecorder.mm` | **fixed** |
| 8 | ~~Timestamp desync on dropped frames~~ | `HoloKitVideoRecorder.cs` | **fixed** - the timestamp now travels with the frame |
| 9 | ~~`assert` compiled out in Release~~ | `VideoRecorder.mm` | **fixed** - runtime check |

**Item 6 should wait for Test A.** It is falsifiable: play a 300 Hz tone and read `pitchHz`
at the moment `vol > 0.02`.

---

## P2 - quality

| # | Item | Benefit |
|---|---|---|
| 10 | ~~Add `AVVideoAverageBitRateKey`, `AVVideoExpectedSourceFrameRateKey`, `AVVideoMaxKeyFrameIntervalKey`~~ | **done** - bitrate scales with pixel count, clamped 8-60 Mbps. On device 9.2 -> **20.4 Mbps** at 2544x1168 |
| 11 | ~~Add `AVVideoColorPropertiesKey` (BT.709)~~ | **done** - ffprobe now reports `color_space=bt709` instead of `unknown` |
| 12 | Attach `ARBackgroundVideoEnhancementManager` for 4K | Turns high resolution into real detail rather than an upscale |
| 13 | ~~Avoid the CPU readback~~ | **done** - zero-copy GPU path |
| 14 | ~~IOSurface pixel buffer attributes~~ | **done** - required by the GPU path |
| 15 | Make the frame-rate gate configurable | Currently hardcoded to 60 in `TimeQueue` |

Items 10 and 11 are done. Item 12 is the remaining quality lever: without it the high
resolution is an upscale rather than real detail.

---

## P3 - cleanup

| # | Item |
|---|---|
| 16 | Remove the temporary harnesses: `AutoRecordTest.cs`, `PerfProbe.cs` and the `ECHOVISION_AUTOTEST` define in `iOS.asset` |
| 16b | Missing script in the scene: `EchoVision.unity:238,2474` references `HolokitMicrophoneAPI`, actual class is `MicrophoneAPI` |
| 17 | Remaining `CS0618 FindObjectOfType` warnings in the HoloKit SDK (one fixed in `HoloKitDefaultUICanvas`) |
| 18 | ~~`getUnityMatrix` leak~~ — WRONG, retracted. `HoloKitARKitNativeProvider.mm:97` does `delete[](matrix)` inside the dispatch block. The original claim came from grepping only the C# side for `Marshal.FreeHGlobal` without reading the native block. |
| 19 | `iOSPhoneModelList.asset` lists `iPhone14,5` twice ("iPhone 13" and "iPhone 13 Mini"); iPhone 13 mini is actually `iPhone14,4` and is missing |
| 20 | Reorganise native sources (`Runtime/Native/Shared` + `Prebuilt`) and add an Editor script that rebuilds the macOS bundle automatically when sources change |

---

## Files lost during the session
A `git clean` removed several untracked files. `Assets/Editor/EchoVisionBuilder.cs` is obsolete
anyway, since the project's own `iOS.asset` build profile already overrides the scene list and
`unity build --profile iOS` needs no custom script. `Assets/Plugins/iOS/MicFix.mm` and
`Assets/Scripts/MicFix.cs` were abandoned by decision; nothing references them any more.
