# visionOS status in this project

## Summary
`com.unity.xr.visionos` has been **removed from the package dependencies**, so it no longer
affects the iOS build. **All visionOS source, configuration assets and build output are kept**
and can be restored in one step.

## Why the package had to be removed (rather than commented out)
The problem is not our own code. There are **zero** references to visionOS in `Assets/` or in
`Packages/io.holokit.unity-sdk/` (verified by grep).

The actual cause is the package's own `LicenseCheck`, which throws from an `InitializeOnLoad`
static constructor:

```
System.Exception: Unity PolySpatial and visionOS XR support is only available to
Unity Pro, Unity Enterprise, and Unity Industry users.
  at UnityEngine.XR.VisionOS.Internal.LicenseCheck..cctor ()  LicenseCheck.cs:40
  UnityEditor.EditorAssemblies:ProcessInitializeOnLoadAttributes  EditorAssemblies.cs:132
```

**Unity batch mode skips `-executeMethod` entirely and exits non-zero when an `InitializeOnLoad`
static constructor throws.** That code lives in `Library/PackageCache/`, a read-only cache
managed by Unity, so it cannot be commented out or patched. Removing the dependency is the only
way to make a headless iOS build run at all.

In the GUI the same exception is only a console error, which is why this is invisible during
normal use.

## What is preserved

| Item | Path | Status |
|---|---|---|
| visionOS XR settings | `Assets/XR/Settings/VisionOSSettings.asset` | kept |
| visionOS runtime settings | `Assets/XR/Settings/VisionOS Runtime Settings.asset` | kept |
| visionOS loader | `Assets/XR/Loaders/VisionOSLoader.asset` | kept |
| XR per-target settings | `Assets/XR/XRGeneralSettingsPerBuildTarget.asset` | kept |
| visionOS PlayerSettings entries | `ProjectSettings/ProjectSettings.asset` | kept (`visionOSBundleVersion` etc.) |

These are pure data assets. With the package absent they take no part in compilation and
produce **no build errors**; they simply show as unresolved script references in the Inspector.

## How to restore visionOS

Prerequisite: a **Unity Pro / Enterprise / Industry** licence, signed in via `unity auth login`.
Without it, restoring the package immediately reproduces the blocker above.

Add one line back to the `dependencies` block of `Packages/manifest.json`:

```json
"com.unity.xr.visionos": "3.2.2",
```

(Place it after `com.unity.xr.arkit` to preserve the existing ordering.)

Reopen Unity; the package resolves and the assets above rebind automatically.

## Notes
- `Packages/manifest.json` is JSON and cannot carry comments, which is why these instructions
  live in this file rather than inline.
- The package was at one point embedded at `Packages/com.unity.xr.visionos/`. After that
  directory was deleted, leftover references produced a large number of `error CS2001`. Removing
  the dependency completely cleared them; the project now compiles.
