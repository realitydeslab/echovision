# Asynchronous recorder stop validation — 2026-09-22

Scope: validate the existing uncommitted asynchronous stop implementation in the embedded HoloKit SDK; reduce stop-click stalls without claiming an improvement to sustained recording FPS. Preserve unrelated scene, UI, audio, quality/pacing and author-credit edits.

Environment: Unity 6000.3.24f1 CLI, Xcode 27 / iOS 27 SDK; physical iPhone 17 Pro (iPhone18,1), iOS 27.0 (24A437). Do not substitute the previous iPhone 15 Pro evidence for this build.

Implementation under test:
- Stop capture and audio input, enter draining state immediately.
- Submit an asynchronous one-pixel readback per pending GPU slot, retaining textures until completion.
- Preserve capture FIFO order even if callbacks arrive out of order; failed callbacks count as dropped frames and do not block later slots.
- Finalize only after all pending GPU slots and fallback readbacks finish. AVAssetWriter already finalizes asynchronously.
- Block restart during GPU drain and native writer finalization.
- Completion callbacks, rather than a camera-owned coroutine, allow finalization after disabling/destroying the recorder.

Completed local checks:
- Unity CLI EditMode: 39/39 passed. New FIFO tests enumerate 0–3 pending slots, every starting slot, all callback permutations, and all failure masks (270 combinations).
- Native AVAssetWriter harness: 6/6 movies passed frame count, dimensions, channel count/sample rate, decoded audio RMS, RGB channel order and A/V-duration checks. Fixed the harness ABI to include the existing frameRate parameter. Double-stop, rejected duplicate start, rejected start during finalization and subsequent restart are exercised.

Device methodology:
- Dedicated development-only RecorderStopProbe, selected by a one-shot Documents/recorder-test-mode.txt containing stop or background; normal recovery walker is bypassed. Initial iOS launch-argument selection did not reach managed code and instead ran the existing orientation walker; this was corrected to file-based selection. Neither probe is included by the shipping iOS profile.
- Interleave 20 samples per method/format plus warm-up, at the same capture workload. Compare current asynchronous stop to a same-binary emulation of the old one-pixel WaitForCompletion + WaitAllRequests path. The emulation uses reflection and is not an exact historical-build benchmark; it retains current frame pacing, bitrate and capture code.
- Record main-thread stop-call duration separately from time until IsFinishing clears. Finalization is observed at frame cadence, not directly timestamped in the native callback. Frame-time maximum is sampled through finalization and two following frames.
- Lifecycle cases: immediate stop with zero frames, 1–3 pending GPU copies, repeated stop/restart, disabled recorder, destroyed recorder, immediate application sleep, OS background/resume.
- Batch test export disables only the generated Xcode copy's Photos save call, avoiding benchmark clips in the user's album. Repo native source remains unchanged; sandbox MP4 creation and writer completion are exercised. Album saving is not validated by that batch.
- Copy and decode movies with ffprobe/ffmpeg; match decoded frames to SubmittedFrames and check A/V end-time delta < 0.2 s.

Evidence directory: /tmp/echovision-async-stop-20260922 (will preserve results under ignored Build_RecoveryArtifacts).

Status: device validation in progress. Do not interpret this document as a complete SDK promotion approval. See sdk-promotion-2026-09-22.md for deferred destinations and audio API limitations.

## SDK-owned regression suite

At Amber's request, moved the device probe, build-only native diagnostics, movie
collector, and recording state tests into `Packages/io.holokit.unity-sdk/Tests/Recorder`.
The device assemblies use the SDK-specific `HOLOKIT_RECORDER_TESTS` define.
EchoVision retains only its microphone/power-save scene adapter. The collector
accepts an explicit bundle ID and rejects incomplete reports. Test reports now
identify the device, OS, mode and completion state.

Unity CLI after the move: 26 SDK EditMode tests + 13 EchoVision tests passed.
Evidence: `sdk-editmode.xml`. Unity iOS export and Xcode device build also passed
(`unity-sdk-probe.log`, `xcode-sdk-probe.log`). Runtime verification is separate.

## Memory isolation, physical iPhone 15 Pro, iOS 27.0 (24A437)

The phone reconnected on 2026-09-22 was iPhone16,1, not the earlier iPhone18,1.
Do not compare their absolute timings as a same-device optimization result.

- Flushing the recorder's CVMetalTextureCache after writer finalization did not
  fix growth. The stop stress run produced 77 completed cases before signal 9;
  it did not finish and does not pass regression.
- A subsequent diagnostic run held other workloads apart. Idle footprint was
  about 848 MiB; 20 camera-format switch cycles without recording ended around
  858 MiB, without sustained growth. The pre-recording GPU counter in this first
  diagnostic returned zero because the recorder's Metal device was not yet
  initialized; those GPU values are invalid. Footprint values remain usable.
- Twenty repeated recordings with fixed non-4K AR input grew GPU allocation
  from 364 to 1,031 MiB. Twenty with fixed 4K input grew from 1,084 to 1,758 MiB.
  Median increment was about 34 MiB per completed recording in both phases.
- Empty recordings did not show the same step growth. After 30 seconds idle,
  GPU allocation remained about 1,680 MiB and footprint 2,227 MiB.
