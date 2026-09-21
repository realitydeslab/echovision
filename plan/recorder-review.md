# Recording pipeline code review (quality + microphone sharing)

Scope: `HoloKitVideoRecorder.cs` (452 lines) + `VideoRecorder.mm` (452 lines) +
`MicrophoneAPI.cs` + `AudioProcessor.cs` + `DualAudioMixer.mixer`.

---

# 1. Microphone contention

## Conclusion: already solved by the original design

This is not a design flaw. The actual signal chain:

```
Microphone.Start(null, true, 1, 44100)
   -> microphoneAudioClip   (1 second ring buffer, continuously written by the system)
         |- audioSourceForAnalysis   volume=1    -> mixer "ForAnalysis" (-80 dB)
         |     -> AudioProcessor reads clip.GetData() directly to drive the visuals
         |- audiosourceForRecording  volume=0.5  -> mixer "Master" (0 dB)
               -> OnAudioFilterRead -> CommitSamples -> written into the video
```

Three decisions make it possible to both analyse and record the microphone:

1. **Two AudioSources share one AudioClip.** `Microphone.Start` returns a ring buffer; several
   AudioSources can play it simultaneously without contending. The microphone is opened once.

2. **The analysis path bypasses playback.** `AudioProcessor.cs:63-66` states the reason:
   "Read raw clip data directly to avoid mixer group volume attenuation". RMS/dB therefore see
   the original amplitude even though the mixer attenuates by 80 dB.

3. **`audiosourceForRecording.volume = 0.5f`, not 0.** The source comment explains:
   "Can not be set to zero cause that will cut off the data stream". Unity may skip audio
   processing at volume 0, which would starve `OnAudioFilterRead`.

Note that `OnAudioFilterRead` lives on the **Main Camera**, which carries an `AudioListener`
and no `AudioSource`, so it intercepts the **final global mix**, not one source.

## Two real bugs found

### BUG-1 (P0): feedback howl when not recording - FIXED

`HoloKitVideoRecorder.cs` used to read:
```csharp
if (!IsRecording) return;              // <-- returned before clearing
CommitSamples(data, channels);
System.Array.Clear(data, 0, data.Length);
```

Measured mixer values (`DualAudioMixer.mixer` snapshot):
```
ForAnalysis = -80 dB      <- attenuated, inaudible, fine
Master      =   0 dB      <- no attenuation at all
```

`audiosourceForRecording` (volume 0.5) runs through **Master at 0 dB**, so it really does reach
the speaker. While recording, `Array.Clear` suppressed it; when not recording nothing did, so
the microphone picked up its own output and self-oscillated. `MicFix_ForceToSpeaker()` and
`Force IOS Speakers When Recording: 1` raise the loop gain further.

Fixed by clearing unconditionally. Recording is unaffected because `CommitSamples` captures the
mix before the clear.

### BUG-2 (P0): pitch detection is probably broken - UNVERIFIED

RMS and the spectrum take different routes in `AudioProcessor.cs`:
```csharp
audioSource.clip.GetData(_samples, ...);                             // bypasses the mixer
audioSource.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris); // does not
```

Since the author deliberately bypassed "mixer group volume attenuation" for RMS, that
attenuation is real; but `GetSpectrumData` reads the output of `audioSourceForAnalysis`, which
is at **-80 dB** (amplitude x 1e-4). Peak picking uses an absolute threshold:

```csharp
private float Threshold = 0.02f;
if (!(_spectrum[i] > maxV) || !(_spectrum[i] > Threshold)) continue;
```

An attenuated spectrum can essentially never reach 0.02, so `maxN` stays 0 and `PitchValue`
stays 0.

**Falsifiable:** play a 300 Hz tone and read `pitchHz` while `vol > 0.02`. Reading ~300 refutes
this; a persistent 0 confirms it. The Editor session so far never exceeded the threshold
(max RMS 0.0297 against a required 0.0331), so it remains untested.

### Incidental finding
`Assets/Scenes/EchoVision.unity:238,2474` references `HolokitMicrophoneAPI, Assembly-CSharp`,
but the class is called `MicrophoneAPI`. This is the missing-script warning noted in the
project memory.

---

# 2. Recording correctness and quality

