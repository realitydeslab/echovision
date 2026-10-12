# Recovery checks

Recorder stop, memory, and background regression probes live in the SDK. See the
[SDK recorder regression guide](../../../../../holokit-unity-sdk/Tests/Recorder/README.md)
for build defines and reusable device runners. EchoVision keeps only its scene
and mask-sleep adapter in `Tests/Runtime/EchoVisionRecorderTestAdapter.cs` of the
`org.realitydeslab.echovision` package. Run the commands below from the project root.

Use Unity CLI with Unity 6000.3.24f1. The shipping profile is `iOS`; the automatic device walker exists only in the `iOS Recovery Tests` development profile.

```sh
unity test . --mode EditMode --output /tmp/echovision-editmode.xml --editor-version 6000.3.24f1 -- -buildTarget iOS
python3 ../holokit-unity-sdk/Tests/Recorder/Tools/native_recorder.py /tmp/echovision-native-tests
unity build . --profile 'iOS Recovery Tests' --output-path Build_RecoveryTests --editor-version 6000.3.24f1 --allow-dirty-build
```

For manual inspection of the recovery build, copy a one-shot `Documents/recorder-test-mode.txt` containing `manual` into the app container before launch. This suppresses the automatic orientation walker; the application stays under normal user control.

Build/sign the exported Xcode project for a connected iPhone, install it, and launch with `xcrun devicectl device process launch --device <UDID> --terminate-existing --console org.realitydeslab.echovision`, redirecting console output to a file. The walker starts automatically and reports `[STATE] COMPLETE failures=0`. It checks that the upside-down orientations are not allowed (turning the phone upside down puts EchoVision to sleep), walks Portrait and LandscapeLeft, records four GPU movies (HD60 by default; the last is a 4K30 take through the on-screen quality and REC buttons in Stereo, which must include the microphone), verifies the camera format of every take (HD 1920x1440 or 4K 3840x2160) and the HD60 / 4K30 button label, rendered pose, the 60 fps cap, that Stereo shows EchoVision's own Mono button and no SDK stereo bar, and exercises mode changes during recording. PortraitUpsideDown and LandscapeRight are not declared, so the walker never requests them.

```sh
python3 "Packages/org.realitydeslab.echovision/Tests/Tools~/device_artifacts.py" /path/to/device.log /path/to/artifacts --device <UDID>
```

This exports movies/screenshots, decodes every video frame, compares decoded/submitted frame counts, checks each movie's measured frame rate against the recorder's announced fps (at least 90 %) and checks audio/video end times. Visual registration still requires inspection.

**Recording orientation and voice.** The walker draws a 2x2 marker (red | green over blue | yellow) at the centre of the recorded camera, logs `[STATE] VOICE-CUE take=N` when each take starts and `[STATE] TAKE-FRAME take=N t=...` at each stable state inside a take. `device_artifacts.py` decodes the movie frame at every TAKE-FRAME time and requires the marker to read upright (not rotated or mirrored), square (aspect 1 +/- 0.12) and centred (+/- 0.1), also under the bloom of EchoVision's sound waves, including the take that switches Stereo -> portrait -> landscape while recording. With `--voice-reference` it also requires the phrase to be found in each movie's microphone track (`recording_checks.voice_match`, score >= 0.5). `run_walker.py` does the whole run: it launches the walker, with `--voice` repeats the phrase through the Mac speakers from the walker's START to COMPLETE so every take holds a whole phrase (never changes the volume; refuses to run while the output is muted; keep the phone still and near the speakers), stops at COMPLETE (which terminates the app) and runs `device_artifacts.py`:

```sh
python3 "Packages/org.realitydeslab.echovision/Tests/Tools~/run_walker.py" --device <UDID> --output Build_RecoveryArtifacts/<run> --voice
```

Host tests for the checks (no phone): `cd "Packages/org.realitydeslab.echovision/Tests/Tools~" && python3 -m unittest test_recording_checks`. For an optional persistent LiDAR mesh overlay, copy `AlignmentProbe/Assets/` into the project `Assets/` for the test build and remove those copied assets before shipping.

The walker also logs raw microphone analysis and actual `SoundWaveEmitter` index transitions. Physical audio test: play a 440 Hz tone in two-second on/off intervals from a nearby speaker; check approximately 440 Hz analysis, `[AUDIO-TRIGGER] EMIT` in Mono/Stereo with recording off/on, and visible waves in the recorded movie. Do not lower the production threshold to manufacture a pass. Restore any temporary speaker-volume change.

See `plan/recovery-2026-09-21.md` and ignored `Build_RecoveryArtifacts/` for measured results and preserved source backups. Native CPU submission tests do not replace actual Metal GPU or microphone tests.

Human-stencil and command-forced sleep diagnostics: see `HumanStencilProbe/README.md`. The full EditMode suite includes GPU packing, sleep lifecycle and camera/stencil transform regressions.
