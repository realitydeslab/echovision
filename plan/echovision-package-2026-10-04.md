# Task Plan: move EchoVision content into `Packages/org.realitydeslab.echovision`

## Goal
All EchoVision content (scripts, scenes, art, VFX, audio, tests, test tools) lives in an embedded
UPM package `Packages/org.realitydeslab.echovision`; the project compiles, EditMode tests pass with
the same count as before, and the iOS + iOS Recovery Tests profiles export.

## Scope
Move (with `.meta`, so every GUID is kept):
- `Assets/Scripts` -> `Runtime/` (asmdef `EchoVision`)
- `Assets/Scenes`, `Materials`, `Models`, `Images`, `VFX`, `AudioGroup`, `Settings/PostProcessing_EchoVision.asset`
- `Assets/Tests/Editor` -> `Tests/Editor/` (asmdef `EchoVision.Editor.Tests`)
- `Assets/Tests/*.cs` -> `Tests/Runtime/` (asmdef `EchoVision.Tests`, define `ECHOVISION_DEVICETEST`, `AlwaysLinkAssembly`)
- `Tools/Tests` -> `Tests/Tools~/` (Unity ignores `~` folders; same convention as the SDK)

Stay in the project (Unity project configuration, not app content):
- `Assets/Settings/Build Profiles` (Unity keeps build profiles under Assets), URP pipeline/renderer/global
  settings, `DefaultVolumeProfile.asset`, `Assets/XR` (XR loader settings), `Assets/TextMesh Pro`
  (TMP Essentials), `ProjectSettings/`, `Tools/visionos-snapshot`, `Tools/VISIONOS.md`, `plan/`.

## References to update
- Build profiles `m_path` (iOS, iOS Recovery Tests, visionOS) and `Tests/Tools~/HumanStencilProbe` copy
- `MeshGpuPackingTests` compute-shader path; `Type.GetType("X, Assembly-CSharp")` in 3 tests
- `Assets/XR/UserSimulationSettings/SimulationEnvironmentAssetsManager.asset` prefab path
- `Tests/Tools~/README.md`, `HumanStencilProbe/README.md` paths

## Phases
- [x] P1 Baseline EditMode run: 87/87 passed (EchoVision 14, SDK 73), no missing scripts
- [x] P2 Create package.json + asmdefs; `git mv` content
- [x] P3 Update path / assembly-name references; `testables` += org.realitydeslab.echovision
- [x] P4 Headless import: 0 compile errors, no missing scripts, no GUID conflicts; lock has
      `org.realitydeslab.echovision` `source: embedded`
- [x] P5 EditMode: 87/87, identical per-class breakdown to the baseline. Note: another session
      moved the SDK pin to `7ef2463` (2026-10-04 15:23) before both runs; both runs used it.
- [x] P6 iOS export: EchoVision + HoloKit only, scene from package; xcodebuild Release (unsigned) BUILD SUCCEEDED.
      iOS Recovery Tests export: EchoVision.DeviceTests linked; DeviceStateWalk.Install and
      EchoVisionRecorderTestAdapter.Install registered in RuntimeInitializeOnLoads.json
- [x] P7 Report
- [x] P8 Committed: c4b7a6b (other session's SDK 7ef2463 work, old paths) + 5a3e572 (this refactor)
- [x] P9 Device, Holo iPhone 15 A (iOS 27), 2026-10-04 16:31, build from 5a3e572 content:
      walker `[STATE] COMPLETE failures=0`, 88 PASS (same as the pre-refactor run 3);
      device_artifacts.py: 6 movies, 0 dropped, all with a 48 kHz mic track, errors [];
      A/V end delta max 0.161 s (gate 0.2 s; run 3 max 0.037 s).
      Hand use after the walker: sound-wave EMIT Mono/Stereo x recording off/on = 11/8/14/14.
      Artifacts: Build_RecoveryArtifacts/pkg-refactor-2026-10-04/ (ignored).
- [x] P10 16:42 Release 0.3.0 (from 5a3e572) installed on iPhone 15 A; launch: Build type 'Release',
      no walker, no exceptions, microphone permission granted. The walker capture's 600 s timeout
      SIGTERMed the test app at 16:41; it had been in the background since 16:34 (no one using it).

## Risks
- IL2CPP strips an assembly no scene references -> `AlwaysLinkAssembly` on the device-test assembly.
- Scene UnityEvents store `Type, Assembly-CSharp`; the two in `EchoVision.unity` already name a
  type that no longer exists (`HolokitMicrophoneAPI`), so Unity resolves them from the target object. Unchanged.
- Another session (`echovision-b1`) left uncommitted edits in 9 files; `git mv` keeps them.

## Errors Encountered
(none yet)

## Status
Done. iPhone 15 A runs the Release build from 5a3e572.
