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
| iOS Xcode project export | Built via the project's own `iOS.asset` build profile |

---

## Recommended order

**Principle: get the build -> install -> test loop working before changing more code.**

Rationale: there has still been no run on real hardware. Every judgement about the GPU path,
4K enhancement and bitrate is inference or Editor-only data. Once the loop works, each
subsequent change can be verified immediately. Test A will also settle whether BUG-2 (pitch
detection) is real, and that result changes the ordering below.

---

## P0 - close the loop

| # | Item | Status |
|---|---|---|
| 1 | Export the iOS Xcode project | **done** |
| 2 | `xcodebuild` against the iOS 27 SDK | in progress |
| 3 | Install (also writes a provisioning profile, fixing the trust problem) | pending |
| 4 | Run tests A/B/C and capture a baseline | pending |

Device state: `devicectl device profile list` returned **No profiles found**, so every
sideloaded app currently fails to launch. Installing one freshly signed build fixes this.

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
| 10 | Add `AVVideoAverageBitRateKey` (~24 Mbps), `AVVideoExpectedSourceFrameRateKey`, `AVVideoMaxKeyFrameIntervalKey` | **Biggest quality win.** Measured 4.46 Mbps at 2768x1280 with the encoder left on its default |
| 11 | Add `AVVideoColorPropertiesKey` (BT.709) | Project uses Linear colour space; ffprobe reports `color_space=unknown` |
| 12 | Attach `ARBackgroundVideoEnhancementManager` for 4K | Turns high resolution into real detail rather than an upscale |
| 13 | ~~Avoid the CPU readback~~ | **done** - zero-copy GPU path |
| 14 | ~~IOSurface pixel buffer attributes~~ | **done** - required by the GPU path |
| 15 | Make the frame-rate gate configurable | Currently hardcoded to 60 in `TimeQueue` |

Items 10 and 11 are pure parameter changes with the lowest risk and the largest benefit.

---

## P3 - cleanup

| # | Item |
|---|---|
| 16 | Missing script in the scene: `EchoVision.unity:238,2474` references `HolokitMicrophoneAPI`, actual class is `MicrophoneAPI` |
| 17 | Remaining `CS0618 FindObjectOfType` warnings in the HoloKit SDK (one fixed in `HoloKitDefaultUICanvas`) |
| 18 | `HoloKitARKitNativeProvider.mm:21` leaks 64 bytes per frame (`new float[16]`, never freed) |
| 19 | `iOSPhoneModelList.asset` lists `iPhone14,5` twice ("iPhone 13" and "iPhone 13 Mini"); iPhone 13 mini is actually `iPhone14,4` and is missing |
| 20 | Reorganise native sources (`Runtime/Native/Shared` + `Prebuilt`) and add an Editor script that rebuilds the macOS bundle automatically when sources change |

---

## Files lost during the session
A `git clean` removed several untracked files. `Assets/Editor/EchoVisionBuilder.cs` is obsolete
anyway, since the project's own `iOS.asset` build profile already overrides the scene list and
`unity build --profile iOS` needs no custom script. `Assets/Plugins/iOS/MicFix.mm` and
`Assets/Scripts/MicFix.cs` were abandoned by decision; nothing references them any more.
