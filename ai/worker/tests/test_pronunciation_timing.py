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


def test_gpu_voice_keeps_its_pcm_and_gets_timing_without_cpu_resynthesis(monkeypatch):
    import io
    import wave

    from test_tts_gpu import GpuServer, kokoro_with_gpu, wav_answer

    server = GpuServer(wav_answer())
    service, engine = kokoro_with_gpu(server)
    engine.tokenizer.phonemize = lambda text, lang: "mˈeɪ aɪ hɛlp juː"
    aligned = []

    def align(wav, cues):
        aligned.append((wav, cues))
        return tts.attach_timing(wav, [phone("f", 0.1, 0.4)])

    monkeypatch.setattr(tts, "align_timing", align)
    first = service.synth("May I help you?", "af_heart", 1)
    replay = service.synth("May I help you?", "af_heart", 1)
    assert read_timing(first[0]) is not None
    assert len(server.requests) == 1 and engine.texts == []
    assert first[0] == replay[0] and replay[2] is True
    assert len(aligned) == 1
    assert [cue["phoneme"] for cue in aligned[0][1]] == ["m", "ey", "_", "ay", "_", "h", "eh", "l", "p", "_", "y", "uw"]
    with wave.open(io.BytesIO(first[0])) as result, wave.open(io.BytesIO(aligned[0][0])) as source:
        assert result.getparams() == source.getparams()
        assert result.readframes(result.getnframes()) == source.readframes(source.getnframes())


def test_gpu_alignment_failure_keeps_voice_without_invented_timing(monkeypatch):
    from test_tts_gpu import GpuServer, kokoro_with_gpu, wav_answer

    server = GpuServer(wav_answer())
    service, engine = kokoro_with_gpu(server)
    engine.tokenizer.phonemize = lambda text, lang: "hɛlp"
    attempts = []

    def reject(wav, cues):
        attempts.append(cues)
        raise ValueError("Incomplete acoustic alignment")

    monkeypatch.setattr(tts, "align_timing", reject)
    wav, duration, cached = service.synth("Help", "af_heart", 1)
    assert len(attempts) == 1
    assert read_timing(wav) is None and duration == 1 and cached is False
    assert len(server.requests) == 1 and engine.texts == []


def test_cpu_aligner_runtime_failure_preserves_valid_times_from_the_same_synthesis(monkeypatch):
    engine = SimpleNamespace(
        has_timings=True,
        tokenizer=SimpleNamespace(phonemize=lambda text, lang: "hɛlp"),
        create_timed=lambda *a, **k: ([0] * 24000, 24000, [phone("h", 0.1, 0.2), phone("ɛ", 0.2, 0.5)]),
    )
    service = tts.Kokoro(engine, "fake")
    monkeypatch.setattr(tts, "to_wav", lambda audio, rate: wav_file())

    def unavailable(*args):
        raise RuntimeError("Failed to stop utterance processing")

    monkeypatch.setattr(tts, "align_timing", unavailable)
    voice, _, _ = service.synth("Help", "af_heart", 1)
    timing = read_timing(voice)
    assert timing is not None
    assert timing["phonemes"] == [dict(phoneme="h", start=0.1, end=0.2), dict(phoneme="eh", start=0.2, end=0.5)]
