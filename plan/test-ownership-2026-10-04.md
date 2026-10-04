# Test ownership: HoloKit SDK vs EchoVision (2026-10-04)

SDK state read: `holokit-unity-sdk` `feature/echovision-port` = `main` = `60be94a` (2026-09-30),
plus uncommitted work from 2026-10-03 (hand skeleton, stereo bar, Stereo 60 fps, iOS 27
per-frame `Screen.orientation` fix, hand joints in XR Origin space). EchoVision pins `60be94a`.

Rule: a test belongs to the SDK when it needs no EchoVision script or scene. EchoVision keeps
tests of its own scripts and of the SDK *inside a real app scene*.

## SDK-owned (run in a carrier project; the boilerplate is the default carrier)

| Layer | Tests |
| --- | --- |
| Host EditMode | `Tests/Camera` (ARKitCameraCompatibility), `Tests/Hands` (gesture classifier/filter, HandSkeleton - uncommitted), `Tests/Recorder` (RecordingState: stop drain, pacing, aspect, 4K restore, output size), `Tests/Tracking` (LowLatency), `Tests/UI` (StereoBar), `Tests/Validation` (project validation rules) |
| Host native / scripts | `Tracking/Native~/tracking_core_tests.cpp` (Swift parity fixtures), `Hands/Native~/hand_depth_tests.cpp`, `Recorder/Native~/audio_buffer.cpp`, `Recorder/Tools/native_recorder.py`, `test_recorder_*.py`, `Hands/Tools~` offline gesture eval |
| Device | Feature probe (`HoloKitProbeBuild`), 16 suites on the sample scenes: device, rendering, orientation, ui, tracking, enhancement, microphone, hand, handmotion, handvideo, coexist, gesture, stereobar, lowlatency, pinchtoplace, gaze. Recorder probes (`RecorderProbeBuild`): stop, background, memory, audio, audio-long, audio-external(-reference) |

A UPM package cannot run tests by itself. Carriers today: `holokit-unity-sdk-boilerplate`
(`file:../../holokit-unity-sdk`, so it tests the working tree; last commit 2026-09-23) and
EchoVision (`testables`, pinned `60be94a`).

## EchoVision-owned (stay)

| Test | Covers |
| --- | --- |
| `Assets/Tests/Editor/PowerSaverTests.cs` | `PowerSaver` sleep/wake, rest-orientation threshold |
| `Assets/Tests/Editor/MeshGpuPackingTests.cs` | `MeshVFX` GPU point packing |
| `Assets/Tests/Editor/HumanStencilTransformTests.cs` | `DepthImageProcessor` stencil transform |
| `Assets/Tests/EchoVisionRecorderTestAdapter.cs` | Hooks the SDK recorder probes into EchoVision's scene (MicrophoneAPI, AudioProcessor, PowerSaver) - the intended pattern |
| `Assets/Tests/DeviceStateWalk.cs` (EchoVision part) | Real scene under mode/orientation/recording changes; `[AUDIO-TRIGGER]` sound-wave emission |
| `Tools/Tests/HumanStencilProbe` | Stencil overlay, command-forced sleep/wake, MeshVFX, profiling |
| `Tools/Tests/device_artifacts.py` | Collects and decodes the walker's movies/screenshots |
| Manual/physical | I2 sound trigger (440 Hz), I6 Release build smoke |

## Misplaced or duplicated

1. `Assets/Tests/Editor/RenderModeStateTests.cs` uses only `HoloKitCameraManager` and
   `HoloKitVideoRecorder` - no EchoVision code - through reflection on 8 SDK private members
   (`m_MonoCamera`, `m_LeftEyeCamera`, `m_RightEyeCamera`, `m_BlackCamera`, `m_CenterEyePose`,
   `m_ARCameraBackground`, `m_ShowAlignmentMarkerInStereoMode`, recorder `_camera` /
   `_background` / `_cameraManager` / `ApplyRenderMode`). An SDK rename breaks EchoVision at
   test runtime, not compile time. **Move to SDK `Tests/Recorder/EditMode`.**
2. `DeviceStateWalk` SDK-level assertions duplicate the probe: four orientations (`orientation`),
   4K input 3840x2160 (`enhancement`), virtual-camera FOV vs ARKit through the SDK's native
   `HoloKit_GetARCameraReference` (`[SDK-FOV]` in the probe), recording across mode switches
   (`stereobar` takes). Keep the walker as an integration smoke; the SDK probe is the authority.
3. `Tools/Tests/AlignmentProbe` (LiDAR-mesh AR alignment overlay) is app-independent; candidate
   for the SDK probe's `orientation` suite. Low priority.
4. EchoVision `testables: io.holokit.unity-sdk` re-runs every SDK test in EchoVision. Useful as an
   upgrade gate only; the SDK's own gate should be the boilerplate run.
