"""Checks on movies recorded by the device walker.

read_marker: the walker draws a 2x2 marker (red, green over blue, yellow) at the centre of the
recorded camera. Where its colours land in a decoded frame tells whether the movie is upright,
rotated or mirrored.

voice_match: a runner plays a known phrase through the Mac speakers during each take. The phrase's
band-energy pattern over time is searched for in the movie's microphone track.
"""
import itertools
from typing import NamedTuple, Optional

import numpy as np
from scipy import ndimage

# Quadrant of each colour in an upright marker: x to the right, y down.
UPRIGHT = {"red": (-1, -1), "green": (1, -1), "blue": (-1, 1), "yellow": (1, 1)}
# Where an upright quadrant (x, y) ends up in the decoded frame.
LAYOUTS = {
    "upright": lambda x, y: (x, y),
    "rotated 90° counter-clockwise": lambda x, y: (y, -x),
    "rotated 180°": lambda x, y: (-x, -y),
    "rotated 90° clockwise": lambda x, y: (-y, x),
    "mirrored left-right": lambda x, y: (-x, y),
    "mirrored top-bottom": lambda x, y: (x, -y),
    "mirrored across the main diagonal": lambda x, y: (y, x),
    "mirrored across the anti-diagonal": lambda x, y: (-y, -x),
}


class MarkerReading(NamedTuple):
    layout: str
    aspect: float  # marker width / height in the frame; 1 when the movie is not stretched
    center: tuple  # marker centre, as fractions of the frame width and height


def _colour_masks(frame):
    """Pixels of each marker colour, by which channels dominate. Saturation alone is not enough:
    the bloom of EchoVision's magenta sound waves pales green and yellow to about 0.4."""
    r, g, b = (frame[..., i].astype(int) for i in range(3))
    bright = np.maximum(np.maximum(r, g), b) > 115
    return {"red": bright & (r - np.maximum(g, b) > 60),
            "green": bright & (g - np.maximum(r, b) > 50),
            "blue": bright & (b - np.maximum(r, g) > 60),
            "yellow": bright & (np.minimum(r, g) - b > 50) & (abs(r - g) < 60)}


def _patches(mask, minimum, keep=4):
    """Solid regions of one colour, largest first: thin sound-wave streaks do not survive."""
    labels, count = ndimage.label(ndimage.binary_opening(mask, np.ones((3, 3)), iterations=2))
    areas = ndimage.sum(np.ones_like(labels), labels, range(1, count + 1))
    return [labels == 1 + i for i in np.argsort(areas)[::-1][:keep] if areas[i] >= minimum]


def _marker_patches(masks, minimum):
    """One patch per colour that together form the 2x2 marker: equal squares side by side. A
    sound-wave glow can be larger than a quadrant, so the largest patch is not always the marker."""
    candidates = {name: [(patch, patch.sum(), np.array(ndimage.center_of_mass(patch)))
                         for patch in _patches(mask, minimum)] for name, mask in masks.items()}
    best, best_cost = None, np.inf
    for choice in itertools.product(*candidates.values()):
        areas = np.array([area for _, area, _ in choice])
        side = np.sqrt(areas.mean())
        spread = max(np.linalg.norm(a[2] - b[2]) for a, b in itertools.combinations(choice, 2)) / side
        cost = spread + np.log(areas.max() / areas.min())
        if spread < 2.5 and cost < best_cost:  # diagonal neighbours sit 1.41 sides apart
            best, best_cost = dict(zip(candidates, choice)), cost
    return best


def read_marker(frame) -> Optional[MarkerReading]:
    """Find the marker near the frame centre; None when any of its four colours is missing."""
    frame = np.asarray(frame)
    h, w = frame.shape[:2]
    top, left = int(h * .2), int(w * .2)
    window = frame[top:h - top, left:w - left]
    minimum = max(20, window.shape[0] * window.shape[1] // 2000)
    patches = _marker_patches(_colour_masks(window), minimum)
    if patches is None:
        return None
    centroids, marker = {}, np.zeros(window.shape[:2], bool)
    for name, (patch, _, (y, x)) in patches.items():
        centroids[name] = (x + left, y + top)
        marker |= patch
    ys, xs = np.nonzero(marker)
    extent = lambda values: np.percentile(values, 98) - np.percentile(values, 2)
    cx = np.mean([c[0] for c in centroids.values()])
    cy = np.mean([c[1] for c in centroids.values()])
    observed = {name: (int(np.sign(x - cx)), int(np.sign(y - cy))) for name, (x, y) in centroids.items()}
    layout = next((name for name, move in LAYOUTS.items()
                   if all(move(*UPRIGHT[c]) == observed[c] for c in UPRIGHT)), "unreadable")
    return MarkerReading(layout, extent(xs) / extent(ys), (cx / w, cy / h))


HOP = .01  # seconds between analysis frames
HEARD = .5  # minimum correlation for the phrase to count as heard


class VoiceMatch(NamedTuple):
    heard: bool
    score: float  # correlation of band energies with the phrase, -1..1
    offset: float  # seconds into the recording where the phrase starts
    snr_db: float  # phrase energy over the rest of the recording


def _band_energy(pcm, rate, bands=16, low=200, high=5000):
    """log10 energy in log-spaced bands, one row per 10 ms."""
    size, hop = int(.032 * rate), int(HOP * rate)
    if len(pcm) < size:
        return np.zeros((0, bands))
    frames = np.lib.stride_tricks.sliding_window_view(np.asarray(pcm, float), size)[::hop] * np.hanning(size)
    power = abs(np.fft.rfft(frames)) ** 2
    frequencies = np.fft.rfftfreq(size, 1 / rate)
    edges = np.geomspace(low, high, bands + 1)
    energy = np.stack([power[:, (frequencies >= a) & (frequencies < b)].sum(1) for a, b in zip(edges, edges[1:])], 1)
    # A 70 ms median over time removes handling clicks (one click spans 3-4 frames); syllables
    # last 80-200 ms and keep their shape.
    return ndimage.median_filter(np.log10(energy + 1e-12), size=(7, 1), mode="nearest")


def voice_match(mic, phrase, rate) -> VoiceMatch:
    """Find the phrase in a microphone recording; both mono PCM at the same rate."""
    reference = _band_energy(phrase, rate)
    loudness = reference.max(1)
    active = np.flatnonzero(loudness > loudness.max() - 4)  # within 40 dB of the loudest frame
    if len(active) == 0:
        return VoiceMatch(False, 0., 0., 0.)
    lead = active[0]
    reference = reference[lead:active[-1] + 1]
    reference = np.maximum(reference, reference.max() - 5)  # quiet gaps count as quiet, not as -120 dB
    recording = _band_energy(mic, rate)
    if len(recording) < len(reference):
        return VoiceMatch(False, 0., 0., 0.)
    x = reference - reference.mean(0)
    x /= np.sqrt((x ** 2).sum())
    windows = np.lib.stride_tricks.sliding_window_view(recording, len(reference), axis=0)  # lag, band, time
    y = windows - windows.mean(2, keepdims=True)
    scores = np.einsum("lbt,tb->l", y, x) / np.sqrt((y ** 2).sum((1, 2)))
    lag = int(np.argmax(scores))
    energy = (10 ** recording).sum(1)
    inside = np.zeros(len(energy), bool)
    inside[lag:lag + len(reference)] = True
    outside = energy[~inside]
    snr = 10 * np.log10(energy[inside].mean() / np.median(outside)) if len(outside) else 0.
    score = float(scores[lag])
    return VoiceMatch(score >= HEARD, score, (lag - lead) * HOP, float(snr))
