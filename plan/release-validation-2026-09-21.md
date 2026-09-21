# EchoVision release validation — 2026-09-21

Source baseline: `9fa39f1` (2025-05-04). Current functional commits: `8205fb1` (GPU MeshVFX and real AR sleep), `1169500` (human-stencil registration). Unity 6000.3.24f1, AR Foundation/ARKit 6.4.3, Xcode/iOS SDK 27. Validation device: iPhone15Pro, iOS27. The requested inherited iPhone17/18-family parameters remain enabled; this report does not claim physical testing on those models.

## Changes retained

GPU recording and automatic 4K camera input; absolute native ARKit pose/background correction for iOS27; actual microphone analysis and audio format/timestamp fixes; green `● REC` and black `Start Recording`; HoloKit-prefixed camera bridge. macOS Unity bundle/build script removed. CPU native recorder tests use an external temporary host library.

MeshVFX now reads raw GPU mesh buffers and packs point/normal GraphicsBuffers in a compute shader. CPU does metadata/selection/dispatch only, with no CPU vertex-array extraction or upload fallback. The old every-frame mesh copies are gone. PowerSaver retains its original direction/timers, adds ARSession shutdown and 5 fps sleep, and correctly resumes microphone/rendering.

Stencil now uses the full transform actually used by the background. The old picture-in-picture code discarded crop/scale and used a different orientation transform, producing the red/blue double-hand mismatch. It is removed. Shader/VFX transforms refresh after the camera correction; first-frame material readiness is guarded. See `human-stencil-2026-09-21.md` for screenshots and evidence boundaries.

## Automated and actual-device results

- 27/27 EditMode tests passed: recording/mode state, GPU compute output/normal transforms/bounds, sleep lifecycle/timers, and stencil rotation/crop/first-frame behavior.
- Final device state walker: `COMPLETE failures=0`; Mono and Stereo, idle/recording, portrait and both landscapes, and mode/orientation changes during recording. Native rendered pose errors logged as 0 degrees. The iPhone rejects PortraitUpsideDown; effective orientation remains Portrait and is explicitly logged.
- Five final GPU movies: 91, 93, 92, 93, 271 decoded frames; zero drops, no decode errors. AAC stereo at 24 kHz. Audio/video end-time differences 35–118 ms.
- Physical speaker -> iPhone microphone -> sound-wave emitter: 27 actual emissions across all four combinations (Mono idle 13, Mono recording 9, Stereo idle 3, Stereo recording 2). Pitch analysis about 441 Hz; each saved movie has a 439.93–440.14 Hz audio peak. Visible purple waves confirmed in extracted video frames. Production threshold unchanged; Mac system volume was not changed in this final run.
- Two sets of four fast sleep/wake cycles passed. The latest set additionally checks production stencil and VFX matrices against background (error 0), and GPU mesh samples against reference (error 0). Recordings finalized before shutdown; no microphone or ARSession work while asleep.
- Real timer rerun passed: 30-second entry, recording finalization, fade then 5 fps/ARSession and microphone off; wake and 60 fps restoration. Probe temporarily matched idle direction to the stationary phone. Production LandscapeRight and original 30/2-second timing remain unchanged; no mounted-mask inversion test is claimed.
- 4K input transition: 1920x1440 -> 3840x2160 -> 1920x1440. In one landscape measurement, horizontal projection FOV changed 71.25 -> 73.79 -> 71.25 degrees. This comes with the capture format/intrinsics change, not just file resolution. Do not apply a compensating visual zoom that breaks AR registration. Saved movie resolution follows the capture target and is not promised to be 3840x2160.

## Performance and evidence limits

See `performance-2026-09-21.md`: Time Profiler, Metal, thermal traces and a completed cumulative component ablation. GPU packing removes the CPU upload hotspot; sleep causes a clear CPU/GPU workload decrease. Charging/power-data limitations prevent a watts or battery-life claim, and the data do not establish LiDAR as the largest heat source. No speculative further quality reduction was made.

The live hand test compared old production/red with full-background/cyan; cyan broadly followed the actual hand. The corrected production transform is verified equal to that reference across orientation/crop and sleep/wake checks. The user removed their hand before the final captures, so there is no fresh after-fix contour-latency benchmark. QuickTime screen recording stopped unexpectedly; only 3.684 seconds / 222 frames were recovered, alongside 18 diagnostic screenshots. The report does not claim a complete gesture video.

Durable ignored evidence: `Build_RecoveryArtifacts/final-regression-20260921/`, `stencil-final-20260921/`, `hand-stencil-20260921/`, `profiling-20260921/`. Earlier checkpoint archive remains in `Build_RecoveryRelease/`.

## Release artifact

Final shipping export is `Build_RecoveryFinal/`; the temporary stencil overlay assets were moved outside Assets before building. Neither HumanStencilProbe nor DeviceStateWalk appears in the shipping IL2CPP Assembly-CSharp output. Archive `Build_RecoveryFinal/EchoVision.xcarchive` succeeded; deep/strict code-signature verification passed. The Release app was installed and launched on the iPhone15Pro, with no startup exceptions or missing-matrix errors. QuickTime USB preview showed the upright bookshelf, standard recording/Stereo UI and no diagnostic overlay. App was terminated after inspection to cool the device. Version 0.3.0, build 0, minimum iOS26 (existing settings), SDK27; App Store submission validation has not been performed. No remote push or store upload has been performed.
