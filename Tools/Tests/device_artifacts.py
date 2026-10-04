#!/usr/bin/env python3
"""Verify and collect a completed DeviceStateWalk run; requires devicectl and ffmpeg."""
import argparse
import json
import pathlib
import re
import subprocess

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("log", type=pathlib.Path)
parser.add_argument("output", type=pathlib.Path)
parser.add_argument("--device", default="00008130-000848EA38298D3A")
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
    subprocess.run(["xcrun", "devicectl", "device", "copy", "from", "--device", args.device,
                    "--domain-type", "appDataContainer", "--domain-identifier", "org.realitydeslab.echovision",
                    "--source", source, "--destination", str(destination)], check=True, capture_output=True)

movies = []
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
    delta = abs(float(video["start_time"]) + float(video["duration"]) - float(audio["start_time"]) - float(audio["duration"]))
    if delta > .2:
        errors.append(f"Movie {i}: audio/video end-time difference {delta:.3f}s")
    subprocess.run(["ffmpeg", "-v", "error", "-xerror", "-err_detect", "explode", "-i", str(destination),
                    "-enc_time_base:v", "1:600", "-fps_mode", "passthrough", "-f", "null", "-"], check=True, capture_output=True)
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-ss", "1", "-i", str(destination), "-frames:v", "1",
                    "-vf", "scale=900:-1", str(destination.with_suffix(".png"))], check=True)
    movies.append(dict(file=destination.name, frames=int(frames), dropped=int(dropped),
                       width=video["width"], height=video["height"], video_duration=video["duration"],
                       audio_duration=audio["duration"], video_start=video["start_time"], audio_start=audio["start_time"], end_delta=delta, sample_rate=audio["sample_rate"], channels=audio["channels"]))
if len(movies) != 6:
    errors.append(f"Expected six movies, got {len(movies)}")
for path in set(re.findall(r"\[STATE\] SCREENSHOT path=([^\r\n]+)", log)):
    name = pathlib.Path(path).name
    receive("Documents/" + name, args.output / name)
report = dict(movies=movies, errors=errors, visual_alignment="Requires inspection of screenshots and decoded movies")
(args.output / "results.json").write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps(report, indent=2), flush=True)
raise SystemExit(bool(errors))
