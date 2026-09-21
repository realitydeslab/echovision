# EchoVision — on-device test plan (iPhone 15 Pro, iOS 27)

Device: **`00008130-000848EA38298D3A` — "Holo iPhone 15 I", iPhone 15 Pro (`iPhone16,1`), iOS 27.0**
State `connected`, paired, Developer Mode enabled. Supported by HoloKit
(`iOSPhoneModelList.asset` has `iPhone16,1` with real calibration data), so stereoscopic mode works.
NOT the iPhone 17 Pro (`00008150-...`), which is registered but unplugged — see `test-device` memory.
Bundle: `org.realitydeslab.echovision`
Mac audio out: **MacBook Pro Speakers** (confirmed default; `BlackHole 2ch` present but NOT
default, so `afplay` produces real audible sound).

---

## BLOCKERS — none of this can run yet

| # | Blocker | Evidence |
|---|---------|----------|
| ~~B1~~ | **RESOLVED — wrong device.** A phone was connected all along; I was querying the iPhone 17 Pro, which is unplugged. The real target is the iPhone 15 Pro above, `Device State: connected`. | `ioreg -p IOUSB` shows `iPhone@02200000`; `devicectl` shows `connected`. Note `system_profiler SPUSBDataType` returns EMPTY under this sandbox and must never be used to conclude a device is absent. |
| B2 | **No app to install** | `Build/` does not exist; the Unity iOS export is blocked by the visionOS `LicenseCheck` throw. |
| B3 | **`-ld64` link failure** | `HoloKitBuildProcessor.cs:31`; verified to fail on Xcode 27. Must be removed before any build links. |

**A usable baseline already exists on the device:** `EchoVision` `org.realitydeslab.echovision`
**v0.2.4** is installed (pre-upgrade build; ProjectSettings is now at `bundleVersion 0.3.0`).
Tests A and B can therefore run **immediately** against 0.2.4, before B2/B3 are cleared. That
establishes whether the audio trigger and recording work at all, separating real regressions
from pre-existing behaviour once the Unity 6.3 / iOS 27 build lands.

---

## Why this app is unusually testable

`Assets/Scripts/AudioProcessor.cs:43` logs **every 0.1 s, ungated by any verbose flag**:
```
[Audio] RMS=0.041230 dB=-7.7 vol=0.115 pitchHz=301 pitchNorm=0.502
```
So pass/fail is read off the console rather than judged by eye. Capture with:
```bash
D=00008130-000848EA38298D3A
xcrun devicectl device process launch --device $D --console org.realitydeslab.echovision
```

## The exact trigger threshold (derived from source, not guessed)

```
AudioProcessor.cs:54   RefValue = 0.1
AudioProcessor.cs:83   DbValue  = 20*log10(RMS / 0.1)
AudioProcessor.cs:36   AudioVolume = Remap(DbValue, -10, 10, 0, 1, clamped)
SoundWaveEmitter.cs:59 emitVolumeThreshold = 0.02
SoundWaveEmitter.cs:104 if (audioProcessor.AudioVolume > emitVolumeThreshold) -> emit
```
Solving: `vol > 0.02` <=> `dB > -9.6` <=> **RMS > 0.0331**.
That is a moderately loud sound — normal speech at ~30 cm, or Mac speakers near max.

Pitch path: `audioPitch = Remap(PitchValue, 0, 600, 0, 1)`, so a **300 Hz** tone should report
`pitchHz≈300, pitchNorm≈0.50` — a second, independent correctness check.

---

## TEST A — physical sound triggers the sound-wave emission

**The signal path must be acoustic end to end: speaker or hands -> air -> the phone's own
built-in mic.** Nothing digital is injected into the app. Two guards below exist specifically
to prove that, because a silently-digital path would make every result meaningless.

### A0. Prove the path is really acoustic (run BEFORE anything else)

```bash
# 1. Output must be the physical speakers. If this says BlackHole 2ch, the sound is going into a
#    virtual loopback and NEVER reaches the air — the whole test would be a false negative.
system_profiler SPAudioDataType | grep -B4 "Default Output Device: Yes"
#    Expect: "MacBook Pro Speakers"

# 2. Mac must not be muted, and should be loud.
osascript -e 'output volume of (get volume settings)'
osascript -e 'set volume output volume 85'
osascript -e 'output muted of (get volume settings)'   # must be false
```

