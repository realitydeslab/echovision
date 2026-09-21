# Change Inventory — Unity 6.3 latest + iOS 27

Two groups: **(A) already applied** by the headless upgrade run (exit 0), and
**(B) proposed, not yet done** — awaiting your go-ahead.

---

## A. ALREADY CHANGED — 6 files

Verified by diffing against a pre-task snapshot in the scratchpad, not against the last git
commit (the tree already had 35 dirty files before I started, so `git diff` overstates this).

| # | File | Change |
|---|------|--------|
| 1 | `ProjectSettings/ProjectVersion.txt` | `6000.3.8f1` -> **`6000.3.24f1`** |
| 2 | `Packages/manifest.json` | 4 version bumps (below) |
| 3 | `Packages/packages-lock.json` | 12 packages re-resolved (below) |
| 4 | `Assets/Images/tip-need-lidar.png.meta` | importer re-serialize |
| 5 | `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` | URP settings re-serialize |
| 6 | `ProjectSettings/ShaderGraphSettings.asset` | re-serialize |

### manifest.json (direct dependencies)
```
com.unity.ide.rider        3.0.39 -> 3.0.40
com.unity.memoryprofiler   1.1.11 -> 1.1.12
com.unity.timeline         1.8.10 -> 1.8.13
com.unity.xr.visionos      3.0.5  -> 3.2.2
```

### packages-lock.json (incl. transitive)
```
com.unity.burst                       1.8.27 -> 1.8.30
com.unity.collections                 2.6.2  -> 2.6.8
com.unity.editorcoroutines            1.0.1  -> 1.1.0
com.unity.ide.rider                   3.0.39 -> 3.0.40
com.unity.inputsystem                 1.18.0 -> 1.20.0
com.unity.memoryprofiler              1.1.11 -> 1.1.12
com.unity.searcher                    4.9.4  -> 4.9.5
com.unity.test-framework.performance  3.2.0  -> 3.5.0
com.unity.timeline                    1.8.10 -> 1.8.13
com.unity.xr.core-utils               2.5.3  -> 2.6.0
com.unity.xr.management               4.5.3  -> 4.6.1
com.unity.xr.visionos                 3.0.5  -> 3.2.2
```

### NOT changed
- `ProjectSettings/ProjectSettings.asset` — byte-identical to the pre-task snapshot.
- ARFoundation (`6.4.0-pre.1` -> resolves 6.4.1) and ARKit (`6.4.1`) — untouched.
- No C# source file was modified.

### Compile result
**0 errors.** 3 warnings, all `CS0618 FindObjectOfType` and all inside the HoloKit fork:
- `Packages/io.holokit.unity-sdk/Runtime/UI/HoloKitDefaultUICanvas.cs:27`
- `Packages/io.holokit.unity-sdk/Runtime/Gaze Interaction/GazeRaycastInteractor.cs:19`
- `Packages/io.holokit.unity-sdk/Editor/HoloKitCreateUtils.cs:191`

---

## B. PROPOSED — not yet done

### B1. iOS 27 — likely ZERO settings changes needed
Per your answer, min deployment target stays **26.0**. Checked what that implies:
- `iOSTargetOSVersionString: 26.0` -> **unchanged**
- `iPhoneSdkVersion: 988` -> already "Device SDK", which means *latest installed* = iOS 27.0.
  **unchanged**
- Xcode 27.0 with `iphoneos27.0` is already the only iOS SDK on this machine, so the generated
  project compiles against iOS 27 with no edit at all.

So "upgrade to iOS 27" is not a settings edit — it is a **build-and-fix exercise**. The real work
is B2/B3.

### B2. Generate the iOS Xcode project (`Build/` does not currently exist)
Needs an `-executeMethod` entry point. The project has **no build script** — the only build hook
is `Packages/io.holokit.unity-sdk/Editor/HoloKitBuildProcessor.cs` (a post-processor, not an
entry point). So I would **add one new file**:
- `Assets/Editor/EchoVisionBuilder.cs` (new) — a minimal `BuildPipeline.BuildPlayer` wrapper for
  headless iOS export. ~40 lines. Nothing else in the project references it.

### B3. Build with Xcode 27 and fix what breaks
Unknown until it runs. Expected candidates, in likelihood order:
- Unity 6000.3.24f1's iOS trampoline vs the iOS 27 SDK (deprecated UIKit calls)
- `Packages/io.holokit.unity-sdk/Runtime/iOS/NativeCode/VideoRecorder.mm` — already locally
  modified, uses AVFoundation/ReplayKit, a common breakage point on new SDKs
- Code signing (`appleEnableAutomaticSigning: 0`, manual profile)

### B4. Optional cleanup — say if you want it
- Fix the 3 `FindObjectOfType` warnings -> `FindFirstObjectByType`. Touches the HoloKit fork,
  which you already have local modifications in. **Default: skip** (warnings only).
- `tvOSTargetOSVersionString: 15.0` is stale but there is no tvOS target. **Default: leave.**

---

## BLOCKERS / RISKS

### R1. Unity Personal license blocks visionOS — HARD BLOCKER for the visionOS half
`unity license status` reports **"Unity Personal Version"**, and the upgrade log contains:
```
System.Exception: Unity PolySpatial and visionOS XR support is only available to
Unity Pro, Unity Enterprise, and Unity Industry users. These packages will be uninstalled.
  at UnityEngine.XR.VisionOS.Internal.LicenseCheck.CheckLicenseRequirements
     (com.unity.xr.visionos@66cce876ccca/Runtime/Internal/LicenseCheck.cs:89)
```
`com.unity.xr.visionos` 3.2.2 is **still present** in the manifest after the run, so nothing was
actually removed this time — but the check fires on every domain reload and explicitly fires
`isBuilding: true` on build. **The iOS target is unaffected.** Needs a Pro seat, or the visionOS
target gets dropped. Your call — I did not touch it.

### R2. No device attached
`xcrun devicectl list devices` shows "Botao's iPhone 17 Pro" and an iPad, both **`unavailable`**.
The iPhone 16 J from my notes is gone. Phase 7 (install + launch) needs a cable.

### R3. The upgrade ran unlicensed-for-visionOS and I had no git checkpoint
You did not answer the checkpoint question, so I made **no git commits**. Rollback today is the
scratchpad snapshot (`ProjectSettings/`, `manifest.json`, `packages-lock.json`) — which does NOT
cover `Library/`, the two re-serialized assets, or the `.meta`. Reverting the editor bump means
re-importing under 6000.3.8f1.
