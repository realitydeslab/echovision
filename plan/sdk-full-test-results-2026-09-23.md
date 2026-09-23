# HoloKit SDK 0.6.0 port — test results (2026-09-23)

Devices: iPhone 16 Pro `00008140-…` (iOS 26, early runs) until it disconnected; final runs on iPhone 15 Pro "I"
`00008130-000848EA…` (iOS 27, sees Safari at an angle). The user accepted iOS 27 coverage as sufficient.
Unity 6000.3.24f1. Artifacts: `Build_RecoveryArtifacts/sdk-2026-09-23/` (ignored).
Status: **near final**. Non-audio device tests complete on iOS 27; audio stimulus tests approved and in progress.

## Part M — migration

| Item | Result | Evidence |
| --- | --- | --- |
| M1 branch | done | `holokit-unity-sdk` `feature/echovision-port`; `main` untouched |
| M2 port | done | fork snapshot `dc7efb3` (branch `echovision-fork`) merged as `08362da`; only header conflicts plus phone list |
| Phone list | done | union of fork + upstream; upstream's duplicate `iPhone14,5` "iPhone 13 Mini" entry became `iPhone14,4` |
| M3 version | done | 0.6.0, AR Foundation/ARKit 6.4.3, **minimum Unity 6000.3 LTS** (user decision), CHANGELOG |
| M4 EchoVision | done | embedded package removed; `file:../../holokit-unity-sdk`; lock `source: local`; backup `embedded-sdk-backup.tgz` |
| M5 no fork dependency | done | code diff vs embedded = SPDX headers only; EchoVision compiles and tests against upstream |

## Part BP — boilerplate

| Item | Result | Evidence |
| --- | --- | --- |
| BP1 | done | branch `feature/sdk-0.6-tests`; Unity 6000.3.24f1; local SDK; ARF 6.4.3; `testables` |
| BP2 | done | `Samples/RecordingAudio`: RecordingEnvironment (raw mic) and RecordingGameAndMicrophone (AEC + game tone) |
| BP2 fix | done | Gaze Gesture Interaction sample lacked `GazeRaycastInteractor`/`GazeGestureInteractor` (never selected); added in boilerplate and SDK `Samples~` |
| BP3 | done | `HoloKitProbeBuild.Build` development player with `HOLOKIT_SDK_TESTS` |

## Tier 0 — host

| ID | Result | Evidence |
| --- | --- | --- |
| T0.1 EditMode | PASS | 39/39 (SDK 26 + EchoVision 13) against the upstream local package |
| T0.2 native | PASS | `native_recorder.py` all cases; `audio_buffer.cpp` exit 0; tool unit tests 8/8 |
| T0.1b boilerplate EditMode | PASS | 26/26 `HoloKit.Recorder.Tests` in the boilerplate against the local SDK |
| T0.5 builds | PASS | EchoVision recovery-test and boilerplate probe players built, signed, installed |

## Tier 1 — SDK features on device (boilerplate probe, run3 unless noted)

| Group | Result | Evidence |
| --- | --- | --- |
| A9 device | PASS | LiDAR true; thermal Normal; native uptime vs realtime 2.001/2.001 s |
| A1–A5 rendering | PASS | profile iPhone17,1 (460 dpi); one event per mode change; viewports symmetric 0.40x0.72; IPD 0.0640 m; center-eye offset applied/cleared |
| A4 orientation | PASS | pose-stable (<1 mm); LandscapeLeft -97°, LandscapeRight +97° vs Portrait; upside-down refused by iOS (excluded). run2 had a disturbed frame, no pose data |
| E1 default UI | PASS | Button wired to SwitchRenderMode toggles mode and label, restores (invoked, not physically tapped) |
| A6 tracking | PASS | native frame callback 179/3 s, monotonic, max age 65 ms; Unity frames resume after delegate restore; stereo low-latency 181 frames/3 s |
| A7 world origin | PASS | translate delta (-0.500, 0, 0); rotate 90.0° |
| A8 4K | PASS after fix | run2 FAIL (stuck in 4K after rapid toggle, "RESTORED: 3840x2160"); fixed in native provider; run3 restore 1920x1440 |
| G1 microphone sharing | PASS | ready 48 kHz; shared lease; AEC conflict rejected; idempotent; released |
| G2 440 Hz | UNTESTED | speaker not allowed (pending user decision) |
| B hand tracking | PASS after fix (B4 pending) | run2 crashed (SIGSEGV in processCurrentFrame3D); fixed; run3 B1 1.00, B2 1.00 + finite joints, depth 0.30 m, B3 two hands 1.00, B5 lost events 4; B4 sweep r=0.43 (stimulus looped) — rerun |
| C gesture | FAIL (investigating) | run3: no Pinched/Five events; diagnostics added |
| D gaze | D1 PASS, D2 depends on C | enter/select 238 frames/exit; gaze+pinch 0 |