- This isolates growth to recordings that submit images, not format switching
  alone. Explicit RenderTexture.Release before Destroy is the next candidate,
  not yet a verified fix. Counters now query the default Metal device even before
  the first recording.

Evidence: `device-memory-fix-15pro.log`, `memory-isolation-results.json`,
`device-memory-isolate.log`, `memory-isolation-summary.json`.

Further isolation (`device-memory-owners.log`): weak references to native writer
and recording texture objects show writer count returns to zero after each
recording, but textures grow by about 3 per recording. Forty recordings leave
122 textures / 1,450,047,104 bytes alive even after the cooldown. Explicit
RenderTexture.Release did not prevent this. The next targeted candidate detaches
each external Texture2D from its native texture before destroying its wrapper.
This is a hypothesis until the same device's post-change counters are measured.

Detaching external Texture2D references also failed to stop accumulation
(`detach-slots-stop`). Destroying and recreating CVMetalTextureCache per recording
did release the native texture views: weak texture counts stayed at zero and GPU
allocation stayed roughly 290–453 MiB across 77 cases. However physical footprint
still rose from 843 to 3,302 MiB before signal 9. This is a partial resource-lifetime
improvement, **not a complete memory fix**. The generic SDK runner correctly
rejected the incomplete run (`recreate-cache-stop/validated-summary.json`).

Production source now scopes the cache to one recording and removes the ineffective
explicit RenderTexture.Release / UpdateExternalTexture(null) experiments. The
finishing callback no longer accesses that cache, avoiding a release/flush race.
The next build adds a zero-retained-recording-textures assertion to each stop case.
Remaining total-footprint growth still requires investigation; SDK promotion is
not ready.

SDK OS-background regression (`sdk-background`) completed but failed: first movie
was interrupted by AVFoundation -11847 / underlying -16121, dropped its last
pending frame, and lacked a moov atom; the after-resume movie decoded correctly.
The asynchronous GPU callback ran only after Unity resumed, too late to finalize
the first writer. Calling stop at focus loss and draining already-passed fences
also failed (`focus-background`); these unsuccessful candidate changes were
removed. A lifecycle-specific completion path that does not depend on suspended
Unity callbacks remains necessary. Do not add back global GPU waits to the normal
stop path without considering this distinction.

The SDK collector now continues past corrupt files and records all file failures
in its JSON summary, instead of aborting on the first ffprobe failure. The generic
runner's actual iOS background/resume control has been exercised on iPhone 15 Pro.
The moved native host harness re-ran successfully: 6/6 movies. The moved portable
audio queue tests also passed. Compact evidence is copied under
`Build_RecoveryArtifacts/20260922-sdk-regression` (ignored by Git).

The next ownership trace (`vm-owners-stop`) shows writer/adaptor/Metal texture
counts return to zero but AVAssetWriterInput objects accumulate by two per
recording (142 at the last reported case). An experimental Xcode-only build
wraps the nine native recorder bridge calls in autorelease pools; this has not
been promoted to production source. Diagnostic weak-object snapshots are also
scoped in their own autorelease pools to avoid extending observed lifetimes.
The experimental export still includes the unsuccessful focus-stop variant,
which has already been removed from C# source; do not treat this binary as the
final source build. Next evidence directory: `autorelease-stop`.

Autorelease experiment result: **91/91 device stop cases completed, 0 failures;
90 nonempty movies decoded, all exact submitted-frame counts, no file errors**.
Maximum A/V end-time delta was 0.087 s (not an onset/drift synchronization proof).
Writer, input, adaptor and recording-texture counts returned to zero. Footprint
was 850 MiB initially and about 967–1,014 MiB at later checkpoints, ending at
996 MiB instead of growing to jetsam. Same iPhone 15 Pro / iOS 27 device.

Measured same-binary stop emulation: non-4K median synchronous 13.56 ms vs
asynchronous 0.020 ms; 4K 13.51 ms vs 0.0247 ms. Asynchronous file finalization was
still 82 ms / 126 ms median, respectively; stopping the UI and making the file
available remain different events. This intentionally stresses a pending GPU
copy and is not a claim about sustained recording FPS.

The nine scoped native autorelease pools are now in production source, together
with per-recording cache ownership. Native host writer tests reran 6/6 successfully.
The SDK probe now also asserts that AVAssetWriterInput objects are released.
A fresh final-source device build/run is still needed (the passing experiment
included the subsequently removed focus-stop attempt). OS background recording
remains a separate failure; the audio redesign and full promotion gate remain open.

## Final memory source and OS-background fix (2026-09-22)

- `final-source-stop/validated-summary.json`: 91 scenarios, 90 movies, zero device failures and zero collector errors on iPhone 15 Pro / iOS 27. Native texture/input ownership assertions passed. This run used the production autorelease-pool and per-recording cache fixes without the discarded focus handler.
- `background-drain/validated-summary.json`: two movies and zero errors. `background-before-resume.json` confirms a valid movie copied while Unity remained in the background; the runner verified its SHA256 was unchanged after resume. The OS pause boundary waits only for this recorder's outstanding GPU requests; ordinary stop/disable/destroy remain asynchronous. Native finalization holds a UIKit background task.
- These results precede the new shared-audio writer integration. Rerun lifecycle and memory tests on the audio-integrated build; do not treat the prior binary as audio acceptance evidence.
