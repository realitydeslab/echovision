# Task Plan: update EchoVision to the latest committed HoloKit SDK (2026-10-03)

## Goal
EchoVision on `upgrade/unity-6.3-ios27` builds and passes its checks against HoloKit SDK commit
`60be94a` (2026-09-30), pinned so the build no longer follows the SDK working tree.

## Decisions
- SDK target = latest **committed** `60be94a`. The SDK working tree has 10 modified + several
  untracked files edited 2026-10-03 23:02-23:38 (another session, in progress). Not included, not touched.
- Pin with a Git dependency on the local repo (`git+file://...#60be94a`) instead of `file:../../holokit-unity-sdk`.
  Fallback if UPM rejects it: a separate git worktree at `60be94a`.
- Previous tested SDK state: `9cc462c` (2026-09-24 00:51). Delta = 18 commits.

## "No errors" means
1. Unity CLI import: 0 compile errors.
2. EditMode tests: all pass (EchoVision + SDK testables).
3. iOS: Unity export + `xcodebuild` succeed (Release).
4. Device (only if the user approves a phone): install, launch, walker `failures=0`.

## Phases
- [x] P1 Map the 18-commit SDK delta onto EchoVision's SDK usage (API renames, serialized fields, build validation rules)
- [x] P2 Pin the dependency (`git+file://...#60be94a`, lock `source: git`), import headless: 0 compile errors
- [x] P3 EditMode: 86/86 passed (EchoVision + SDK testables)
- [x] P4 iOS export + xcodebuild (generic iOS): Release and Recovery Tests both `BUILD SUCCEEDED`
- [x] P5 Device check on iPhone 15 A (approved): walker failures=0 (88/88) with SDK 7ef2463 content
- [x] P6 Final: EditMode 87/87 with the `7ef2463` pin (lock source git); Release export + xcodebuild `BUILD SUCCEEDED`; installed on iPhone 15 A 15:27

## Findings
- API compatible: `RecordIn4K` and `DeviceData.SupportLiDAR()` kept as aliases; `_recordIn4K` has
  `FormerlySerializedAs`; enums moved to their own files in the same namespace.
