# HoloKit SDK promotion — deferred until validation

User direction, 2026-09-22: validate the SDK embedded in EchoVision completely first. Only then proceed to the downstream SDK update stage. Do not modify either destination repository during current validation.

Deferred destinations:
- /Users/amber/Projects/HoloKit/HoloKit2/holokit-unity-sdk
- /Users/amber/Projects/HoloKit/HoloKit2/holokit-unity-sdk-boilerplate

Current work: asynchronous video stop, GPU resource lifetime, repeat recording, disable/destroy, rapid sleep/wake and real iOS background/resume. Measure stop-call latency separately from finalization latency and verify recorded files by decoding them.

Before promotion:
- Review the complete embedded SDK diff and distinguish SDK changes from EchoVision-specific code and development probes.
- Complete the Unity 6.3 / iOS 27 validation matrix, including orientation, AR background/human stencil alignment, GPU recording with and without 4K, audio, lifecycle, and GPU mesh behavior. Carry forward existing evidence with its exact build/device; do not label older-build results as current-build validation.
- Resolve failures and explicitly record any unverified requirements; a successful build alone is insufficient.
- Prepare the validated SDK revision and migration notes, then enter the downstream synchronization stage with the user. The destination repositories remain untouched until then.

Status: validation in progress; downstream synchronization has not started.

Initial audio configuration audit (before refactor, 2026-09-22; superseded by the implementation checkpoint below):
- Recorder's OnAudioFilterRead is on the Main Camera with AudioListener, so its tap receives listener audio, not exclusively the referenced microphone source.
- MicrophoneAPI routes one microphone clip to separate analysis and recording sources. Analysis mixer group is -80 dB; recording source is played on start with volume 0.5 and routes to Master.
- StartRecording currently requires a microphone source/clip even for a hypothetical game-only mode.
- OnAudioFilterRead clears the listener buffer unconditionally, muting downstream game audio as well as microphone monitoring, including outside recording.
- Before general SDK promotion, resolve/document explicit capture selection (game, microphone, mixed, silent), independent capture levels versus playback/monitoring, and microphone permission/lifecycle. These modes are not implemented or validated yet. Do not silently expand the current stop-latency patch into an audio redesign.

## Expanded authorized goal (2026-09-22 continuation)

User has now explicitly requested implementation and testing, then conditional TestFlight publication and downstream SDK upgrade. Preserve this full scope:

- [x] Move recorder regression tests into the embedded SDK, with independent assemblies, generic CLI tools, and only application-specific scene/sleep wiring left in EchoVision. Migration validated by 26 SDK + 13 EchoVision EditMode tests and 6 native writer cases; this does not mean the device acceptance matrix passed.
- [ ] Locate and resolve repeated-recording memory growth; validate async stop/lifecycle and file integrity.
- [ ] Implement shared microphone ownership and Inspector configuration on SDK components; remove SDK dependencies on EchoVision-specific classes.
- [ ] Environment preset: raw environment audio, no AEC; maintain EchoVision sound analysis independently of recording.
- [ ] MOFA preset: internal game track plus AEC-cleaned microphone track; actual speaker-to-microphone tests, independent playback and recording levels.
- [ ] Use a common timebase for video/game/mic and all tracks; measure initial offset, long-recording drift, stop, sleep and resume with flash/pulse fixtures. Passing decode or matching durations alone does not establish synchronization.
- [ ] Complete EchoVision device and regression tests, including the previous AR/orientation/4K/GPU/audio/lifecycle requirements and release build.
- [ ] Publish the validated EchoVision build to App Store Connect TestFlight, then verify uploaded build processing/status.
- [ ] Promote the validated embedded SDK into holokit-unity-sdk, preserving existing local changes and restricting support to Unity 6 and later.
- [ ] Upgrade holokit-unity-sdk-boilerplate to test the promoted SDK, add demonstration scenes for both microphone modes and extensive automated/device regression tests.
- [ ] Complete a requirement-by-requirement SDK/boilerplate verification and publish exact build/version/evidence references.

Read-only downstream inspection found existing local edits in both repositories. Preserve them when promotion begins; do not overwrite the checkout wholesale. Promotion and TestFlight are authorized conditional on their validation gates; no additional routine permission request is needed.

Current status: both downstream repositories have only been inspected, not changed. Audio design is accepted but implementation remains pending. The initial async stop pressure run terminated after 72 successful cases; an instrumented run completed 91 cases, but Metal memory rose from 307 to 2066 MiB over 56 seconds. Its 90 nonempty movies decoded with exact submitted-frame counts, but 2 exceeded the 200 ms A/V end-time threshold. These are outstanding failures, not release approval.

## Current implementation checkpoint

Shared native microphone ownership, recorder Inspector configuration, independent AAC game/microphone tracks, common host-clock timestamps, bounded audio queues and asynchronous audio drain are integrated in the embedded SDK. EchoVision analysis now reads SDK microphone samples directly. Both downstream repositories and TestFlight remain untouched pending acceptance.

Verified: Unity 39 EditMode tests; native writer six original plus four source-mask/pulse cases; integrated iOS stop pressure 91 scenarios/90 movies with no errors or retained recording resources; integrated background movie valid before resume and unchanged afterward. These pressure/background runs precede the execution-order and fixture additions, so repeat on final source.

Short device game/flash timing is within 50 ms, but full synchronization is **not accepted**. The bypassed microphone contains a strongly correlated speaker echo about 275 ms after the game track. AEC reduces that echo, but the first analyzer used too narrow a window and produced a misleading attenuation result. Reanalysis now measures the acoustic path delay separately; it must never shift track timestamps to manufacture a synchronization pass. Next: inspect actual session/node/DSP latencies, separate playback buffering from microphone capture/processing delay, test independent near-end voice, and run long-duration/lifecycle fixtures. Optional mixed sharing output and final SDK/boilerplate promotion remain outstanding.
