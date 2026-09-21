# Task Plan: Upgrade EchoVision to Unity 6.3 (latest) + iOS 27

## Goal
Project builds and runs on a physical iPhone from Unity 6000.3.24f1 against the iOS 27 SDK,
with all compile/build errors resolved.

## Key finding that reframes the task
The project is **already on Unity 6.3** (`6000.3.8f1`). "Upgrade to Unity 6.3" therefore means
bumping to the newest 6.3 patch, **6000.3.24f1** (already installed locally).

## Baseline (verified 2026-09-21)
- ProjectVersion: `6000.3.8f1`
- Installed editors: 6000.6.0f1, **6000.3.24f1**, 6000.0.84f1, 2023.2.3f1, 2022.3.62f3, 2021.3.45f2
  (all with iOS + visionOS modules)
- Latest 6.3 release available: `6000.3.24f1`  | latest 6.6: `6000.6.2f1`
- Xcode **27.0** (27A266a), iOS SDK **27.0** present
- `iOSTargetOSVersionString: 26.0`, `iPhoneSdkVersion: 988`, `tvOSTargetOSVersionString: 15.0`
- No Unity Editor running; no `Build/` (iOS Xcode project absent), `Build_visionOS/` present
- Devices: "Botao's iPhone 17 Pro" + an iPad, both **unavailable** (not attached)
- Key packages: ARFoundation 6.4.0-pre.1 (resolves 6.4.1), ARKit 6.4.1, visionOS 3.0.5,
  URP 17.4.0, VFXGraph 17.4.0, unity-mcp 0.48.1, HoloKit SDK embedded (local fork)
- Only build post-processor: `Packages/io.holokit.unity-sdk/Editor/HoloKitBuildProcessor.cs`
- Working tree: ~21 modified files uncommitted before this task started

## Phases
- [x] Phase 0: Inventory current state (versions, toolchain, devices, packages)
- [x] Phase 1: Safety checkpoint (file snapshot only; user made no git decision)
- [x] Phase 2: Upgraded to 6000.3.24f1 headless — exit 0, **0 compile errors**
- [x] Phase 3: No compile errors to fix (3 CS0618 warnings only, deferred)
- [x] Phase 4: iOS settings — NO change needed (min stays 26.0, SDK already "device/latest")
- [~] Phase 5: iOS Xcode export running (task b0mgmnzk9)
- [ ] Phase 6: xcodebuild against iOS 27 SDK; fix native/link errors
- [ ] Phase 7: Install + launch on device (needs an attached iPhone)
- [ ] Phase 8: Report changed files + verification

## Open Questions
1. iOS **minimum deployment target**: raise 26.0 -> 27.0, or keep 26.0 and merely build with the
   iOS 27 SDK? Raising it drops every device still on iOS 26. -> ASKED USER
2. Stay on 6.3 as asked, or go to 6.6 (6000.6.0f1 installed)? -> defaulting to 6.3 as instructed.

## Decisions Made
- Target editor = `6000.3.24f1` (newest 6.3, already installed -> no download needed).
- Do NOT touch `com.unity.xr.visionos`; this is a dual-target iOS + visionOS project.

## Errors Encountered
- (none yet)

## Status
**Phase 1** - taking a safety checkpoint before the editor upgrade.

## Decisions (updated 2026-09-21)
- iOS min deployment target **stays 26.0**; build against iOS 27.0 SDK. (user)
- **Keep visionOS code as-is.** Do not remove packages or Build_visionOS/. Focus iOS. (user)
- No git commits; file snapshot in scratchpad is the only rollback. (default, unanswered)
- Scene list passed explicitly in the builder; `EditorBuildSettings.m_Scenes` is `[]` and was
  deliberately NOT mutated.

## New files added
- `Assets/Editor/EchoVisionBuilder.cs` (119 lines) — headless iOS export entry point.

## Errors Encountered
- Unity Personal license: `LicenseCheck` throws for visionOS/PolySpatial on every domain reload.
  Does not affect iOS. Left in place per user instruction.
- `EditorBuildSettings.m_Scenes` is empty -> a default build would ship an empty app.
  Worked around in the builder rather than by editing project settings.