## Tier 2 — EchoVision

| ID | Result | Evidence |
| --- | --- | --- |
| I1 walker | PASS | `[STATE] COMPLETE failures=0`: 4 orientations, GPU recording, 4K, pose match, audio drain |
| I2 sound trigger | INCONCLUSIVE | Mic hears the Mac tone (RMS p50 0.0003, p90 0.0012, max 0.0014) but ~29 dB below the emit threshold (RMS 0.033); 0 emits. Level problem, not a code failure; threshold not lowered |


## Final iOS 27 runs (iPhone 15 Pro, after all fixes)

| Area | Result | Evidence |
| --- | --- | --- |
| Full probe (all suites) | 63 pass / 2 fail / 1 untested before later fixes | fails were fiducial not evaluable (angled view) and C1 false pinches from the hand-dropout bug (fixed) |
| UI + gesture + gaze | 23/23 | standard prefab: render, 4K, record (3.2 MB movie, 114 frames, 0 dropped, mic fallback); one Pinched/Five per window; gaze+pinch 2 selects for 2 pinches |
| Virtual camera FOV | PASS | Unity == ARKit in portrait (34.66x68.16), landscape (68.16x34.66), 4K (70.65x36.21) and restore; compatibility attached per scene |
| EchoVision walker | failures=0 | with FOV assertion in every state after the portrait fix |
| Hand motion (known sweep) | M1/M2 PASS | 42 pose updates/s; capture-to-pose p50 73 ms, p95 81 ms. Continuity/trajectory limited by angled view |
| Real hand video (Pexels) | V2/V4/V5 PASS | trajectory R2 0.908 (0.906 repeat), pinch distance r 1.00, two-hand 0.62; detection recall 0.38-0.76 (angled view); display-to-pose not isolatable without a photodiode |
| Microphone 440 Hz (G2) | PASS | tone RMS 0.00789 vs silence 0.00095; 440 Hz selectivity 38x; Mac volume unchanged |
| Release build (T0.4) | PASS | Release config, no probe/test assemblies or native test patches; installed and launched |

Additional SDK fixes after the first results: iOS 27 portrait projection, compatibility on every scene,
gesture kept through 0.2 s dropouts, recorder microphone fallback, standard UI prefab + samples.

## Findings

1. Gaze Gesture Interaction sample shipped without interactors — fixed (see BP2 fix).
2. Upstream phone list had two `iPhone14,5` entries — fixed.
3. Probe assembly was stripped by IL2CPP on first build — fixed with `AlwaysLinkAssembly`/`Preserve`.
4. Hand tracking 3D crash (frame race, null depth) — fixed in `AppleVisionHandPoseDetector.m`.
5. 4K restore lost after same-frame enable/disable — fixed in `HoloKitARKitNativeProvider.mm`.
6. Unattended probe runs were suspended by auto-lock; probe now sets `NeverSleep`; runner timeout no longer depends on log output.
7. `native_recorder.py` fails if its output folder holds earlier movies (AVAssetWriter will not overwrite); use a clean folder.
