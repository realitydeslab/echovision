# Performance investigation — in progress

Device: iPhone15Pro/iOS27. Baseline stencil diagnostic build from cleanup `18dfc7f` (before later recorder-label, bridge rename and GPU-mesh changes). Development build, no Deep Profiling. Probe UI/counters add overhead; do not compare these numbers directly with production battery life.

## Captured evidence

`/tmp/echovision-recovery-20260921-163532/stencil-cpu-power.trace`: 45-second Time Profiler + Power Profiler + Thermal State, recorded at 17:45:31 local time. All-process capture was necessary: direct attach by name/PID could not resolve the app in this Instruments session. A full Power Profiler template failed because its Location Energy Model does not support all-process recording; the successful trace uses Time Profiler with the individual Power Profiler/Thermal instruments.

CPU sample weights (aggregate across threads; not wall-clock latency or watts):

| Process/thread | Sampled CPU time |
| --- | ---: |
| EchoVision, all threads | 47,521 ms |
| cameracaptured | 38,468 ms |
| EchoVision main thread | 5,748 ms |
| EchoVision graphics worker | 5,447 ms |

`baseline-unity-cpu.json` resolves UnityFramework addresses against the matching built binary using atos. Graphics-worker `SetComputeBufferData` has 1,755 ms inclusive sample weight; MeshVFX is present in CPU stacks. ARKit/AppleCV3D worker stacks also consume substantial CPU. This does **not** identify LiDAR as the largest power consumer, and overlapping inclusive samples must not be summed.

Charging state was Charging and display brightness 100%. SystemPowerLevel reported 0%/hour throughout, and per-process power-impact rows were empty. These are not usable power measurements. Power reduction cannot be claimed from this capture.

The following ablation run reached Serious thermal state and 30 fps (previously Normal/Fair at 60 fps); it was stopped and the app quit to cool. Treat it as incomplete, thermally confounded evidence. Turning MeshVFX off reduced the sampled GC allocation counter from roughly 166 kB/frame to 2.9 kB/frame while both phases were already at 30 fps. This isolates avoidable allocations, not LiDAR energy.

## Current changes awaiting device comparison

At the user's explicit request, MeshVFX now packs raw GPU vertex buffers directly into VFX GraphicsBuffers with a compute shader; CPU manages mesh selection/metadata only. No CPU vertex/normal arrays or SetData upload fallback. Pack only after geometry changes or 5 cm of head movement; update mesh-origin transform every frame. Per-mesh geometry is transformed into a shared reference space.

23 EditMode checks passed, including two actual GPU compute-output tests (interleaved/separate vertex streams, nonuniform transforms/normals and destination bounds) and four sleep lifecycle/timer checks. Test-only readbacks do not exist in the shipping mesh data path.

Sleep now includes ARSession in the disabled object list, changes target render rate to 5 fps after fade, and restores the awake rate on wake. Microphone focus callbacks no longer restart capture while that component is inactive. Real-device sleep/wake and recordings still need verification.

The next short probe compares component costs, logs FOV/intrinsics around 4K switching, checks GPU-packed vertices against a test-only reference, and exercises the actual 30-second sleep timer. The human-stencil shader remains diagnostic; alignment has not yet been confirmed with a hand/body in view.

## API evidence

- [Unity direct GPU vertex-buffer access](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Mesh.GetVertexBuffer.html): buffers may be recreated after mesh updates, so handles are reacquired for each pack.
- [Apple Power Profiler](https://developer.apple.com/documentation/xcode/measuring-your-app-s-power-use-with-power-profiler): measure thermal/charging context along with power.
- [ARKit 4K video-format selection](https://developer.apple.com/documentation/arkit/arconfiguration/recommendedvideoformatfor4kresolution): switches capture format, not merely the saved movie size. Actual FOV effects must be measured on device.

## GPU and sleep measurements (18:11–18:15 local)

`gpu-mesh-performance.trace`: 35.840485 seconds, Time Profiler + Metal Performance Overview + Thermal State. 2,081 displayed frames; weighted frame interval 16.814 ms (~59.5 fps). GPU Active Time 12.450 ms/frame, CPU Begin to Present 11.039 ms/frame. Aggregate CPU samples: EchoVision 34,855 ms, cameracaptured 28,347 ms; main thread 4,264 ms and graphics worker 2,999 ms. Matching UnityFramework UUID `0BD6E06A-9D0F-3636-8CDC-D746943CEED4` is preserved under `Build_RecoveryArtifacts/gpu-stencil-fix-symbols/`. SetComputeBufferData inclusive sample weight is 113 ms (the old run was 1,755 ms over a longer interval). This is consistent with removal of CPU mesh uploads, but scene geometry, phone movement and diagnostic overhead differ, so it is not a controlled whole-app speedup or energy reduction result.

`sleep-performance.trace`: 15-second capture while forced asleep. ARSession was stopped, microphone stopped and target frame rate 5. Aggregate EchoVision CPU samples 569 ms; cameracaptured no longer appeared among significant CPU users. Frame interval 200.041 ms, GPU Active Time 1.322 ms/frame, CPU Begin to Present 1.622 ms/frame. This verifies a real workload reduction, not merely a black overlay. It does not measure watts.

The first stencil-fix probe exposed 17 startup matrix-read errors before the first camera frame. Their development console adds allocation/render overhead. The latest source guards `Material.HasMatrix`, with regression coverage; the corrected build must be used for subsequent clean profiling. These 35/15-second traces remain valid observations of the explicitly identified intermediate build.

Four command-forced sleep/wake cycles passed across Mono portrait, both landscapes and Stereo landscape; two included GPU recording. AR pose error and background matrix error were zero in logged samples. All four GPU point-reference checks passed. Both finalized movies decode (31/32 frames respectively, zero dropped). The separate 30-second timer test was inconclusive for the feature: the phone physically changed direction during the waiting window, correctly resetting the timer, so the probe reported failed entry. Repeat with a stationary phone; do not count that run as a pass.

## Clean probe follow-up

After the first-frame matrix guard, startup logs contain no missing-matrix errors and the allocation counter returns to roughly 3.0–3.2 kB/frame at 60 fps. The latest 27/27 EditMode suite passes. Four more forced sleep/wake cycles pass, now also asserting exact agreement of the production stencil and VFX matrices with the actual background matrix (logged error 0 in every cycle). Three finalized GPU movies (33, 31 and 61 frames) decode completely with zero drops.

A stationary-phone rerun passes the real 30-second entry / 2-second wake timer, recording finalization, ARSession/microphone stop, 5 fps sleep and 60 fps wake. The probe temporarily uses the phone's current physical direction as the idle direction; the production scene's LandscapeRight setting is unchanged. This is not a claim that a mounted bat mask was physically inverted during the test.

The clean 30-second cumulative ablation completed at Normal/Fair thermal state without an abort. `gpu-component-ablation.trace` and `ablation-second-metrics.json` preserve the evidence. Mesh packing, particles, meshing, occlusion and the AR session were disabled in sequence and restored. Stopping the AR session produced the largest clear CPU decrease in this combined sequence; individual sensor power cannot be separated from this experiment. No additional visual-quality reduction is justified by these short measurements.
