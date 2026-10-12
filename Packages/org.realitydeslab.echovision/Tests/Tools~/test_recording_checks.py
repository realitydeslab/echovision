"""Host tests for recording_checks: python3 -m unittest test_recording_checks (from this folder)."""
import unittest

import numpy as np
from PIL import Image, ImageFilter
from scipy import signal

from recording_checks import read_marker, voice_match

RATE = 16000

RED, GREEN, BLUE, YELLOW = (230, 30, 25), (40, 220, 60), (35, 60, 235), (240, 225, 40)


def scene(width=400, height=860, seed=7):
    """A dim, reddish-brown textured background, like the fabric the test phone faces."""
    rng = np.random.default_rng(seed)
    base = np.array([70, 30, 22], float) * rng.uniform(.4, 1.2, (height, width, 1))
    return np.clip(base + rng.normal(0, 8, (height, width, 3)), 0, 255).astype(np.uint8)


def with_marker(frame, size=90):
    """The walker's marker: red | green over blue | yellow, centred, softened like bloom."""
    frame = frame.copy()
    h, w = frame.shape[:2]
    top, left, half = h // 2 - size // 2, w // 2 - size // 2, size // 2
    for (row, col), colour in {(0, 0): RED, (0, 1): GREEN, (1, 0): BLUE, (1, 1): YELLOW}.items():
        frame[top + row * half: top + (row + 1) * half, left + col * half: left + (col + 1) * half] = colour
    return np.asarray(Image.fromarray(frame).filter(ImageFilter.GaussianBlur(1.5)))


class MarkerOrientationTests(unittest.TestCase):
    def test_upright_marker_reads_upright(self):
        self.assertEqual(read_marker(with_marker(scene())).layout, "upright")

    def test_each_rotation_and_mirror_is_named(self):
        frame = with_marker(scene())
        cases = {
            "rotated 90° counter-clockwise": np.rot90(frame, 1),
            "rotated 180°": np.rot90(frame, 2),
            "rotated 90° clockwise": np.rot90(frame, -1),
            "mirrored left-right": frame[:, ::-1],
            "mirrored top-bottom": frame[::-1],
            "mirrored across the main diagonal": frame.transpose(1, 0, 2),
            "mirrored across the anti-diagonal": np.rot90(frame, 2).transpose(1, 0, 2),
        }
        for expected, transformed in cases.items():
            with self.subTest(expected):
                self.assertEqual(read_marker(np.ascontiguousarray(transformed)).layout, expected)

    def test_frame_without_marker_has_no_reading(self):
        self.assertIsNone(read_marker(scene()))


class MarkerAspectTests(unittest.TestCase):
    def resized(self, x_scale, y_scale):
        image = Image.fromarray(with_marker(scene()))
        return np.asarray(image.resize((round(image.width * x_scale), round(image.height * y_scale)), Image.BILINEAR))

    def test_marker_scaled_evenly_stays_square(self):
        self.assertAlmostEqual(read_marker(self.resized(.8, .8)).aspect, 1, delta=.08)

    def test_stretched_movie_shows_as_marker_aspect(self):
        self.assertAlmostEqual(read_marker(self.resized(1.5, 1)).aspect, 1.5, delta=.1)
        self.assertAlmostEqual(read_marker(self.resized(1, 1.5)).aspect, 1 / 1.5, delta=.07)


