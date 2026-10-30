from types import SimpleNamespace

import pytest
from test_speech_timing import wav_file

from app.speech_alignment import align_timing
from app.speech_timing import read_timing


class Word(list):
    def __init__(self, name, *phones):
        super().__init__(SimpleNamespace(name=p, start=s, duration=e - s) for p, s, e in phones)
        self.name = name


class Decoder:
    def __init__(self, result):
        self.result = result
        self.words = []
        self.passes = 0

    def add_word(self, word, phones, update):
        self.words.append((word, phones))

    def set_align_text(self, text):
        self.text = text

    def start_utt(self):
        pass

    def process_raw(self, pcm, full_utt):
        self.passes += 1

    def end_utt(self):
        pass

    def set_alignment(self):
        pass

    def get_alignment(self):
        return self.result


def test_actual_pcm_alignment_replaces_shifted_boundaries_without_word_silence():
    original = wav_file()
    expected = [
        dict(phoneme=p, start=s, end=e) for p, s, e in [("m", 0.17, 0.22), ("ey", 0.22, 0.32), ("_", 0.32, 0.37), ("ay", 0.37, 0.44)]
    ]
    decoder = Decoder(
        [
            Word("<sil>", ("SIL", 0, 8)),
            Word("npc0", ("M", 8, 15), ("EY", 15, 30)),
            Word("npc1", ("AY", 30, 36)),
            Word("<sil>", ("SIL", 36, 100)),
        ]
    )
    result = align_timing(original, expected, decoder)
    timing = read_timing(result)
    assert timing is not None
    assert decoder.passes == 2
    assert decoder.words == [("npc0", "M EY"), ("npc1", "AY")]
    assert [p["phoneme"] for p in timing["phonemes"]] == ["_", "m", "ey", "ay", "_"]
    assert timing["phonemes"][1] == dict(phoneme="m", start=0.08, end=0.15)
    assert timing["phonemes"][3] == dict(phoneme="ay", start=0.3, end=0.36)
    assert result[44:48044] == original[44:48044]


def test_incomplete_alignment_is_rejected_instead_of_claiming_accurate_lip_sync():
    expected = [dict(phoneme="m", start=0.1, end=0.2), dict(phoneme="ey", start=0.2, end=0.4)]
    with pytest.raises(ValueError, match="phoneme sequence"):
        align_timing(wav_file(), expected, Decoder([Word("npc0", ("M", 10, 20))]))


def test_failed_acoustic_alignment_returns_unchanged_voice_without_stale_timing(monkeypatch):
    import threading
    from collections import OrderedDict
    from types import SimpleNamespace

    from test_speech_timing import phone

    from app import tts

    original = wav_file()
    service = tts.Kokoro.__new__(tts.Kokoro)
    service.engine = SimpleNamespace(has_timings=True, create_timed=lambda *a, **k: ([0] * 24000, 24000, [phone("m", 0.1, 0.2)]))
    service.lock, service.cache = threading.Lock(), OrderedDict()
    monkeypatch.setattr(tts, "to_wav", lambda audio, rate: original)

    def reject(*args):
        raise ValueError("phoneme sequence mismatch")

    monkeypatch.setattr(tts, "align_timing", reject)
    voice, _, _ = service.synth("May I help you?", "af_heart", 1)
    assert voice == original
    assert read_timing(voice) is None