**A0-c — mic-occlusion negative control. This is the decisive proof.**
With a loud tone playing continuously, cover the phone's bottom mic port firmly with a finger.
`RMS` in the `[Audio]` log must drop sharply and `vol` must fall below 0.02, then recover when
uncovered. If RMS does *not* change, the app is not listening to the built-in mic and every
other result in Test A is invalid.

### A1. Real physical sounds — the actual use case
EchoVision is echolocation-style, so the sounds that matter are broadband transients, not tones.
These are produced by hand, in the room, ~30 cm from the phone:

| Sound | Why it matters | Expect |
|---|---|---|
| Hand clap | sharp broadband transient, the canonical trigger | `vol` spikes well past 0.02, one wave burst per clap |
| Finger snap | quieter transient — probes the threshold edge | near-threshold; may or may not fire |
| Spoken voice ("hey") | sustained broadband | `vol > 0.02` for the word's duration |
| Cane tap / knock on desk | low-frequency impact | low `pitchHz`, `vol` spike |
| Room silence (hold still) | negative control | `vol <= 0.02`, no new waves |

Count claps against wave bursts — **one clap should yield one burst**. Repeated claps ~1 s apart
check that the emitter re-arms rather than latching on.

### A2. Speaker-generated tones — repeatable calibration, still acoustic
Played out of the MacBook speakers into the air. Purpose is a *reproducible* amplitude and a
known frequency, which hand claps cannot give. Phone ~20 cm from the speakers, facing them.

```bash
ffmpeg -f lavfi -i "sine=frequency=300:duration=3" -ar 44100 -ac 1 -y /tmp/tone300.wav
afplay -v 1.0 /tmp/tone300.wav          # expect pitchHz ~300, vol > 0.02

# threshold sweep — find where real-world emission actually starts
for v in 0.1 0.2 0.4 0.6 0.8 1.0; do echo "vol=$v"; afplay -v $v /tmp/tone300.wav; sleep 1; done

# distance sweep at fixed volume — 10cm / 30cm / 1m / 2m
```

### Pass criteria

| Check | Pass criterion |
|---|---|
| A0-a | default output is MacBook Pro Speakers, not BlackHole; not muted |
| A0-c | covering the mic collapses RMS; uncovering restores it |
| A1 mic alive | `[Audio]` lines at ~10/s, RMS varying with room noise, never pinned at 0 |
| A1 clap | each clap drives `vol > 0.02` and emits exactly one wave burst |
| A1 silence | at rest `vol <= 0.02`, no new waves |
| A2 threshold | tone crosses `vol > 0.02` (`RMS > 0.0331`) at some volume/distance; record which |
| A2 pitch | `pitchHz` reads 290-310 for the 300 Hz tone |
| A2 falloff | `vol` decreases monotonically as distance increases |

Note: `MicrophoneAPI.cs:50` uses `Microphone.Start(null, ...)` — the **default** input. If AirPods
or any Bluetooth headset are paired and connected, the phone may capture from those instead of
the built-in mic, and the acoustic test silently measures the wrong microphone. Disconnect all
Bluetooth audio before testing; A0-c will also catch this.

---

## TEST B — recording captures the sound

Path: `HoloKitVideoRecorder.cs:266` writes to `Application.temporaryCachePath`
(= `<container>/Library/Caches` on iOS); `VideoRecorder.mm:413` then calls
`UISaveVideoAtPathToSavedPhotosAlbum` to copy it into Photos.

1. Start recording in-app.
2. Make **physical sound in the room** for ~5 s — same acoustic path as Test A, no digital
   injection. Use a fixed, countable script so the recording can be checked against it:
   **3 claps ~1 s apart, then say "one two three", then 3 s of the 300 Hz tone from the
   speakers.** The claps give sharp transients that are unmistakable in a waveform; the tone
   gives a known frequency to look for in the spectrum.
3. Stop recording.
4. Pull the file off the device rather than trusting Photos:
```bash
xcrun devicectl device info files --device $D \
  --domain-type appDataContainer --domain-identifier org.realitydeslab.echovision \
  --username mobile / 2>/dev/null | grep -i -E "\.mp4|\.mov"

xcrun devicectl device copy from --device $D \
  --domain-type appDataContainer --domain-identifier org.realitydeslab.echovision \
  --source Library/Caches/<file>.mp4 --destination ./recording.mp4
```
5. Verify objectively:
```bash
ffprobe -v error -show_streams -select_streams a recording.mp4   # an audio stream must exist
# mean_volume must NOT be -inf / near-silent:
ffmpeg -i recording.mp4 -af volumedetect -f null - 2>&1 | grep -E "mean_volume|max_volume"
# and the 300 Hz peak should be visible:
ffmpeg -i recording.mp4 -lavfi showspectrumpic=s=640x480 -y spectrum.png
```

