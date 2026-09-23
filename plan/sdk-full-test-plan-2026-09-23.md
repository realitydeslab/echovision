# HoloKit SDK — port to upstream + full feature test plan

Created 2026-09-23. Status: **draft, awaiting user review**. Nothing below has run yet.

Scope (goal updated 2026-09-23): port EchoVision's customized embedded SDK into
`../holokit-unity-sdk`, add the new test cases to `../holokit-unity-sdk-boilerplate`, switch
EchoVision to the upstream SDK (no embedded fork), then run every test below against that SDK.

## Part M — SDK migration (before testing)

Facts: the embedded fork is based on upstream `e7326129` (0.5.1). Upstream `main` has 4 later
commits (0.5.4→0.5.6, ARFoundation 6, stereo position fix, iPhone 11); runtime changes since the
base are copyright headers plus one `LowLatencyTrackingManager` line. Upstream also has
uncommitted local edits (version 0.6.0, ARFoundation 6.2.0, phone list, README) — kept, not overwritten.

- M1 Branch `feature/echovision-port` in `holokit-unity-sdk`; commit nothing on `main`.
- M2 Apply the fork's changes onto upstream (3-way from `e7326129`): recorder GPU path + audio
  (game tap, shared microphone, AEC, mixed movie), `ARKitCameraCompatibility` + camera reference
  (iOS 27 background fix), `-ld64` removal, gaze-gesture/UI/low-latency edits, `Tests/`,
  `Documentation~/RecordingAudio.md`, recorder Inspector. Keep upstream headers and its
  `LowLatencyTrackingManager` fix. Merge both phone lists (union; keep iPhone17/18/19 entries).
- M3 `package.json`: 0.6.0, ARFoundation/ARKit 6.4.3 (what EchoVision validated), `unity` 6000.0;
  CHANGELOG entry. Drop `.DS_Store` files.
- M4 EchoVision: remove `Packages/io.holokit.unity-sdk`, reference `file:../../holokit-unity-sdk`
  for testing. Switching to a git URL/tag needs a push — asked separately, not done automatically.
- M5 Remove EchoVision references to fork-only code paths, if any remain; build must pass.

## Part BP — boilerplate update

- BP1 Branch in `holokit-unity-sdk-boilerplate`; keep its existing local edits. Upgrade to Unity
  6000.3.24f1 and point it at the local upstream SDK.
- BP2 Add scenes: **Recording — Environment** (raw mic) and **Recording — Game + Mic (AEC)**, plus
  an **SDK Feature Probe** scene (automated B/C/D/A6–A9/G checks below).
- BP3 Enable SDK `testables`; EditMode tests + probe device run in the boilerplate too.

## Operating principle: no human in the loop

- The phone stays on its stand, camera facing the Mac display. The Mac drives all stimuli in
  Safari (`osascript` → Safari URL/JS) and the Mac speakers (`afplay`/`say`).
- The app is driven by a one-shot `Documents/*-test-mode.txt` file (existing pattern) and a new
  test-only **SDK feature probe** that logs `[SDK-TEST] <id> PASS|FAIL <evidence>` lines.
- Screen taps, when required, go through the already-installed XCUITest runner
  (`org.realitydeslab.echovision.devicechecks.xctrunner`) using coordinate taps.
- Background/foreground via `devicectl` (launch another app, relaunch ours) — no hand needed.
- Results: console log + `Documents/*.json` pulled with `devicectl`; movies decoded with ffmpeg.

Device: **iPhone 16 Pro** `00008140-001111563A0B001C` (`iPhone17,1`, iOS 26.0), aimed at Safari by the user.
Previous evidence was iPhone 15 Pro / iOS 27; results are not carried over.

## Stimuli served in Safari (Mac display)

| Stimulus page | Used by |
|---|---|
| Hand photos/video: open palm, fist, 2 hands, pinch/release loop, blank | B, C, D |
| Person/forearm video | I (human stencil) |
| Fiducial / chessboard with orientation arrow | A (background orientation, alignment) |
| `external_stimulus.html` (flash + 1200 Hz pulse, existing) | F (A/V sync) |
| Tone page / `afplay` 440 Hz on-off; `say` for near-end speech | F, G, I |

## Tier 0 — host checks (no device)

| ID | Test | Pass criterion |
|---|---|---|
| T0.1 | Unity CLI EditMode: SDK recorder tests + EchoVision tests (GPU pack, sleep, stencil) | all pass (last known 39/39) |
| T0.2 | Native writer harness `native_recorder.py`, `audio_buffer.cpp`, tool unit tests | all cases pass |
| T0.3 | Import all 9 `Samples~` into a throwaway copy; compile for iOS | 0 compile errors |
| T0.4 | Production `iOS` profile export → xcodebuild → link | builds; no test probes/defines in Release |
| T0.5 | `iOS Recovery Tests` + new SDK-probe profile export → build → install | installs on device |

## Tier 1 — SDK features on device (automated)

