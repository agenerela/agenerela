# Sound track for the Agenerela logo animation, synthesised: 6 s, 48 kHz stereo WAV.
#     python sfx.py            -> out/sfx.wav
# The cue times are the TIMES table in anim.py, so every sound lands on its frame.

import os
import re
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt, fftconvolve

HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "anim.py"), encoding="utf-8").read()
TIMES = eval(re.search(r"TIMES = (dict\(.*?\))\n", src, re.S).group(1))

SR, DUR = 48000, 6.0
N = int(SR * DUR)
t = np.arange(N) / SR
rng = np.random.default_rng(7)
mix = np.zeros((N, 2))


def add(sig, start, pan=0.0, gain=1.0):
    """Mix a mono signal in at start seconds, equal-power pan -1 (left) .. 1 (right)."""
    i = int(start * SR)
    sig = sig[: max(0, N - i)]
    a = (pan + 1) * np.pi / 4
    mix[i:i + len(sig), 0] += sig * np.cos(a) * gain
    mix[i:i + len(sig), 1] += sig * np.sin(a) * gain


def env(n, attack, decay):
    k = np.arange(n) / SR
    return np.minimum(1, k / max(attack, 1e-4)) * np.exp(-k / decay)


def band(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], "bandpass", fs=SR, output="sos"), x)


def swept_noise(dur, f0, f1, q=0.6):
    """Noise through a band-pass whose centre glides f0 -> f1 (exponentially)."""
    n, blk = int(dur * SR), 256
    x, out = rng.standard_normal(n), np.zeros(n)
    for b in range(0, n, blk):
        fc = f0 * (f1 / f0) ** (b / n)
        lo, hi = fc * (1 - q / 2), min(fc * (1 + q / 2), SR / 2 - 100)
        seg = x[max(0, b - 2048): b + blk]
        out[b:b + blk] = band(seg, lo, hi)[-len(x[b:b + blk]):]
    return out


def tone(freqs, dur, decay, attack=0.002, detune=0.0):
    n = int(dur * SR)
    k = np.arange(n) / SR
    s = sum(np.sin(2 * np.pi * f * (1 + detune * (i % 2 * 2 - 1)) * k) / (i + 1) ** 0.6
            for i, f in enumerate(freqs))
    return s * env(n, attack, decay)


# 1. Pad: C minor-ish open chord, in from 0.2 s, lifts at the impact, out with the fade.
pad = sum(np.sin(2 * np.pi * f * t + 0.3 * np.sin(2 * np.pi * 0.23 * t * (i + 1)))
          for i, f in enumerate([65.41, 98.0, 130.81, 155.56, 196.0]))
pad = band(pad, 40, 900)
shape = np.clip((t - 0.2) / 1.5, 0, 1) * (0.55 + 0.45 * np.clip((t - TIMES["impact"]) / 0.4, 0, 1))
shape *= np.clip((DUR - t) / (DUR - TIMES["fade_out"]), 0, 1)
add(pad * shape, 0, gain=0.04)

# 2. Riser as the A comes up out of the floor, ending in a soft low settle.
rise = TIMES["rise_end"]
r = swept_noise(rise, 250, 3200) * np.linspace(0, 1, int(rise * SR)) ** 2
add(r, 0.0, pan=-0.2, gain=0.55)
add(tone([82.4, 164.8], 0.6, 0.18), rise, gain=0.35)
add(band(rng.standard_normal(int(0.25 * SR)), 120, 900) * env(int(0.25 * SR), 0.003, 0.06), rise, gain=0.25)

# 3. The square's fall: a falling whistle and air, accelerating into the impact.
fall = TIMES["impact"] - TIMES["drop_start"]
n = int(fall * SR)
k = np.arange(n) / SR
freq = 2400 * (300 / 2400) ** ((k / fall) ** 2)
whistle = np.sin(2 * np.pi * np.cumsum(freq) / SR) * (k / fall) ** 1.5
air = swept_noise(fall, 5000, 600, q=0.9) * (k / fall) ** 2
add(whistle * 0.25 + air * 0.6, TIMES["drop_start"], pan=0.1, gain=0.5)

