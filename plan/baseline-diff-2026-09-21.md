# May 4 baseline comparison and cleanup

Baseline: `9fa39f1` (2025-05-04). Verified recovery checkpoint: `bff71d8`.
The installed Release/archive belongs to that checkpoint. No push or upload.

## Required or deliberately retained

- Unity 6000.1.1f1 -> 6000.3.24f1; URP 17.1 -> 17.4; AR Foundation/ARKit 6.1 -> 6.4.3. Retain automatic scene/renderer/importer serialization, including the LiDAR tip sprite importer migration.
- Xcode 27 linker compatibility; iOS 27 absolute camera pose and custom background matrix. HoloKitCameraManager's orientation state machine remains unchanged from May 4.
- GPU encoder-buffer ownership/synchronization, lifecycle and audio timing fixes; CPU fallback; automatic 4K input and exact prior-format restoration. Explicitly requested and device tested.
- Raw microphone analysis bypasses mixer attenuation; scene recorder/UI references and null guards. Existing optional audio diagnostics retained for reproducibility; source attribution restored.
- iOS render target stays 60 fps. Simplified the helper and removed the incorrect claim that iOS requires disabling vSync. [Unity API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-targetFrameRate.html).
- Minimum iOS 26 and version 0.3.0 retain the earlier decision recorded in `changes.md`; SDK 27 does not itself imply that minimum. The microphone speaker-routing setting is retained with the physically verified audio path.
- visionOS dependency remains excluded from this iOS checkout because its license initialization blocked the CLI; source/assets retained, see `../Tools/VISIONOS.md`.
- Tests and diagnostic source remain separate from the shipping profile. Historical notes are evidence of past experiments, not current acceptance reports; `recovery-2026-09-21.md` is authoritative.

## Removed or restored

- Removed unused MCP config, OpenUPM scopes/registry state and MCP compilation defines on all platforms.
- Restored runtime-written SoundWave material volume/origin to neutral defaults.
- Restored original XR simulation GUIDs and Room17 environment; moved ignored duplicate XR/Temp assets outside Assets into `Build_RecoveryArtifacts/xr-simulation-temp-before-cleanup` to prevent GUID collisions.
- Restored TMP editor visibility preference and original URP reflection-atlas/postprocess-alpha settings. The original Start/Stop wording was briefly restored during cleanup, then the green `● REC`/black `Start Recording` visual was reinstated at the user's explicit request; it is a useful retained change. Retained Unity's new asset schema; build-derived prefilter fields may be regenerated.
- Removed the duplicate iPhone13Mini entry. The four iPhone19,* entries were removed in cleanup and then restored at the user's explicit request to retain iPhone18-series support. The user explicitly accepts inherited previous-generation parameters and requests iPhone17/17Pro/17ProMax as well; all those entries are present and retained. Fresh optical calibration is not a release prerequisite. Earlier local iPhone16e/Air/SE/mini entries are retained. Device test evidence is still specifically from iPhone15Pro.

## Verification boundary

Cleanup EditMode regression: 17/17 passed. The previous Release checkpoint remains available and installed. Render-setting restoration still needs device inspection; the user's new human-stencil alignment report is a separate open issue. The existing background/mesh and audio passes do not prove human-stencil registration.
