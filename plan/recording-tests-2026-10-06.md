# Task Plan: recording tests for image orientation and voice (2026-10-06)

## Goal
Every movie the EchoVision device walker records is checked automatically for (1) image
orientation: upright, not mirrored, not stretched, including the take that rotates mid-recording,
and (2) voice: a known spoken phrase played during the take is present in the microphone track.

## Agreed seams (user, 2026-10-06)
1. Host, unit-tested without a phone (`Tests/Tools~/recording_checks.py`):
   - `read_marker(frame)` -> layout (upright / rotated / mirrored) and aspect of the test marker
   - `voice_match(mic, phrase, rate)` -> score, offset, SNR
2. Device: the walker draws a 2x2 marker (red, green / blue, yellow) into the recorded camera and
   logs `[STATE] VOICE-CUE take=N` at each take start and `[STATE] TAKE-FRAME` at stable states;
   a runner plays a `say` phrase on each cue through the Mac speakers (opt-in `--voice`, never
   changes the volume); `device_artifacts.py` gates every movie on both checks.

## Facts
- EchoVision records Environment + Default processing: one mono audio track = the microphone.
- Phrase "Echo vision test" = 1.12 s (`say`, 22.05 kHz). Takes are about 3 s.
- numpy 2.5, scipy 1.17, PIL 12, ffmpeg available.
- Mac output: MacBook Pro Speakers, volume 60, **muted** (2026-10-06 22:35). Needs the user.
- Connected phone: Holo iPhone 15 Crack B only. Confirm before installing.

## Phases
- [ ] H1 red/green: `read_marker` upright vs rotated/mirrored (8 dihedral transforms), no marker
- [ ] H2 red/green: `read_marker` aspect (uniform vs stretched)
- [ ] H3 red/green: `voice_match` phrase through channel + noise vs noise / other speech
- [ ] D1 walker: marker on the capture camera, VOICE-CUE and TAKE-FRAME logs
- [ ] D2 `device_artifacts.py`: per-movie orientation + voice gates; `run_walker.py` runner
- [ ] D3 EditMode + Recovery Tests export + xcodebuild
- [ ] D4 device run (phone + speaker decision), negative check, reinstall Release
- [ ] D5 docs, commit

## Progress
- H1-H3 green: 9/9 host tests. Offline H.264/AAC + real `say` check: phrase 0.72 heard, other 0.25, silence 0.09.
- D1/D2 done; Recovery Tests export + signed xcodebuild succeeded (2026-10-06).
- 2026-10-06 run on Crack B interrupted by the user after 5 of 6 cues; device.log empty (runner
  buffered the log) -> runner now line-buffered. Crack B still has the EchoVision test build
  instead of HoloKit Audio Lab (restore pending; phone not reachable since).
- 2026-10-12: user: run on iPhone 15 A (over the network: "available (paired)"). Mac output
  unmuted at 51 before the run (not changed by me); restore that state after.

- 2026-10-12 01:14 run 2 on iPhone 15 A (after the user re-trusted the developer): walker
  `COMPLETE failures=0`; checker: orientation upright in 7/8 frames, movie 0 marker missed under
  sound-wave bloom (green/yellow saturation 0.38); voice not heard in 6/6 (phrase late or absent:
  takes 3-5 nothing above the floor; cue latency). Fixes, test-first: colour classification by
  dominant channel + 2x2 patch selection (all 8 real frames upright, aspect 0.98-1.02); 70 ms
  median against handling clicks (phrase 0.83, other speech <= 0.30); runner loops the phrase
  from START to COMPLETE instead of per cue.
- User decision 2026-10-12: Stereo keeps EchoVision's own Mono button, no SDK stereo bar
  (`m_StereoBarItems: 0`); walker checks it and its 6th take presses the on-screen REC in Stereo.

## Errors Encountered
- 2026-10-12 01:10 iOS refused to launch any app from the team ("profile has not been explicitly
  trusted"); certificate unchanged on the Mac. The user re-trusted on the phone; launch worked after.
- Room fixture had reverb energy 36x the direct sound, so "clean" cases were at -10 dB SNR. Fixed to DRR +3 dB.
- Interrupted run lost its log (block-buffered file). Fixed with line buffering.
