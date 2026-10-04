# Temporary stencil and performance probe

This source lives outside Unity's Assets tree. For a diagnostic build, copy `Assets/` to `Assets/HumanStencilProbe/`, then move `iOS Stencil Debug.asset` and its meta into `Assets/Settings/Build Profiles/`. Build with Unity CLI profile `iOS Stencil Debug`. Remove those temporary copies before a shipping build. The normal device walker is excluded by using a separate `ECHOVISION_STENCILTEST` define.

The full-screen shader shows the actual human-stencil texture:
- Current/red: the application's production display matrix (normalized/incorrect before `1169500`; shared full camera matrix after the fix).
- Camera/cyan: the complete matrix actually used by the corrected AR background.
- Compare: both; coincident regions are white, disagreement red/cyan.
- Off: no overlay, for performance collection.

Hold a hand/forearm in view, first stationary, then moving. Check centre and screen edges, portrait and both landscapes; start/stop recording to change the camera input format. If a lone hand is not segmented, include more of the arm/body before concluding that mapping is broken. The overlay identifies segmentation pixels, not ground-truth body boundaries. Save screenshot captures the displayed overlay. It is a ScreenSpaceOverlay canvas and is **not** included in the GPU recorder's camera render.

Logs report thermal state, frame rate, GC allocation counter, physical vs interface orientation, sleep timer/state, ARSession running state and recording-related occlusion mode. `Profile 30s` disables components cumulatively in 5-second windows: baseline, mesh upload, particles, meshing, occlusion, ARSession. This is an initial attribution experiment, not an isolated LiDAR power measurement or a causal benchmark; repeat selected A/B cases at comparable thermal/charging state. Do not profile a hot device (Serious/Critical entry is rejected). Close the app between runs to cool.

Use Instruments Power Profiler with Time Profiler/Metal traces. A foreground black screen is not proof that ARSession stopped. Physical rest-orientation test should wait 30 seconds plus the 2-second fade, then verify subsystem/microphone state and wake.

For CLI coordination, write a single command into the app's Documents/probe-command.txt via `devicectl device copy to --domain-type appDataContainer --domain-identifier org.realitydeslab.echovision`. Supported commands: `snapshot`, `profile`, `fov`, `sleep`, `sleep-now`, `wake-now`, `quick-sleep`, `record`, `meshcheck`, `quit`, or overlay mode `0`/`1`/`2`/`3`. These commands only exist in the temporary development probe.

`sleep-now` / `wake-now` call the actual PowerSaver transitions and skip the timer/fade for rapid diagnosis. `quick-sleep` repeats sleep/wake in Mono portrait, both landscapes and Stereo landscape, including recording in two cycles. It checks ARSession/microphone shutdown, recorder finalization, resumed native camera pose/display matrix, exact production stencil/VFX matrix agreement, and GPU mesh output; before/after screenshots support visual review. These checks do not establish human-stencil alignment without a person in view. The original physical 30-second rest test remains separate.
