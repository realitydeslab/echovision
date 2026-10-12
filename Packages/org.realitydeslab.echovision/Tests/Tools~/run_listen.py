#!/usr/bin/env python3
"""Listen session: what EchoVision's audio analysis hears while a person talks, claps and taps.

Needs the iOS Recovery Tests build on the phone. Sends the one-shot mode "listen" (the walker does
not run), launches the app, which shows the person what to do (QUIET, TALK NORMALLY, TALK LOUDLY,
CLAP, TAP THE PHONE, QUIET) and logs each phase, records until [LISTEN] DONE, then prints the
levels of each phase and writes listen.png: RMS over time against the current wave trigger
(RMS 0.0331: AudioProcessor RefValue 0.1, emit at 0.02).
The console session ends at the end and devicectl terminates the app with it.
"""
import argparse
import pathlib
import re
import subprocess
import sys
import time

import numpy as np

BUNDLE = "org.realitydeslab.echovision"
TRIGGER_RMS = 0.0331

parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
parser.add_argument("--device", required=True)
parser.add_argument("--output", required=True, type=pathlib.Path)
parser.add_argument("--mode", default="listen", choices=["listen", "listen-mics", "listen-gravity"],
                    help="listen: quiet/talk/loud/clap/tap; listen-mics: talk into each edge, then music; "
                         "listen-gravity: poses for the upside-down sleep")
parser.add_argument("--timeout", type=float, default=120, help="seconds to wait for DONE")
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)

mode = args.output / "recorder-test-mode.txt"
mode.write_text(args.mode + "\n")
subprocess.run(["xcrun", "devicectl", "device", "copy", "to", "--device", args.device, "--domain-type",
                "appDataContainer", "--domain-identifier", BUNDLE, "--source", str(mode),
                "--destination", "Documents/recorder-test-mode.txt"], check=True, capture_output=True)

console = subprocess.Popen(["xcrun", "devicectl", "device", "process", "launch", "--device", args.device,
                            "--terminate-existing", "--console", BUNDLE],
                           stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, errors="replace")
samples, emits, phases, poses, ready, done = [], [], [], [], None, False
with (args.output / "device.log").open("w", buffering=1) as log:
    for line in console.stdout:
        now = time.time()
        log.write(line)
        if ready is None and "[LISTEN] READY" in line:
            ready = now
            print(time.strftime("%H:%M:%S"), "listening; the phone shows what to do", flush=True)
        if ready is None:
            continue
        if match := re.search(r"\[Audio\] RMS=([\d.]+)", line):
            samples.append((now - ready, float(match.group(1))))
        elif match := re.search(r"\[LISTEN\] GRAVITY x=(\S+) y=(\S+) z=(\S+) upsideDown=(\w+) asleep=(\w+)", line):
            poses.append((now - ready, *map(float, match.groups()[:3]), match.group(4) == "True", match.group(5) == "True"))
        elif "[LISTEN] EMIT" in line:
            emits.append(now - ready)
        elif match := re.search(r"\[LISTEN\] PHASE (\S+)", line):
            phases.append((now - ready, match.group(1)))
        elif "[LISTEN] DONE" in line:
            done = True
            break
        if now - ready > args.timeout:
            break
console.terminate()
console.wait(10)
if poses:
    print(f"{'pose':17s} {'gravity x':>9s} {'y':>6s} {'z':>6s}  upside-down  asleep at end")
    bounds = phases + [(poses[-1][0] + 1, None)]
    for (start, name), (end, _) in zip(bounds, bounds[1:]):
        inside = [p for p in poses if start <= p[0] < end]
        if inside:
            g = np.median([p[1:4] for p in inside], axis=0)
            print(f"{name:17s} {g[0]:9.2f} {g[1]:6.2f} {g[2]:6.2f}  {np.mean([p[4] for p in inside]) * 100:9.0f} %  {inside[-1][5]}")
if not samples:
    sys.exit(f"No analysis samples; see {args.output / 'device.log'}")

t, rms = map(np.array, zip(*samples))
db = lambda v: 20 * np.log10(v + 1e-9)
print(f"{'phase':10s} {'median':>8s} {'p90':>8s} {'max':>8s}   dBFS(p90)  above-trigger  waves")
bounds = phases + [(t.max() + 1, None)]
for (start, name), (end, _) in zip(bounds, bounds[1:]):
    inside = rms[(t >= start) & (t < end)]
    if len(inside):
        waves = sum(start <= e < end for e in emits)
        print(f"{name:10s} {np.median(inside):8.4f} {np.percentile(inside, 90):8.4f} {inside.max():8.4f}   "
              f"{db(np.percentile(inside, 90)):8.0f}  {np.mean(inside > TRIGGER_RMS) * 100:11.0f} %  {waves:5d}")
print(f"trigger RMS {TRIGGER_RMS} ({db(TRIGGER_RMS):.0f} dBFS); all phases seen: {done}")

try:
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    figure, axis = plt.subplots(figsize=(12, 4))
    axis.semilogy(t, rms, lw=.8, label="analysis RMS (21 ms)")
    axis.axhline(TRIGGER_RMS, color="red", ls="--", label="current trigger 0.0331")
    for e in emits:
        axis.axvline(e, color="orange", alpha=.3)
    for start, name in phases:
        axis.axvline(start, color="gray", lw=.6)
        axis.text(start + .2, .5, name, fontsize=8)
    axis.set_xlabel("seconds after READY")
    axis.set_ylim(1e-4, 1)
    axis.legend(loc="upper right")
    figure.tight_layout()
    figure.savefig(args.output / "listen.png", dpi=110)
except ImportError:
    pass