- **Build blocker:** new `HoloKitProjectValidation` iOS rule (Error) fails the build when
  "Adjust iOS FPS based on thermal state" is on with Serious < 60 fps. EchoVision had it on (Serious 30).
  Fixed: `adjustIOSFPSUsingThermalState: 0` (the SDK's own FixIt).
- Behaviour changes (no error, but visible) in Stereo mode:
  - Stereo frame rate: SDK sets `Application.targetFrameRate` to the display maximum (120 Hz on
    iPhone 15 Pro), 40 at Serious, 30 at Critical, and turns off ARSession Match Frame Rate.
    EchoVision's `FrameRateGovernor` caps at 60 to limit heat; Mono keeps 60.
  - Stereo bar (MORE / RECORD / MONO / OPTIONS / TRIGGER) is spawned by default
    (`m_StereoBarItems` not serialized in the scene -> All). Alignment guide stays off (scene sets 0).
  - `PowerSaver` sets 5 fps while asleep; the SDK re-applies the Stereo rate when the thermal
    state changes, which can override the sleep rate in Stereo.
- `LowLatencyTrackingManager` is in `EchoVision.unity`; it now uses Kalman pose prediction.

## User decisions (2026-10-04 00:xx)
- Stereo frame-rate cap 60, not 120 ("120 is too much"). Applied in EchoVision's scene
  (`m_StereoFrameRate: 60`). The SDK default itself was NOT changed: `HoloKitCameraManager.cs`
  has uncommitted edits from another session in the SDK working tree.
- Show the SDK stereo bar. Applied as `m_StereoBarItems: 27` (MONO, RECORD, MORE, OPTIONS);
  TRIGGER hidden because nothing in EchoVision handles `OnStereoTrigger`.
- Device tests on "Holo iPhone 15 A"; reinstall the new Release afterwards.

## Changes
- `Packages/manifest.json`, `packages-lock.json`: SDK pinned to `7ef2463` (git source) = `60be94a` + mic-permission fix.
- `ProjectSettings/ProjectSettings.asset`: `adjustIOSFPSUsingThermalState: 0` (SDK validation rule).
- `Assets/Scenes/EchoVision.unity`: Stereo frame rate 60, stereo bar items 27.
- `Assets/Scripts/PowerSaver.cs`: keeps 5 fps while asleep even if the SDK re-applies the Stereo
  rate on a thermal change; on wake asks the SDK to re-apply the rate for the current thermal state.
- `Assets/Tests/Editor/PowerSaverTests.cs`: `Asleep_UndoesExternalFrameRateChange`.
- `Assets/Tests/DeviceStateWalk.cs`: checks target fps <= 60 in Stereo and Mono, stereo bar shown
  with controls (`HasControls`); logs fps.
- Review fix (code-reviewer, verified): `SleepModeCover` (last child of the Default UI Canvas) had
  `raycastTarget: 0`, so taps on the black sleep screen reached Switch Render Mode / Record buttons;
  the stereo bar (sortingOrder 32000, own raycaster) drew above the cover and stayed tappable.
  A mode switch while asleep makes the SDK save/restore wrong frame rates (inactive ARSession, 5 fps
  saved as the Mono rate). `PowerSaver.SetObjectListState` now makes the cover block taps and hides
  the stereo bar while asleep, and shows the bar again on wake in Stereo. Test asserts the cover.
- Reviewer's "stray preloadedAssets / XRSimulation deletions" were mid-build transient state; not in
  the diff after the builds finished.

## Verification so far
- EditMode 87/87 (after the first round of changes). Release export: no HoloKit validation warnings/errors.
- Xcode generic iOS: Release `BUILD SUCCEEDED`, Recovery Tests `BUILD SUCCEEDED` (before the review fix).
- Final rerun after the review fix (2026-10-04 12:02): EditMode 87/87; both exports exit 0 with no
  HoloKit validation messages; Release and Recovery Tests xcodebuild `BUILD SUCCEEDED`.
  Apps: `Build_SDK60be94a/DerivedData/Build/Products/Release-iphoneos/EchoVision.app`,
  `Build_SDK60be94a_Tests/DerivedData/Build/Products/ReleaseForRunning-iphoneos/EchoVision.app`.

## Device run 1 (iPhone 15 A, iOS 27, 2026-10-04 14:00, before the authorization fix)
- Walker `[STATE] COMPLETE failures=0`, 82 PASS / 0 FAIL. Stereo `fps=60`; Mono 60 idle, 30 while
  recording with the 4K camera; pose error 0.00 deg in every state; FOV Unity == ARKit; no exceptions.
- After the walker someone used the phone by hand: real sounds triggered `[AUDIO-TRIGGER] EMIT` in all
  four combinations (Mono/Stereo x recording off/on: 22/17/9/6). This settles I2 under `Default`
  microphone processing.
- **SDK bug:** recording from the stereo bar logged "No microphone permission yet; recording game audio
  only" and wrote `sources=1` (no microphone), although `[Microphone] Native permission=1`.
  `HoloKitStereoBar.Record.cs:123` checks `Application.HasUserAuthorization(Microphone)`, which stays
  false until Unity itself requests it; the SDK's native capture permission does not count.
  EchoVision workaround: `MicrophoneAPI.Start` calls `Application.RequestUserAuthorization(Microphone)`.
  Proper fix belongs in the SDK (use the native capture's permission state). Not changed there:
  the stereo bar files are in another session's uncommitted work.
- Stereo-bar take: 2560x1440, 484 submitted / 4 dropped (walker takes: 0 dropped).
- My 240 s console timeout terminated the app (signal 15) during that manual use.

## Device run 2 and the SDK microphone-permission fix
- Run 2 (Unity `RequestUserAuthorization` workaround): new check failed, `failures=1`. Cause verified in
  the exported Xcode project: `Classes/Preprocessor.h` has `UNITY_USES_MICROPHONE 0` because no script
  uses Unity's `Microphone`; `Classes/Unity/AVCapture.mm` then compiles the permission query and request
  out, so `HasUserAuthorization(Microphone)` is always false. Workaround and its check reverted.
- Affects every SDK app that records only through the SDK's native microphone.
- User decision: commit the fix directly to the SDK branch `feature/echovision-port`, only the 2 files.
- SDK patch: `HoloKitMicrophoneCapture.HasPermission` (native `HoloKit_MicrophonePermission() == 1`,
  false in the Editor); `HoloKitStereoBar.Record.cs` uses it. Both files had no uncommitted edits.
- Verification before committing: scratch export of `60be94a` + patch, EchoVision temporarily on `file:`
  that copy; walker adds a stereo-bar RECORD step (reflection on `ToggleRecording`) that checks the take
  logs `sources=2` and no "No microphone permission".
- iPhone 15 A had the new Release (11:58 build) installed after run 2.

## Device run 3 (patched SDK) and the SDK commit
- Run 3 (scratch `60be94a` + patch): EditMode 87/87; walker `COMPLETE failures=0`, 88 PASS. Stereo-bar
  take: `sources=2`, 91 frames, 0 dropped. `device_artifacts.py`: 6 movies decoded, frames == submitted,
  0 dropped, every movie has a 48 kHz mono (microphone) track, A/V end delta <= 0.037 s, `errors: []`.
- `device_artifacts.py` / `Tools/Tests/README.md`: walker now records six movies.
- SDK commit **`7ef2463`** on `feature/echovision-port` (2 files only; `git diff 60be94a 7ef2463` equals
  the tested patch; the other session's uncommitted work untouched). SDK not pushed.
- EchoVision pinned to `7ef2463`. Final Release: `Build_SDK7ef2463/`.

## Errors Encountered
- `unity build ... -- -logFile x`: CLI rejects extra args after `--` for `build` (exit 2). Rerun without them.
- Python text-mode edit converted PowerSaver.cs's mixed CRLF/LF line endings; reverted and redone byte-wise.
- iPhone 15 A went `unavailable` (not on USB) before install; xcodebuild found no destination.

## Status
Done 2026-10-04 15:27. iPhone 15 A runs the final Release (`Build_SDK7ef2463`). EchoVision changes uncommitted; SDK `7ef2463` committed, not pushed.
