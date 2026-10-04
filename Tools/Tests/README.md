# Recovery checks

Recorder stop, memory, and background regression probes live in the SDK. See the
[SDK recorder regression guide](../../../holokit-unity-sdk/Tests/Recorder/README.md)
for build defines and reusable device runners. EchoVision keeps only its scene
and mask-sleep adapter in `Assets/Tests/EchoVisionRecorderTestAdapter.cs`.

Use Unity CLI with Unity 6000.3.24f1. The shipping profile is `iOS`; the automatic device walker exists only in the `iOS Recovery Tests` development profile.

```sh
unity test . --mode EditMode --output /tmp/echovision-editmode.xml --editor-version 6000.3.24f1 -- -buildTarget iOS
python3 ../holokit-unity-sdk/Tests/Recorder/Tools/native_recorder.py /tmp/echovision-native-tests
unity build . --profile 'iOS Recovery Tests' --output-path Build_RecoveryTests --editor-version 6000.3.24f1 --allow-dirty-build
```

For manual inspection of the recovery build, copy a one-shot `Documents/recorder-test-mode.txt` containing `manual` into the app container before launch. This suppresses the automatic orientation walker; the application stays under normal user control.

Build/sign the exported Xcode project for a connected iPhone, install it, and launch with `xcrun devicectl device process launch --device <UDID> --terminate-existing --console org.realitydeslab.echovision`, redirecting console output to a file. The walker starts automatically and reports `[STATE] COMPLETE failures=0`. It requests four interface orientations, records six GPU movies (the last through the stereo bar's RECORD, which must include the microphone), verifies rendered pose/4K state, the 60 fps cap and the stereo bar, and exercises mode changes during recording. iPhone may reject upside-down portrait; check the requested/effective log rather than counting it as coverage.

```sh
python3 Tools/Tests/device_artifacts.py /path/to/device.log /path/to/artifacts --device <UDID>
```

This exports movies/screenshots, decodes every video frame, compares decoded/submitted frame counts and checks audio/video end times. Visual registration still requires inspection. For an optional persistent LiDAR mesh overlay, copy `AlignmentProbe/Assets/` into the project `Assets/` for the test build and remove those copied assets before shipping.

The walker also logs raw microphone analysis and actual `SoundWaveEmitter` index transitions. Physical audio test: play a 440 Hz tone in two-second on/off intervals from a nearby speaker; check approximately 440 Hz analysis, `[AUDIO-TRIGGER] EMIT` in Mono/Stereo with recording off/on, and visible waves in the recorded movie. Do not lower the production threshold to manufacture a pass. Restore any temporary speaker-volume change.

See `plan/recovery-2026-09-21.md` and ignored `Build_RecoveryArtifacts/` for measured results and preserved source backups. Native CPU submission tests do not replace actual Metal GPU or microphone tests.

Human-stencil and command-forced sleep diagnostics: see `HumanStencilProbe/README.md`. The full EditMode suite includes GPU packing, sleep lifecycle and camera/stencil transform regressions.
