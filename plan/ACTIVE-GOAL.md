# EchoVision recovery — active goal

Updated 2026-09-21. This is the task queue for the existing active recovery goal.
Do not mark the goal complete until every required item below has concrete evidence.

- [x] Preserve and commit the verified current Release: `bff71d8`; archive in `Build_RecoveryRelease/`.
- [x] Compare against 2025-05-04 `9fa39f1` and commit scoped cleanup: `18dfc7f`; see `baseline-diff-2026-09-21.md`. Cleanup EditMode: 17/17.
- [x] Restore pre-cleanup iPhone18-series model entries at the user's request. Preserve support; document inherited offsets and untested calibration.
- [ ] **Human stencil:** dedicated overlay shader; compare current stencil transform against actual AR background transform using the user's hand/body. Check portrait, both landscapes, stationary vs moving, recording/4K off/on. Do not confuse segmentation quality/latency with geometric registration. Fix only after evidence; exclude probe from shipping.
- [ ] **Bat-mask rest orientation:** preserve original LandscapeRight physical orientation, 30 s entry, 2 s exit and fade. Verify actual physical inversion, stop/finalize recording, microphone stop, ARSession/meshing shutdown, low render workload, then wake and resume correctly. Existing code is present but ARSession is absent from its sleep object list.
- [ ] **Performance/heat:** cool device first (app stopped). Capture Instruments Power Profiler + CPU/Metal evidence. Compare mesh-to-VFX upload, meshing, particles, human/environment depth, 4K/recording and sleep without assuming LiDAR dominates. Record thermal/charging state and distinguish component toggles from sensor-level power measurements.
- [ ] **Optimize:** make bounded changes supported by profiles; compare before/after under equivalent conditions. Keep GPU recording and automatic 4K.
- [ ] **Final regression:** Unity CLI tests, actual phone orientations × Mono/Stereo × recording, 4K restore, audio-trigger waves, human stencil, sleep/wake. Inspect decoded movies and frame counts. Produce/install clean Release; verify diagnostics excluded and commit changes.

## Constraints and evidence

Use Unity CLI. No redundant approval requests; full access is enabled. No upload requested at this stage. Preserve checkpoints and separate cleanup, diagnostic and functional commits.

Existing evidence: `recovery-2026-09-21.md`, ignored `Build_RecoveryArtifacts/`. iPhone15Pro/iOS27 verified so far; no claim for iPhone17/iPad or unsupported PortraitUpsideDown. Human-stencil correctness and energy reduction are still unverified.

## Current findings

- Stencil uses an old camera matrix with scaling/crop translation stripped; corrected camera background uses a different full matrix. This is a hypothesis to test, not yet a verified fix.
- `MeshVFX.LateUpdate` copies mesh vertices/normals/triangles and uploads buffers every rendered frame, even without mesh changes. Profile before altering cadence/data ownership.
- `PowerSaver` disables Main Camera, MeshManager, AudioInput, SoundWaveEmitter and MeshVFX after fading. It leaves AR Session enabled and does not lower the render frame rate. Validate runtime subsystem state.