| Check | Pass criterion |
|---|---|
| B1 | file exists and is non-trivial in size |
| B2 | container has BOTH a video and an audio stream |
| B3 | `mean_volume` is finite and well above silence |
| B4 | spectrum shows energy at ~300 Hz, and 3 clap transients are visible in the waveform |
| B5 | video and audio durations match within ~0.2 s (A/V sync) |

Watch for: `ProjectSettings.asset` has `Force IOS Speakers When Recording: 1`, and
`Assets/Plugins/iOS/MicFix.mm:4` `MicFix_ForceToSpeaker()` forces the output route to the
speaker. Harmless here because the Mac makes the sound, but if the app ever plays audio during
recording it will feed back into its own mic.

---

## TEST C - recording: captures video, captures audio, high resolution

The third function is recording. Three things need to hold: **a file is produced**, **the audio
track has content**, and **the resolution is high enough**. Test B already covers the first two;
this section handles resolution, because the source has three independent layers and two of them
are currently wrong.

### The three layers of resolution (verify separately or you will misjudge it)

**L1 - container resolution: tied to the screen, not configurable**
`HoloKitVideoRecorder.cs:270-271`
```csharp
var width  = Screen.width  & ~15;   // iPhone 15 Pro: 2556 -> 2544
var height = Screen.height & ~15;   //                1179 -> 1168
```
`& ~15` rounds down to a multiple of 16, required by the HEVC hardware encoder. This layer is
fine.

**L2 - encoder bitrate: not set at all, and this is the real problem**
`VideoRecorder.mm`
```objc
NSDictionary* settings = @{
    AVVideoCodecKey:  AVVideoCodecTypeHEVC,
    AVVideoWidthKey:  @(width),
    AVVideoHeightKey: @(height) };
```
No `AVVideoCompressionPropertiesKey` / `AVVideoAverageBitRateKey`. AVFoundation falls back to a
default that measured **4.46 Mbps at 2768x1280** in the Editor - high resolution, soft picture.
Bitrate must be measured; the resolution number alone proves nothing.

**L3 - AR background input: 4K enhancement not enabled**
The SDK ships `ARBackgroundVideoEnhancementManager` ("Enable ARKit's 4K HDR background video"),
backed by `recommendedVideoFormatFor4KResolution` in `HoloKitARKitNativeProvider.mm`. The scene
does not have this component attached (`grep -c VideoEnhancement EchoVision.unity` = 0), so the
ARKit camera input is not 4K and the recording is an upscale rather than real detail.

Also note `m_DisableVideoEnhancementInStereoMode = true` by default: **stereo mode actively
switches 4K off**, so attaching the component is not sufficient if recording happens in stereo.

### Verification steps
Using `recording.mp4` pulled in Test B:
```bash
# C1/C2 resolution and codec
ffprobe -v error -select_streams v:0 \
  -show_entries stream=width,height,codec_name,avg_frame_rate,bit_rate \
  -of default=noprint_wrappers=1 recording.mp4

# C3 actual bitrate (ffprobe often reports N/A for HEVC; fall back to size/duration)
ffprobe -v error -show_entries format=duration,bit_rate,size \
  -of default=noprint_wrappers=1 recording.mp4

# C4 audio track
ffprobe -v error -select_streams a:0 \
  -show_entries stream=codec_name,sample_rate,channels,bit_rate \
  -of default=noprint_wrappers=1 recording.mp4

# C5 is the picture actually sharp - extract a frame and look
ffmpeg -v error -i recording.mp4 -vf "select=eq(n\,60)" -vframes 1 -y frame.png
```

| Check | Pass criterion |
|---|---|
| C1 resolution | width x height equals `Screen.width/height & ~15` |
| C2 codec | `codec_name = hevc`, frame rate near 30 or 60 |
| C3 bitrate | for HEVC at this resolution, **below ~8 Mbps counts as too low**; needs an explicit `AVVideoAverageBitRateKey` |
| C4 audio | `codec_name = aac`, `sample_rate` matches `AudioSettings.outputSampleRate`, `channels = 2` |
| C5 quality | extracted frame shows no obvious blocking or smearing; AR background detail is sharp rather than upscaled |
| C6 duration | video and audio durations agree within 0.2 s |