# 4. Impact: sub boom with a pitch drop, a click, and a bright chime that rings out.
imp = TIMES["impact"]
n = int(1.6 * SR)
k = np.arange(n) / SR
boom = np.sin(2 * np.pi * np.cumsum(38 + 50 * np.exp(-k * 18)) / SR) * env(n, 0.002, 0.45)
click = band(rng.standard_normal(int(0.02 * SR)), 2000, 9000) * env(int(0.02 * SR), 0.0005, 0.004)
chime = tone([523.25, 784.0, 1046.5, 1568.0, 2093.0, 3136.0], 2.6, 0.9, detune=0.002)
add(boom, imp, gain=1.0)
add(click, imp, gain=0.6)
add(chime, imp, pan=-0.15, gain=0.22)
add(chime, imp + 0.012, pan=0.15, gain=0.18)
add(tone([1046.5, 1568.0], 0.3, 0.05), imp + 0.13, gain=0.08)          # the little bounce

# 5. Letters: one blip each, climbing a pentatonic scale, panned where the letter sits.
scale = [523.25, 587.33, 659.25, 783.99, 880.0, 1046.5, 1174.66, 1318.51, 1567.98]
for i, f in enumerate(scale):
    blip = tone([f, f * 2, f * 3], 0.25, 0.07, attack=0.001)
    add(blip, TIMES["letters"] + i * TIMES["letter_gap"], pan=-0.7 + 1.4 * i / 8, gain=0.22)

# 6. Shimmer that travels left to right with the light sweep.
s0, s1 = TIMES["sweep_start"], TIMES["sweep_end"]
n = int((s1 - s0) * SR)
k = np.arange(n) / SR
bell = np.sin(np.pi * k / (s1 - s0))
sh = band(rng.standard_normal(n), 6000, 14000) * 0.25
for j in range(26):                                # sparkles: short high partials
    at = rng.uniform(0, s1 - s0 - 0.2)
    f = rng.choice([2093.0, 2349.3, 2637.0, 3136.0, 3520.0, 4186.0])
    i = int(at * SR)
    p = tone([f], 0.25, 0.06)[: n - i]
    sh[i:i + len(p)] += p * 0.5
sh *= bell
for c, pan in ((0, -1), (1, 1)):                   # pan follows the light
    w = np.cos((k / (s1 - s0) * 2 - 1 + 1) * np.pi / 4) if c == 0 else np.sin((k / (s1 - s0)) * np.pi / 2)
    i = int(s0 * SR)
    mix[i:i + n, c] += sh * w * 0.3

# Room: a short stereo reverb from decaying noise, then a gentle limiter and normalise.
ir_n = int(2.2 * SR)
ir = rng.standard_normal((ir_n, 2)) * np.exp(-np.arange(ir_n) / SR / 0.55)[:, None]
ir = np.stack([band(ir[:, c], 200, 9000) for c in range(2)], 1)
ir /= np.abs(ir).sum(0, keepdims=True) ** 0.5 * 30
wet = np.stack([fftconvolve(mix[:, c], ir[:, c])[:N] for c in range(2)], 1)
out = mix + wet * 0.35
out *= np.clip((DUR - t) / 0.25, 0, 1)[:, None]          # nothing cut off at the very end
out = np.tanh(out * 1.6) / np.tanh(1.6)
out *= 10 ** (-1 / 20) / np.abs(out).max()

os.makedirs(os.path.join(HERE, "out"), exist_ok=True)
wavfile.write(os.path.join(HERE, "out", "sfx.wav"), SR, (out * 32767).astype(np.int16))
print("wrote out/sfx.wav", out.shape, "peak dBFS", round(20 * np.log10(np.abs(out).max()), 2))
