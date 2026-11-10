import argparse
import json
import subprocess
from pathlib import Path

import numpy as np
import soundfile as sf

parser = argparse.ArgumentParser(
    description="Render the original ELSEHERE entry music; requires numpy, soundfile and ffmpeg."
)
parser.add_argument("--output", type=Path, required=True)
parser.add_argument("--ffmpeg", default="ffmpeg")
args = parser.parse_args()
OUT = args.output
OUT.mkdir(parents=True, exist_ok=True)
SR = 44100
FF = args.ffmpeg


def compose(style, bpm, name):
    beat = 60 / bpm
    n = round(64 * beat * SR)
    y = np.zeros((n, 2))
    rng = np.random.default_rng(758)

    def note(midi, pos, dur, level, voice, pan=0):
        t = np.arange(round((dur * beat + 0.65) * SR)) / SR
        f = 440 * 2 ** ((midi - 69) / 12)
        if voice == "nylon":
            x = sum(
                ((-1) ** (h + 1) / h**1.5)
                * np.sin(2 * np.pi * f * h * t)
                * np.exp(-t * (2.6 + h * 0.7))
                for h in range(1, 11)
            )
            attack = 0.006
        elif voice == "clarinet":
            x = sum(
                a * np.sin(2 * np.pi * f * h * t) * np.exp(-t * (1.2 + h * 0.3))
                for h, a in [(1, 1), (3, 0.25), (5, 0.09)]
            )
            attack = 0.025
        elif voice == "pizz":
            x = sum(
                a * np.sin(2 * np.pi * f * h * t) * np.exp(-t * (6 + h * 2))
                for h, a in [(1, 1), (2, 0.4), (3, 0.2), (4, 0.08)]
            )
            attack = 0.004
        elif voice == "bells":
            x = (
                np.sin(2 * np.pi * f * t)
                + 0.2 * np.sin(2 * np.pi * f * 2.75 * t) * np.exp(-t / 0.1)
                + 0.07 * np.sin(2 * np.pi * f * 4 * t)
            ) * np.exp(-t / 0.35)
            attack = 0.005
        elif voice == "rhodes":
            x = (
                np.sin(2 * np.pi * f * t)
                + 0.24 * np.sin(2 * np.pi * f * 2 * t) * np.exp(-t / 0.18)
                + 0.1 * np.sin(2 * np.pi * f * 3 * t) * np.exp(-t / 0.09)
            ) * np.exp(-t / 0.65)
            attack = 0.015
        elif voice == "strings":
            x = (
                np.sin(2 * np.pi * f * t)
                + 0.28 * np.sin(2 * np.pi * f * 2 * t)
                + 0.09 * np.sin(2 * np.pi * f * 3 * t)
                + 0.16 * np.sin(2 * np.pi * f * 1.002 * t)
            ) * np.exp(-t / 4)
            attack = 0.09
        else:
            x = (
                np.sin(2 * np.pi * f * t) + 0.18 * np.sin(2 * np.pi * f * 2 * t)
            ) * np.exp(-t / 0.75)
            attack = 0.025
        x *= (
            np.minimum(t / attack, 1)
            * np.clip((dur * beat + 0.15 - t) / 0.12, 0, 1)
            * level
        )
        idx = (round(pos * beat * SR) + np.arange(len(t))) % n
        np.add.at(y[:, 0], idx, x * np.sqrt((1 - pan) / 2))
        np.add.at(y[:, 1], idx, x * np.sqrt((1 + pan) / 2))

    chords = [
        (48, 52, 55, 60),
        (41, 45, 48, 53),
        (50, 53, 57, 62),
        (43, 47, 50, 55),
    ] * 4
    motifs = (
        ([72, 67, 69, 71], [72, 76, 74, 72], [74, 78, 76, 74], [71, 74, 72, 67])
        if style == "bossa"
        else ([76, 79, 84, 79], [81, 77, 76, 72], [77, 81, 74, 77], [79, 83, 86, 83])
    )
    for bar, c in enumerate(chords):
        base = bar * 4
        if style == "bossa":
            note(c[0] - 12, base, 0.6, 0.11, "bass")
            note(c[0] - 5, base + 1.5, 0.45, 0.075, "bass")
            note(c[0] - 12, base + 2.5, 0.6, 0.085, "bass")
            for pos in [0.5, 1.75, 2.75, 3.5]:
                for k, pitch in enumerate(c[1:]):
                    note(
                        pitch + 12,
                        base + pos + k * 0.035,
                        0.4,
                        0.041,
                        "nylon",
                        [-0.35, 0, 0.35][k],
                    )
            for pos, pitch in zip([0.0, 0.75, 2.0, 3.25], motifs[bar % 4]):
                note(pitch, base + pos, 0.45, 0.105, "clarinet", 0.05)
        else:
            note(c[0] - 12 if c[0] >= 48 else c[0], base, 2.2, 0.065, "bass")
            for pos in [0.5, 1.5, 2.5, 3.5]:
                for k, pitch in enumerate(c[1:]):
                    note(
                        pitch + 12,
                        base + pos + k * 0.015,
                        0.28,
                        0.043,
                        "pizz",
                        [-0.3, 0, 0.3][k],
                    )
            for j, pitch in enumerate(motifs[bar % 4]):
                note(pitch, base + [0, 0.75, 1.5, 2.75][j], 0.4, 0.095, "bells", 0.08)
        if style == "pizz":
            # Under the approved melody: warm comping keys, light strings, and a short answering line.
            for pos in [0, 2.25]:
                for k, pitch in enumerate(c[1:]):
                    note(
                        pitch + 12,
                        base + pos + k * 0.025,
                        0.85,
                        0.026,
                        "rhodes",
                        [-0.35, 0, 0.35][k],
                    )
            for k, pitch in enumerate(c[:3]):
                note(pitch + 12, base, 3.55, 0.018, "strings", [-0.5, 0, 0.5][k])
        # Small dry percussion, kept behind the melody.
        for pos in [0.5, 1.5, 2.5, 3.5]:
            t = np.arange(round(0.045 * SR)) / SR
            x = rng.normal(size=len(t)) * np.exp(-t / 0.009) * 0.004
            idx = (round((base + pos) * beat * SR) + np.arange(len(t))) % n
            np.add.at(y[:, 0], idx, x)
            np.add.at(y[:, 1], idx, x * 0.8)
    direct = y.copy()
    for delay, level in [(0.083, 0.1), (0.159, 0.04)]:
        y += np.roll(direct[:, ::-1], round(delay * SR), axis=0) * level
    y *= 0.095 / max(float(np.sqrt(np.mean(y * y))), 1e-8)
    y = 0.72 * np.tanh(y / 0.72)
    sf.write(OUT / (name + ".wav"), y, SR, subtype="PCM_16")
    for ext, codec, args in [
        ("ogg", "libvorbis", ["-q:a", "5"]),
        ("mp3", "libmp3lame", ["-b:a", "160k"]),
    ]:
        subprocess.run(
            [
                str(FF),
                "-v",
                "error",
                "-y",
                "-i",
                str(OUT / (name + ".wav")),
                "-c:a",
                codec,
                *args,
                str(OUT / (name + "." + ext)),
            ],
            check=True,
        )
    print(name, "BPM", bpm, "seconds", len(y) / SR)
    (OUT / (name + ".json")).write_text(
        json.dumps(
            {
                "style": style,
                "bpm": bpm,
                "seconds": len(y) / SR,
                "original_synthesis": True,
                "seam_delta": float(np.max(np.abs(y[0] - y[-1]))),
            },
            indent=2,
        ),
        encoding="utf-8",
    )


compose("pizz", 124, "elsehere-town-balanced-v6")