## Error: iOS export aborted at exit 255 (RESOLVED, pending confirmation)
First `-executeMethod EchoVisionBuilder.BuildiOS` run never invoked the method. Log sequence in
`scratchpad/export-ios.log`:
```
4712  Application.AssetDatabase Initial Refresh End
4715  [MODES] ModeService[none].Initialize
4723  SocketException: Connection refused   <- com.ivanmurzak.unity.mcp
5174  UnityEditor.EditorApplication:Internal_EditorApplicationQuit
      (EchoVisionBuilder never logged; Build/ never created; Unity exit 255)
```
Cause: `com.ivanmurzak.unity.mcp` ("AI Game Developer") auto-connects to a local MCP server at
editor startup. That server binary — `Library/mcp-server/osx-arm64/unity-mcp-server`, referenced
by `.mcp.json` — does not exist, so the package raises an unhandled `Debug.LogException` during
startup. In batch mode Unity aborts before running `-executeMethod`.

Ruled out first: the builder type DOES exist in `Library/ScriptAssemblies/Assembly-CSharp-Editor.dll`,
`Assets/Editor/EchoVisionBuilder.cs.meta` was generated, the scene path is valid, and there were
0 compile errors. So this was never a problem with the build script.

Fix: the package ships its own guard — `EnvironmentUtils.IsCi()` (checks `CI`/`GITHUB_ACTIONS`/
`TF_BUILD`), consumed at `Startup.Editor.cs:94` as `connectionAllowed = IsCi() == false`.
Running the export with **`CI=true`** suppresses the connection with **zero project-file changes**.
Preferred over removing the package from the manifest, which would churn packages-lock.json.

Note for later: this affects every headless Unity invocation in this project, not just builds.

## ROOT CAUSE of the headless build failure (confirmed 2026-09-21)

`-executeMethod` never runs in batch mode. Proven with a no-op probe:
`EchoVisionBuilder.Ping` (logs one line, exits 0) ALSO never executed — Unity exit 255,
empty stderr. So this was never about the build code.

Sole cause: **exactly one `InitializeOnLoad` exception** in the entire startup
(`grep -c ProcessInitializeOnLoadAttributes` -> 7 frames, 1 distinct exception):

```
System.Exception: Unity PolySpatial and visionOS XR support is only available to
Unity Pro, Unity Enterprise, and Unity Industry users.
  at UnityEngine.XR.VisionOS.Internal.LicenseCheck.CheckLicenseRequirements (isBuilding)
     com.unity.xr.visionos@66cce876ccca/Runtime/Internal/LicenseCheck.cs:89
  at UnityEngine.XR.VisionOS.Internal.LicenseCheck..cctor ()  LicenseCheck.cs:40
  UnityEditor.EditorAssemblies:ProcessInitializeOnLoadAttributes  EditorAssemblies.cs:132
```
Unity batch mode skips `-executeMethod` and exits non-zero when an `InitializeOnLoad`
static constructor throws. In GUI mode the same exception is only a console error.

So the Unity Personal license blocks the **iOS** build too, not just visionOS.

Two separate startup faults were found and only the first is fixed:
1. FIXED, zero project changes: `com.ivanmurzak.unity.mcp` unhandled `SocketException`
   -> suppressed with `CI=true` (package's own `EnvironmentUtils.IsCi()` guard).
2. OPEN: visionOS `LicenseCheck` throw (above).

### Lead worth trying first
`unity license status` -> "Unity Personal Version", and `signedIn: false, authMode: none`.
The machine is NOT signed in to a Unity account. If the user holds a Pro/Enterprise seat,
`unity auth login` may resolve this with no project change at all.

### Options if not Pro
- A. Sign in / activate a Pro seat -> everything works unchanged. PREFERRED.
- B. Temporarily drop `com.unity.xr.visionos` from manifest.json for iOS builds, restore after.
     Reversible (backups held), but churns packages-lock.json and risks the
     `UnityEditor.XR.VisionOS.VisionOSSettings` entry in EditorBuildSettings.asset.
- C. Build from the Unity GUI, where the InitializeOnLoad throw is non-fatal.
