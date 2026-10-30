"""The dictionary pronunciation and lip-sync times must describe the same synthesis."""

from types import SimpleNamespace

import pytest
from test_speech_timing import phone, wav_file

from app import tts
from app.speech_timing import read_timing


@pytest.mark.parametrize("timed", [True, False])
def test_dictionary_phonemes_reach_the_selected_engine_once_and_survive_cache(monkeypatch, timed):
    calls = []
    engine = SimpleNamespace(
        has_timings=timed,
        tokenizer=SimpleNamespace(phonemize=lambda text, lang: text.strip()),
    )

    def synth(text, **options):
        calls.append((text, options))
        base = ([0] * 24000, 24000)
        return (*base, [phone("f", 0.1, 0.4)]) if timed else base

    def unexpected(*args, **kwargs):
        raise AssertionError("Do not synthesize a second voice for its timing.")

    engine.create_timed = synth if timed else unexpected
    engine.create = unexpected if timed else synth
    service = tts.Kokoro(engine, "fake")
    monkeypatch.setattr(tts, "to_wav", lambda audio, rate: wav_file())
    aligned = []

    def align(wav, cues):
        aligned.extend(cues)
        return tts.attach_timing(wav, [phone("f", 0.1, 0.4)])

    monkeypatch.setattr(tts, "align_timing", align)
    first = service.synth("That's $13, hon.", "af_heart", 1)
    replay = service.synth("That's 13 dollars, hon.", "af_heart", 1)
    assert calls == [("That's 13 dollars, hˈʌn.", dict(voice="af_heart", speed=1, lang="en-us", is_phonemes=True))]
    assert first[0] == replay[0] and replay[2] is True
    assert (read_timing(first[0]) is not None) is timed
    assert bool(aligned) is timed