class MarkerUnderSoundWavesTests(unittest.TestCase):
    """Colours measured on iPhone 15 Pro, 2026-10-12: the marker as rendered, and the same marker
    under the bloom of EchoVision's magenta sound waves (green and yellow lose saturation)."""
    RENDERED = {"red": (250, 49, 24), "green": (147, 235, 84), "blue": (33, 18, 240), "yellow": (236, 225, 91)}
    BLOOM = np.array([8, 0, 60])  # added to every marker pixel: green (147,235,84) -> (155,235,144) on device

    def frame_under_waves(self):
        frame = scene(540, 1176).astype(int)
        h, w = frame.shape[:2]
        size, top, left = 110, h // 2 - 55, w // 2 - 55
        for (row, col), name in {(0, 0): "red", (0, 1): "green", (1, 0): "blue", (1, 1): "yellow"}.items():
            frame[top + row * 55: top + (row + 1) * 55, left + col * 55: left + (col + 1) * 55] = \
                np.array(self.RENDERED[name]) + self.BLOOM
        # Violet and magenta wave streaks crossing the centre window, beside the marker.
        for x0, colour in ((w // 2 + 80, (170, 50, 250)), (w // 2 + 120, (255, 60, 255)), (w // 2 - 140, (150, 40, 240))):
            for y in range(int(h * .2), int(h * .8)):
                x = x0 + int(25 * np.sin(y / 40))
                frame[y, x - 3:x + 3] = colour
        # A broad violet glow larger than a marker quadrant (27k pixels against 3k on device).
        frame[h // 2 - 120:h // 2 + 60, w // 2 + 70:w // 2 + 200] = (150, 40, 240)
        return np.clip(frame, 0, 255).astype(np.uint8)

    def test_marker_under_sound_wave_bloom_still_reads_upright_and_square(self):
        reading = read_marker(self.frame_under_waves())
        self.assertIsNotNone(reading)
        self.assertEqual(reading.layout, "upright")
        self.assertAlmostEqual(reading.aspect, 1, delta=.1)
        self.assertAlmostEqual(reading.center[0], .5, delta=.03)


def speechlike(seed, syllables=7):
    """Voiced syllables (harmonics shaped by three formants) and fricative bursts, with pauses."""
    rng = np.random.default_rng(seed)
    parts = []
    for _ in range(syllables):
        t = np.arange(int(rng.uniform(.08, .2) * RATE)) / RATE
        if rng.random() < .25:
            sound = signal.sosfilt(signal.butter(4, [3000, 6000], "bandpass", fs=RATE, output="sos"),
                                   rng.normal(0, 1, len(t)))
        else:
            f0, formants = rng.uniform(110, 210), rng.uniform([300, 900, 2200], [800, 1800, 3000])
            sound = sum(np.sin(2 * np.pi * k * f0 * t) * sum(np.exp(-((k * f0 - f) / 150) ** 2) for f in formants)
                        for k in range(1, int(4000 / f0)))
        parts += [sound * np.sin(np.pi * t / t[-1]) ** 2, np.zeros(int(rng.uniform(.03, .12) * RATE))]
    phrase = np.concatenate(parts)
    return phrase / abs(phrase).max()


def through_speaker_and_room(phrase, seed=3):
    """Mac speaker -> room -> phone microphone: band limits, reverberation, low gain."""
    tail = np.arange(1, int(.15 * RATE)) / RATE
    reverb = np.random.default_rng(seed).normal(0, 1, len(tail)) * np.exp(-tail / .05)
    room = np.r_[1, reverb * np.sqrt(.5 / np.sum(reverb ** 2))]  # reverberation 3 dB below the direct sound
    return signal.sosfilt(signal.butter(2, [250, 4500], "bandpass", fs=RATE, output="sos"),
                          np.convolve(phrase, room)) * .05


# Room noise 6 dB below the phrase as the phone hears it, the same in every case.
NOISE = np.sqrt(np.mean(through_speaker_and_room(speechlike(1)) ** 2)) / 2


def heard_by_phone(phrase, at=.7, length=3., seed=3):
    played = through_speaker_and_room(phrase)
    mic = np.zeros(int(length * RATE))
    start = int(at * RATE)
    mic[start:start + len(played)] += played[:len(mic) - start]
    return mic + np.random.default_rng(seed).normal(0, NOISE, len(mic))


class VoiceTests(unittest.TestCase):
    def test_phrase_through_speaker_and_room_is_heard_at_its_offset(self):
        match = voice_match(heard_by_phone(speechlike(1)), speechlike(1), RATE)
        self.assertTrue(match.heard, match)
        self.assertAlmostEqual(match.offset, .7, delta=.03)

    def test_noise_alone_is_not_heard(self):
        self.assertFalse(voice_match(heard_by_phone(np.zeros(1)), speechlike(1), RATE).heard)

    def test_other_speech_is_not_the_phrase(self):
        self.assertFalse(voice_match(heard_by_phone(speechlike(2)), speechlike(1), RATE).heard)

    def test_phrase_is_heard_through_handling_clicks(self):
        # Device take 2026-10-12: sharp broadband clicks (peak 1.0, clipped) from handling the phone.
        mic = heard_by_phone(speechlike(1))
        for at in np.random.default_rng(5).uniform(.1, 2.9, 8):
            start = int(at * RATE)
            mic[start:start + 40] += np.random.default_rng(int(at * 1000)).choice([-1., 1.], 40)
        match = voice_match(mic, speechlike(1), RATE)
        self.assertTrue(match.heard, match)
        self.assertAlmostEqual(match.offset, .7, delta=.03)

    def test_recording_shorter_than_the_phrase_is_not_heard(self):
        self.assertFalse(voice_match(heard_by_phone(speechlike(1), at=0, length=.3), speechlike(1), RATE).heard)


if __name__ == "__main__":
    unittest.main()
