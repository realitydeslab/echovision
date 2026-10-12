#!/usr/bin/env python3
"""Verify and collect a completed DeviceStateWalk run; requires devicectl and ffmpeg.

Every movie is also checked for image orientation (the walker's colour marker must be upright,
square and centred at each TAKE-FRAME time) and, with --voice-reference, for the phrase the runner
played during the take (see run_walker.py).
"""
import argparse
import json
import pathlib
import re
import subprocess
import sys
import time

sys.dont_write_bytecode = True
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

from recording_checks import read_marker, voice_match  # noqa: E402

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("log", type=pathlib.Path)
parser.add_argument("output", type=pathlib.Path)
parser.add_argument("--device", default="00008130-000848EA38298D3A")
parser.add_argument("--voice-reference", type=pathlib.Path,
                    help="audio of the phrase played on each VOICE-CUE; without it voice is reported untested")
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
log = args.log.read_text()
errors = []
if "[STATE] COMPLETE failures=0" not in log:
    errors.append("State walker did not complete successfully")
for marker in ["[STATE] FAIL", "Exception:", "[Recorder] GPU copy could not", "Failed to store screen shot"]:
    if marker in log:
        errors.append(marker)

def receive(source, destination):
    # A copy right after the console session ends can fail once while iOS tears the app down.
    for attempt in range(2):
        result = subprocess.run(["xcrun", "devicectl", "device", "copy", "from", "--device", args.device,
                                 "--domain-type", "appDataContainer", "--domain-identifier", "org.realitydeslab.echovision",
                                 "--source", source, "--destination", str(destination)], capture_output=True, text=True)
        if result.returncode == 0:
            return
        time.sleep(3)
    raise RuntimeError(f"could not copy {source} from the phone: {result.stderr.strip()[-300:]}")

RATE = 16000


def pcm(path):
    """Mono float PCM at RATE from any file ffmpeg reads (the movie's audio track, the phrase)."""
    raw = subprocess.check_output(["ffmpeg", "-v", "error", "-i", str(path), "-vn", "-ac", "1", "-ar", str(RATE),
                                   "-f", "f32le", "-"])
    return np.frombuffer(raw, np.float32)


def check_orientation(i, movie, duration):
    """The marker must read upright, square and centred at every stable state of the take."""
    results = []
    for t, label, orientation, mode in re.findall(
            rf"\[STATE\] TAKE-FRAME take={i} t=([\d.]+) label=(\S+) orientation=(\S+) mode=(\S+)", log):
        frame_path = movie.with_name(f"{movie.stem}-{label}.png")
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-ss", str(min(float(t), duration - .1)), "-i", str(movie),
                        "-frames:v", "1", "-vf", "scale=540:-2", str(frame_path)], check=True)
        reading = read_marker(np.asarray(Image.open(frame_path).convert("RGB")))
        result = dict(t=float(t), label=label, orientation=orientation, mode=mode, frame=frame_path.name)
        if reading is None:
            errors.append(f"Movie {i} {label}: orientation marker not found")
        else:
            result.update(layout=reading.layout, aspect=round(reading.aspect, 3),
                          center=[round(c, 3) for c in reading.center])
            if reading.layout != "upright":
                errors.append(f"Movie {i} {label}: image is {reading.layout}")
            if abs(reading.aspect - 1) > .12:
                errors.append(f"Movie {i} {label}: image stretched (marker aspect {reading.aspect:.2f})")
            if max(abs(c - .5) for c in reading.center) > .1:
                errors.append(f"Movie {i} {label}: image off-centre (marker at {reading.center[0]:.2f}, {reading.center[1]:.2f})")
        results.append(result)
    if not results:
        errors.append(f"Movie {i}: no TAKE-FRAME logged, orientation untested")
    return results


def check_voice(i, movie):
    if f"[STATE] VOICE-CUE take={i}" not in log:
        errors.append(f"Movie {i}: no VOICE-CUE logged")
        return "untested"
    if args.voice_reference is None:
        return "untested"
    match = voice_match(pcm(movie), pcm(args.voice_reference), RATE)
    if not match.heard:
        errors.append(f"Movie {i}: phrase not heard in the microphone track (score {match.score:.2f}, SNR {match.snr_db:.1f} dB)")
    return dict(heard=match.heard, score=round(match.score, 3), offset=round(match.offset, 2), snr_db=round(match.snr_db, 1))


movies = []
# One recorder START per movie, in order; fps is the movie's frame rate (HD 60, 4K camera 30).
announced = [int(fps) for fps in re.findall(r"\[Recorder\] START \S+ fps=(\d+)", log)]
for i, (frames, dropped, path) in enumerate(re.findall(r"\[STATE\] MOVIE frames=(\d+) dropped=(\d+) path=([^\r\n]+)", log)):
    destination = args.output / f"movie-{i}.mp4"
    receive("Library/Caches/" + pathlib.Path(path).name, destination)
    info = json.loads(subprocess.check_output(["ffprobe", "-v", "error", "-count_frames", "-show_streams", "-of", "json", str(destination)]))
    video = next(s for s in info["streams"] if s["codec_type"] == "video")
    audio = next(s for s in info["streams"] if s["codec_type"] == "audio")
    if int(dropped) != 0:
        errors.append(f"Movie {i}: recorder dropped {dropped} frames")
    if int(video["nb_read_frames"]) != int(frames):
        errors.append(f"Movie {i}: decoded frame count differs from submissions")
    measured_fps = int(video["nb_read_frames"]) / float(video["duration"])
    expected_fps = announced[i] if i < len(announced) else None
    if expected_fps and measured_fps < .9 * expected_fps:
        errors.append(f"Movie {i}: {measured_fps:.1f} fps, recorder announced {expected_fps}")
    delta = abs(float(video["start_time"]) + float(video["duration"]) - float(audio["start_time"]) - float(audio["duration"]))
    if delta > .2:
        errors.append(f"Movie {i}: audio/video end-time difference {delta:.3f}s")
    subprocess.run(["ffmpeg", "-v", "error", "-xerror", "-err_detect", "explode", "-i", str(destination),
                    "-enc_time_base:v", "1:600", "-fps_mode", "passthrough", "-f", "null", "-"], check=True, capture_output=True)
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-ss", "1", "-i", str(destination), "-frames:v", "1",
                    "-vf", "scale=900:-1", str(destination.with_suffix(".png"))], check=True)
    movies.append(dict(file=destination.name, frames=int(frames), dropped=int(dropped),
                       width=video["width"], height=video["height"], video_duration=video["duration"],
                       audio_duration=audio["duration"], video_start=video["start_time"], audio_start=audio["start_time"], end_delta=delta, sample_rate=audio["sample_rate"], channels=audio["channels"],
                       fps=round(measured_fps, 1), announced_fps=expected_fps,
                       orientation=check_orientation(i, destination, float(video["duration"])),
                       voice=check_voice(i, destination)))
if len(movies) != 4:
    errors.append(f"Expected four movies, got {len(movies)}")
for path in set(re.findall(r"\[STATE\] SCREENSHOT path=([^\r\n]+)", log)):
    name = pathlib.Path(path).name
    try:
        receive("Documents/" + name, args.output / name)
    except RuntimeError as error:
        errors.append(str(error))
report = dict(movies=movies, errors=errors,
              visual_alignment="Marker orientation is checked; AR registration still requires inspection of screenshots and movies")
(args.output / "results.json").write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps(report, indent=2), flush=True)
raise SystemExit(bool(errors))
