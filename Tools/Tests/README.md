# Recovery checks

Use Unity CLI with Unity 6000.3.24f1. The shipping profile is `iOS`; the automatic device walker exists only in the `iOS Recovery Tests` development profile.

```sh
unity test . --mode EditMode --filter 'RecordingStateTests;RenderModeStateTests' --output /tmp/echovision-editmode.xml --editor-version 6000.3.24f1 -- -buildTarget iOS
python3 Tools/Tests/native_recorder.py /tmp/echovision-native-tests
unity build . --profile 'iOS Recovery Tests' --output-path Build_RecoveryTests --editor-version 6000.3.24f1 --allow-dirty-build
```

Build/sign the exported Xcode project for a connected iPhone, install it, and launch with `xcrun devicectl device process launch --device <UDID> --terminate-existing --console org.realitydeslab.echovision`, redirecting console output to a file. The walker starts automatically and reports `[STATE] COMPLETE failures=0`. It requests four interface orientations, records five GPU movies, verifies rendered pose/4K state and exercises mode changes during recording. iPhone may reject upside-down portrait; check the requested/effective log rather than counting it as coverage.

```sh
python3 Tools/Tests/device_artifacts.py /path/to/device.log /path/to/artifacts --device <UDID>
```

This exports movies/screenshots, decodes every video frame, compares decoded/submitted frame counts and checks audio/video end times. Visual registration still requires inspection. For an optional persistent LiDAR mesh overlay, copy `AlignmentProbe/Assets/` into the project `Assets/` for the test build and remove those copied assets before shipping.

The walker also logs raw microphone analysis and actual `SoundWaveEmitter` index transitions. Physical audio test: play a 440 Hz tone in two-second on/off intervals from a nearby speaker; check approximately 440 Hz analysis, `[AUDIO-TRIGGER] EMIT` in Mono/Stereo with recording off/on, and visible waves in the recorded movie. Do not lower the production threshold to manufacture a pass. Restore any temporary speaker-volume change.

See `plan/recovery-2026-09-21.md` and ignored `Build_RecoveryArtifacts/` for measured results and preserved source backups. Native CPU submission tests do not replace actual Metal GPU or microphone tests.