## Overall
The structure is standard and correct: `AVAssetWriter` plus
`AVAssetWriterInputPixelBufferAdaptor`, `WaitForEndOfFrame` timing, and
`AsyncGPUReadback.WaitAllRequests()` before teardown. The problems were concentrated in missing
encoder parameters and a few resource and ordering issues.

## P0 - quality: three encoder parameters missing

`VideoRecorder.mm`
```objc
NSDictionary* settings = @{
    AVVideoCodecKey:  AVVideoCodecTypeHEVC,
    AVVideoWidthKey:  @(width),
    AVVideoHeightKey: @(height) };
```

| Missing | Consequence |
|---|---|
| `AVVideoCompressionPropertiesKey` / `AVVideoAverageBitRateKey` | AVFoundation picks a default bitrate. **Measured 4.46 Mbps at 2768x1280** - high resolution, soft picture. The single largest quality problem. |
| `AVVideoColorPropertiesKey` | No colour tags written. The project is **Linear colour space**; ffprobe reports `color_space=unknown`, and players may interpret it differently. |
| `AVVideoExpectedSourceFrameRateKey` | The encoder does not know the target frame rate, so bitrate allocation and GOP structure are off. |

Suggested values for 2768x1280 at 60 fps:
```objc
NSDictionary* compression = @{
    AVVideoAverageBitRateKey:           @(24000000),
    AVVideoExpectedSourceFrameRateKey:  @(60),
    AVVideoMaxKeyFrameIntervalKey:      @(60),
    AVVideoProfileLevelKey:             AVVideoProfileLevelHEVCMain_AutoLevel,
};
NSDictionary* colorProps = @{
    AVVideoColorPrimariesKey:    AVVideoColorPrimaries_ITU_R_709_2,
    AVVideoTransferFunctionKey:  AVVideoTransferFunction_ITU_R_709_2,
    AVVideoYCbCrMatrixKey:       AVVideoYCbCrMatrix_ITU_R_709_2,
};
```

Supporting evidence: `HoloKitVideoRecorder.cs:427-428` already documents `videoBitRate` and
`audioBitRate` parameters that **do not exist in the function signature** - the original author
intended this and did not finish it.

## Fixed in this pass

- **Four `CFRelease` leaks** on the `appendAudioFrame` error paths.
- **Timestamp desync**: the queue was dequeued unconditionally while native could still drop the
  frame, shifting every later timestamp. The timestamp now travels with the frame.
- **`assert` compiled out in Release**, allowing an out-of-bounds read on a size mismatch;
  replaced with a runtime check.
- **CPU readback removed**: ~713 MB/s of GPU->CPU->GPU traffic at 60 fps, replaced by a
  zero-copy Metal blit into an IOSurface-backed `CVPixelBuffer`.
- **IOSurface / Metal compatibility attributes** added to the pixel buffer pool.
- **Inverted HDR condition** in `setVideoEnhancement`, which meant HDR was only ever requested
  on formats that cannot support it.

## Still open

- **4K background input**: `ARBackgroundVideoEnhancementManager` is not attached to the scene
  (`grep -c VideoEnhancement EchoVision.unity` = 0), and its
  `m_DisableVideoEnhancementInStereoMode` defaults to true, which switches 4K off in stereo
  mode. Without it the recording is an upscale rather than real detail.
- **Frame-rate gate** is hardcoded to 60 in `TimeQueue`.
- **`getUnityMatrix` leaks** 64 bytes per frame (`new float[16]`, never freed).

## Measured results (Editor, macOS)

| | CPU path | GPU path |
|---|---|---|
| Frames | 299 | 300 (matches `submitted=300 dropped=0`) |
| Resolution | 2768x1280 | 2768x1280 |
| Frame rate | 44.5 | 42.45 |
| Video bitrate | 7.78 Mbps | 4.46 Mbps |
| Audio | AAC 48 kHz stereo | AAC 48 kHz stereo |
| `color_space` | unknown | unknown |

A/V duration agreed within 0.08 s in both cases. The bitrate difference is **not** attributable
to the GPU path: the two recordings have different content, and this scene is largely dark and
low-detail, so adaptive bitrate drops sharply. The real issue is that no bitrate is specified
at all.
