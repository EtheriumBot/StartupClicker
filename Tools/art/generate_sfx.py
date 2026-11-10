#!/usr/bin/env python3
"""
Synthesises the game's sound effects as small WAV files in Assets/Resources/Audio.
Run: python3 Tools/art/generate_sfx.py   (standard library only)

Each sound is a list of short tones. A tone is a waveform at some pitch (optionally sliding to
another pitch) shaped by an envelope that fades it out, which is what makes it sound like a "blip".
"""
import math
import os
import random
import struct
import wave

RATE = 44100
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio")


def tone(f0, f1, dur, wave_kind="sine", vol=0.5, attack=0.004, decay=6.0, start=0.0):
    """Return (start_sample, samples). Pitch slides from f0 to f1 over the tone."""
    n = int(dur * RATE)
    out = []
    phase = 0.0
    rng = random.Random(7)
    for i in range(n):
        t = i / RATE
        k = i / max(1, n - 1)
        f = f0 + (f1 - f0) * k
        phase += 2 * math.pi * f / RATE
        if wave_kind == "sine":
            s = math.sin(phase)
        elif wave_kind == "square":
            s = 0.6 if math.sin(phase) >= 0 else -0.6
        elif wave_kind == "tri":
            s = 2 / math.pi * math.asin(math.sin(phase))
        else:  # noise
            s = rng.uniform(-1, 1)
        env = min(1.0, t / attack) * math.exp(-decay * k) * (1 - k) ** 0.5
        out.append(s * env * vol)
    return int(start * RATE), out


def render(name, tones):
    length = max(s + len(d) for s, d in tones)
    buf = [0.0] * length
    for s, d in tones:
        for i, v in enumerate(d):
            buf[s + i] += v
    peak = max(1e-6, max(abs(v) for v in buf))
    scale = min(1.0, 0.9 / peak)
    os.makedirs(OUT, exist_ok=True)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(v * scale * 32767)) for v in buf))


if __name__ == "__main__":
    render("click", [tone(880, 520, 0.07, "tri", 0.6, decay=5), tone(0, 0, 0.02, "noise", 0.25, decay=8)])
    render("select", [tone(660, 700, 0.05, "sine", 0.4, decay=5)])
    render("buy", [tone(523, 523, 0.09, "tri", 0.5), tone(784, 784, 0.14, "tri", 0.5, start=0.07)])
    render("place", [tone(160, 70, 0.16, "sine", 0.8, decay=5), tone(0, 0, 0.05, "noise", 0.3, decay=7),
                     tone(990, 990, 0.08, "tri", 0.25, start=0.03)])
    render("error", [tone(150, 110, 0.16, "square", 0.4, decay=3)])
    render("upgrade", [tone(523, 523, 0.10, "tri", 0.5), tone(659, 659, 0.10, "tri", 0.5, start=0.08),
                       tone(784, 784, 0.10, "tri", 0.5, start=0.16), tone(1047, 1047, 0.30, "tri", 0.5, start=0.24)])
    render("drop", [tone(1319, 1319, 0.25, "sine", 0.4, decay=4), tone(1568, 1568, 0.25, "sine", 0.4, decay=4, start=0.09),
                    tone(2093, 2093, 0.45, "sine", 0.4, decay=4, start=0.18)])
    render("sell", [tone(600, 300, 0.14, "tri", 0.5, decay=4)])
    print("wrote sounds to", OUT)