### Suggested changes if C3 or C5 fail (not yet applied)
1. Add a bitrate to `settings` in `VideoRecorder.mm`:
```objc
AVVideoCompressionPropertiesKey: @{ AVVideoAverageBitRateKey: @(24000000) }  // 24 Mbps
```
2. Attach `ARBackgroundVideoEnhancementManager` to the scene and enable it, for genuine 4K input.
3. To keep 4K in stereo mode, set `m_DisableVideoEnhancementInStereoMode` to false. This raises
   GPU and thermal load, so measure the frame rate afterwards.

---

## Order of execution
1. Fix B3 (`-ld64`) — one line.
2. Clear B2 (visionOS licence) — `unity auth login`, or drop the package for the iOS build.
3. Build, install, launch with `--console`.
4. Reconnect the phone (B1) and confirm `devicectl` shows it `available`.
5. Run A, then B, then C.

---

## RUN LOG — 2026-09-21, first attempt

**Result: not completed. Blocked at app launch (B4, new).**

Passed:
- Device `00008130-000848EA38298D3A` (iPhone 15 Pro) — `connected`
- A0-a Mac default output = **MacBook Pro Speakers** (physical, not BlackHole)
- A0-b volume set to 85, `output muted = false`
- Test tones generated: `tone300.wav` (300 Hz / 4 s), `silence.wav`

Failed:
```
xcrun devicectl device process launch --device $D --console org.realitydeslab.echovision
ERROR: CoreDeviceError 10002 / FBSOpenApplicationErrorDomain error 3
  "Unable to launch org.realitydeslab.echovision because it has an invalid code signature,
   inadequate entitlements or its profile has not been explicitly trusted by the user."
```
Zero `[Audio]` lines captured — the app never started.

### B4 — developer certificate not trusted on this device (device-wide)
Discriminated by launching two other dev-signed apps:

| App | Result |
|---|---|
| `org.realitydeslab.echovision` (0.2.4) | same Security / RequestDenied failure |
| `org.realitydeslab.gravfield` | same |
| `com.holoi.holokit.becoming-bats` | same |

All three fail identically, so this is **not** an EchoVision problem — the developer certificate
backing every sideloaded build on this phone is untrusted or its profiles have expired.

**Fix (requires on-device UI, cannot be done over USB):**
Settings > General > VPN & Device Management > select the developer certificate > Trust.

If the profiles have **expired** rather than merely being untrusted — likely, since these are old
builds and EchoVision here is 0.2.4 — trusting will not help and a freshly signed install is
required. That re-couples the baseline test to B2 (visionOS licence) and B3 (`-ld64`).

## RUN LOG — 2026-09-21, second attempt

**Result: still blocked. Root cause found; earlier advice was wrong.**

Two things changed since attempt 1:

1. `org.realitydeslab.echovision` (0.2.4) is **no longer installed** — the launch now fails with
   `"The requested application ... is not installed"` instead of a security error. It was
   present in the app listing during attempt 1 and is absent now.
2. Trust is still broken. Probing the remaining variant `com.holoi.holokit.becoming-bats` gives
   the same `Security / RequestDenied` failure as before.

**Decisive check:**
```
$ xcrun devicectl device profile list --device 00008130-000848EA38298D3A
No profiles found
```

**There are ZERO provisioning profiles on the device.** So "go to Settings > VPN & Device
Management and trust the certificate" — my advice after attempt 1 — cannot work: there is no
entry there to tap. The profiles are gone or expired, not merely untrusted. Every sideloaded app
on this phone is now unlaunchable for the same reason.

### Consequence
The 0.2.4 baseline is dead. There is no way to test anything on this device without
**installing a freshly signed build**, which re-couples the whole test to B2 and B3.

### B3 — FIXED 2026-09-21
`-ld64` removed from `HoloKitBuildProcessor.cs`:
```diff
-                project.AddBuildProperty(unityFrameworkTargetGuid, "OTHER_LDFLAGS", "-ld64");
```
Backup at `scratchpad/backup/HoloKitBuildProcessor.cs.bak`. Reverting is a one-line re-add.

### Critical path is now unambiguous
1. ~~B3 `-ld64`~~ — done.
2. **B2 visionOS licence** — the only remaining blocker, and the only one I cannot clear myself.
   `unity auth login` (Pro seat), or temporarily drop `com.unity.xr.visionos` for the iOS build.
3. Build -> install (this also lays down a valid provisioning profile, fixing B4 as a side
   effect) -> launch with `--console` -> run Tests A and B.
