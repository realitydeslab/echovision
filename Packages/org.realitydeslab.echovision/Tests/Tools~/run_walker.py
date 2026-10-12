#!/usr/bin/env python3
"""Run the EchoVision device walker, speak a phrase into each recording, then check the movies.

Needs the iOS Recovery Tests build installed on the phone, devicectl, ffmpeg and macOS `say`.
With --voice the phrase plays through the Mac's current output at its current volume, over and
over from the walker's START to COMPLETE (about every 1.7 s), so every 3 s take holds at least one
whole phrase however late the console delivers the take's cue. This script never changes the
volume and stops if the output is muted. Keep the phone still and near the Mac speakers.
Without --voice nothing is played and the voice checks report untested.

The console session ends when the walker logs COMPLETE, and devicectl terminates the app with it.
"""
import argparse
import pathlib
import subprocess
import sys
import threading
import time

BUNDLE = "org.realitydeslab.echovision"
HERE = pathlib.Path(__file__).resolve().parent

parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
parser.add_argument("--device", required=True, help="UDID of the phone")
parser.add_argument("--output", required=True, type=pathlib.Path)
parser.add_argument("--voice", action="store_true", help="play the phrase through the Mac speakers on each take")
parser.add_argument("--phrase", default="Echo vision test")
parser.add_argument("--timeout", type=float, default=300, help="seconds to wait for COMPLETE")
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)

phrase = args.output / "phrase.aiff"
if args.voice:
    muted = subprocess.check_output(["osascript", "-e", "output muted of (get volume settings)"], text=True).strip()
    if muted == "true":
        sys.exit("The Mac output is muted, so the phone cannot hear the phrase. Unmute it, or run without --voice.")
    subprocess.run(["say", "-o", str(phrase), args.phrase], check=True)

log_path = args.output / "device.log"
console = subprocess.Popen(["xcrun", "devicectl", "device", "process", "launch", "--device", args.device,
                            "--terminate-existing", "--console", BUNDLE],
                           stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, errors="replace")
deadline, complete = time.time() + args.timeout, False
stop_speaking = threading.Event()


def speak():
    while not stop_speaking.is_set():
        subprocess.run(["afplay", str(phrase)])
        stop_speaking.wait(.5)


stamp = lambda: time.strftime("%H:%M:%S")
with log_path.open("w", buffering=1) as log:  # line-buffered: an interrupted run keeps its log
    # The app logs microphone analysis every 0.1 s, so lines keep arriving to check the deadline.
    for line in console.stdout:
        log.write(line)
        if "[STATE] START" in line and args.voice:
            print(stamp(), "walker started -> speaking the phrase until COMPLETE", flush=True)
            threading.Thread(target=speak, daemon=True).start()
        elif "[STATE] VOICE-CUE" in line or "[STATE] FAIL" in line or "[STATE] COMPLETE" in line:
            print(stamp(), line.strip(), flush=True)
        if "[STATE] COMPLETE" in line:
            complete = True
            break
        if time.time() > deadline:
            break
stop_speaking.set()
timed_out = time.time() > deadline
console.terminate()
console.wait(10)
if not complete:
    if not timed_out:  # the console ended by itself: usually devicectl could not launch the app
        errors = [line.strip() for line in log_path.read_text().splitlines() if "ERROR" in line or "Reason" in line]
        sys.exit("The app stopped before the walker completed:\n  " + "\n  ".join(errors[:3] or [f"see {log_path}"]))
    sys.exit(f"The walker did not complete within {args.timeout:.0f} s; see {log_path}")

command = [sys.executable, str(HERE / "device_artifacts.py"), str(log_path), str(args.output / "artifacts"),
           "--device", args.device]
if args.voice:
    command += ["--voice-reference", str(phrase)]
sys.exit(subprocess.run(command).returncode)