**A. Rendering / camera (`HoloKitCameraManager`, `PhoneModelList`, `ARKitCameraCompatibility`)**
- A1 Phone model lookup returns `iPhone17,1` specs on the iPhone 16 Pro; default fallback for an unknown model.
- A2 Mono ↔ Stereo via API and `SwitchRenderMode()`; `OnScreenRenderModeChanged` fires once per change.
- A3 Stereo viewports/projections match values computed from phone + HoloKit specs; screenshot shows two eye viewports and alignment marker at expected pixel.
- A4 AR background orientation in all orientations (iOS 27 custom background material): fiducial arrow in screenshot points the right way. PortraitUpsideDown reported as unsupported if iOS rejects it.
- A5 `CenterEyePose` / eye offsets consistent with camera pose.

**A-bis. Tracking and session**
- A6 `LowLatencyTrackingManager`: `OnARSessionUpdatedFrame` rate ≈ camera rate, timestamps monotonic and aligned with `GetSystemUptime()`.
- A7 `WorldOriginResetter.ResetWorldOrigin` to a known pose → camera pose transforms as expected.
- A8 `ARBackgroundVideoEnhancementManager`: 4K/HDR on/off, `ShouldEnhance` rules (stereo, recording), format 1920×1440 ↔ 3840×2160 restore.
- A9 `DeviceData.SupportLiDAR()` = true; `AppleNativeProvider.GetThermalState()` valid; thermal event subscription does not crash.

**B. Hand tracking (`HandTrackingManager`, Vision + LiDAR)**
- B1 Blank page → `HandCount = 0`, `OnHandPoseLost`.
- B2 One hand → `HandCount = 1`, all 21 joints finite, joint depth ≈ measured display distance (LiDAR).
- B3 Two hands → `HandCount = 2`.
- B4 Hand moved left→right on screen → joint X moves monotonically.

**C. Gesture recognition (`HandGestureRecognitionManager`)**
- C1 Pinch/release loop video → `OnHandGestureChanged` Pinched/None alternates at the page's rhythm; no events on blank page.

**D. Gaze interaction**
- D1 `GazeRaycastInteractor`: probe places an interactable on / off the camera ray → Entered, Selected(dwell), Exited in order.
- D2 `GazeGestureInteractor`: gazed object + pinch video → `OnGestureSelected`; no select without pinch.

**E. Default UI (`HoloKitDefaultUICanvas`)**
- E1 Coordinate-tap render-mode and record buttons (XCUITest) → mode toggles, recording starts/stops, green `● REC` label.

**F. Video recorder (`HoloKitVideoRecorder`, GPU path) — existing SDK probe modes**
- F1 `stop`: empty/pending-copy/disabled/destroyed/sleep cases, 20× per stop method and format.
- F2 `memory`: repeated recordings per camera format; report Metal memory trend (known open issue: 307→2066 MiB).
- F3 `background`: background/resume via `devicectl`; movie finalized, second recording OK.
- F4 `audio`: raw mic / game-only / game+mic with AEC bypassed vs enabled; track topology, pulse offsets, echo attenuation.
- F5 `audio-external`: Safari `external_stimulus.html` flash+pulse, 120 s; physical onset offset ≤ 50 ms gate.
- F6 `audio-external-reference`: AVFoundation baseline of the same stimulus for comparison.
- F7 `audio-long`: 610 s takes; drift ≤ 20 ms gate.
- F8 Gains (`GameGain`, `MicrophoneGain`), `CreateMixedSharingCopy` single-track copy, Photos save in a normal build.
- F9 Near-end speech: Mac `say` voice during game playback with AEC on → speech preserved in mic track.

**G. Microphone capture (`HoloKitMicrophoneCapture`)**
- G1 Shared `Acquire/Release`, conflicting processing rejected, `SampleRate` > 0.
- G2 Mac 440 Hz tone → `ReadLatestSamples/ReadSpectrum` peak ≈ 440 Hz; silence baseline low.

## Tier 2 — EchoVision integration on device

- I1 Existing device walker (`iOS Recovery Tests`): 4 orientations, 5 GPU movies, 4K restore, `[STATE] COMPLETE failures=0`; `device_artifacts.py` decode check.
- I2 Sound-wave trigger: 440 Hz 2 s on/off from Mac speaker → `[AUDIO-TRIGGER] EMIT` in Mono/Stereo × recording off/on.
- I3 Human stencil with person/forearm video in Safari; screenshots in portrait + landscape.
- I4 LiDAR mesh VFX GPU path active (mesh samples, no CPU upload).
- I5 Command-forced sleep/wake cycles (with and without recording): AR/mic stop, 5 fps, wake at 60 fps.
- I6 Release build: install, launch, diagnostics absent, visual screenshot.

## Not covered without a person (reported as untested, not passed)

- Optical alignment through the physical HoloKit headset lenses; real mounted-mask rest orientation.
- Real hand (vs. on-screen hand) latency; battery watts; Android; visionOS target; phone models other than the iPhone 16 Pro.

## Execution order

M → BP → T0 (upstream SDK, EchoVision, boilerplate) → build probe → A → B/C/D → G → F (short) → I → F5–F7 (long) → report.
No push, tag, TestFlight or App Store action without explicit approval.
Results go to `plan/sdk-full-test-results-2026-09-23.md`; artifacts under ignored `Build_RecoveryArtifacts/sdk-2026-09-23/`.
