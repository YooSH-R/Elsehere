"""Fit the known generated phoneme sequence to its PCM, without resynthesizing."""

import io
import wave

from .speech_timing import _write

# The acoustic model uses CMU phone names. Keep the original identity in the
# returned timeline (e.g. a reduced vowel still drives a reduced mouth pose).
CMU = {p: p.upper() for p in "p b m f v t d s z n k l g w y ng th dh sh zh jh ch aa ae ah ao eh er ey ay aw oy ow iy ih uh uw r".split()}
CMU.update(h="HH", ax="AH", ix="IH", dx="T")


def align_timing(wav: bytes, expected: list[dict], decoder=None) -> bytes:
    with wave.open(io.BytesIO(wav)) as source:
        if source.getnchannels() != 1 or source.getsampwidth() != 2 or source.getframerate() < 16000:
            raise ValueError("Phoneme alignment requires mono PCM16 at 16 kHz or above")
        rate = source.getframerate()
        pcm = source.readframes(source.getnframes())
        duration = source.getnframes() / rate
    groups, current = [], []
    for cue in expected:
        phone = cue["phoneme"]
        if phone == "_":
            if current:
                groups.append(current)
                current = []
        elif phone in CMU:
            current.append(phone)
        else:
            raise ValueError("Unsupported acoustic phoneme: " + phone)
    if current:
        groups.append(current)
    if not groups:
        raise ValueError("No spoken phoneme sequence to align")
    if decoder is None:
        from pocketsphinx import Decoder

        decoder = Decoder(samprate=rate, frate=100, lm=None, loglevel="ERROR")
    names = [f"npc{i}" for i in range(len(groups))]
    for name, group in zip(names, groups, strict=True):
        decoder.add_word(name, " ".join(CMU[p] for p in group), update=True)
    decoder.set_align_text(" ".join(names))
    for subword in (False, True):
        if subword:
            decoder.set_alignment()
        decoder.start_utt()
        decoder.process_raw(pcm, full_utt=True)
        decoder.end_utt()
    alignment = decoder.get_alignment()
    if alignment is None:
        raise ValueError("Acoustic phoneme sequence was not aligned")
    cues, seen, previous = [], [], 0.0
    for word in alignment:
        if word.name in names:
            group = groups[names.index(word.name)]
            phones = list(word)
            if [p.name for p in phones] != [CMU[p] for p in group]:
                raise ValueError("Acoustic phoneme sequence is incomplete or changed")
            identities = group
            seen.append(word.name)
        else:
            phones = list(word)
            if any(p.name != "SIL" for p in phones):
                raise ValueError("Unexpected acoustic phoneme sequence")
            identities = ["_"] * len(phones)
        for identity, phone in zip(identities, phones, strict=True):
            start, end = phone.start / 100, (phone.start + phone.duration) / 100
            if start < previous - 1e-7 or end <= start or end > duration + 0.02:
                raise ValueError("Acoustic phoneme bounds are outside the voice")
            end = min(end, duration)
            if start >= end:
                raise ValueError("Empty acoustic phoneme interval")
            if identity == "_" and cues and cues[-1]["phoneme"] == "_" and abs(start - previous) < 1e-7:
                cues[-1]["end"] = end
            else:
                cues.append(dict(phoneme=identity, start=start, end=end))
            previous = end
    if seen != names:
        raise ValueError("Acoustic phoneme sequence is incomplete or reordered")
    return _write(wav, cues)
