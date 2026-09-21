# EchoVision recovery — active goal

Updated 2026-09-21. This is the task queue for the existing active recovery goal.
Do not mark the goal complete until every required item below has concrete evidence.

- [x] Preserve and commit the verified current Release: `bff71d8`; archive in `Build_RecoveryRelease/`.
- [x] Compare against 2025-05-04 `9fa39f1` and commit scoped cleanup: `18dfc7f`; see `baseline-diff-2026-09-21.md`. Cleanup EditMode: 17/17.
- [x] Retain iPhone17/17Pro/17ProMax and restore iPhone18-series entries. The user explicitly accepts previous-generation parameters; fresh optical calibration is not a release prerequisite.
- [x] **Human stencil:** live hand comparison identified the red/current mapping defect; cyan/full-background mapping broadly matched the hand. Fix `1169500` shares that matrix with production shaders/VFX, preserves crop/scale and handles first-frame readiness. Four orientation/crop regressions and device sleep/wake matrix checks pass; probe excluded from shipping. This is transform correctness evidence, not a new quantitative contour/latency benchmark after the user left.
- [x] **Bat-mask rest orientation:** preserve original LandscapeRight physical orientation, 30 s entry, 2 s exit and fade. Verify the configured rest direction, recording finalization, microphone/ARSession shutdown, low render workload and correct wake; distinguish runtime tests from a physical mounted-mask trial. The ARSession omission is repaired. Real timer test passed entry/recording finalization/5 fps shutdown and 60 fps wake using the stationary phone's direction; production LandscapeRight setting remains unchanged. Four CLI-forced rapid sleep/wake cycles passed, including two recording cycles; native pose/background matrices and GPU mesh samples matched after wake. No claim of a physical mounted-mask inversion test.
- [x] **4K FOV transition:** measured 1920x1440 -> 3840x2160 -> 1920x1440, with horizontal projection FOV about 71.25 -> 73.79 -> 71.25 degrees. This format/intrinsics change explains the modest view change; restoration and rendered native pose pass. Full crop is retained in stencil matrices; no artificial zoom compensation added.
- [x] **Performance/heat:** Time Profiler, Metal Performance Overview, Thermal State and attempted Power Profiler measurements captured. Clean cumulative component ablation completes; GPU mesh removes CPU array/upload work, and sleep demonstrably stops AR/microphone and reduces CPU/GPU work. Charging made battery-power data unusable; no watts/savings percentage or LiDAR-largest claim. See performance report.
- [x] **MeshVFX GPU path (explicit requirement):** GPU vertex buffers -> compute packing -> VFX GraphicsBuffers. No CPU vertex/normal array extraction or CPU upload fallback. Keep CPU metadata/scheduling only. GPU compute tests passed for interleaved/separate streams, transforms and bounds; 27/27 EditMode total after adding stencil transform regressions. Repeated device GPU reference checks passed (8 points each, max error 0). Both scene references bind the compute shader. No controlled whole-app energy improvement percentage claimed.
- [x] **Optimize:** scoped GPU mesh and sleep changes committed as `8205fb1`; GPU recording and automatic 4K retained. CPU profile supports removal of uploads; differences in geometry and diagnostic overhead prevent a causal whole-app percentage. Avoid further visual-quality changes without stronger measurements.
- [x] **Final regression:** 27/27 Unity CLI EditMode tests, final device walker failures=0, five GPU movies fully decoded/zero dropped frames, real microphone triggers in all four mode/record combinations, 4K restore, stencil transform and sleep/wake checks. Clean Release archived, signature verified, installed and visually inspected; diagnostics absent. Source commits `8205fb1` and `1169500`; see `release-validation-2026-09-21.md` for exact coverage and limits.

- [x] Preserve recording UI: green `● REC` while recording, black `Start Recording` otherwise. User explicitly requests this visual; do not remove during cleanup.

- [x] Rename SDK native camera reference export and all managed/test calls to `HoloKit_GetARCameraReference`; verify linking in the next device build.

- [x] Remove the macOS Unity bundle and its build script at the user's request. Native CPU tests compile a temporary host library outside the Unity asset tree; Editor recording exits cleanly.

## Constraints and evidence

Use Unity CLI. No redundant approval requests; full access is enabled. No upload requested at this stage. Preserve checkpoints and separate cleanup, diagnostic and functional commits.

Existing evidence: `recovery-2026-09-21.md`, ignored `Build_RecoveryArtifacts/`. iPhone15Pro/iOS27 verified so far; no claim for iPhone17/iPad or unsupported PortraitUpsideDown. Live hand mapping evidence and transform regressions are recorded separately from unmeasured contour latency and battery power. No claim of full physical testing on every supported phone model.

## Immediate order (user steering)

Prioritize live hand/forearm stencil capture before the user leaves. Then repair any evidenced mapping issue, run CLI quick-sleep cycles, finish profiling and the complete regression, remove diagnostic assets, and commit/install the final Release. Do not replace the live hand check with automated matrix checks.

## Current findings (baseline unless noted)

- Live hand test confirmed a large red/current versus cyan/background mapping mismatch, especially in landscape. 18 screenshots and a partial screen recording are preserved; source fix now shares the full background transform. After-fix validation pending; see `human-stencil-2026-09-21.md`.
- Baseline MeshVFX copied/uploaded CPU arrays each frame. Current implementation packs raw GPU vertex buffers after mesh changes/head movement and retains CPU metadata only.
- Baseline PowerSaver left ARSession running. Current implementation stops it and uses 5 fps after fade; real timer and rapid transitions pass.

## Completion boundary

The requested implementation/recovery work and feasible repeatable device checks are complete. A new after-fix human contour/latency benchmark, physical mounted-mask trial, battery-watt measurement, and testing every supported phone model are not claimed. Those are distinct from the verified transform, configured sleep logic, CPU/GPU workload reductions, inherited phone parameters and iPhone15Pro regression. No publishing/upload was authorized at this stage.
