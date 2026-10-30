import io
import wave
from types import SimpleNamespace

from app.speech_timing import attach_timing, read_timing


def wav_file(seconds=1.0):
    out = io.BytesIO()
    with wave.open(out, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(24000)
        w.writeframes(b"\x12\x00" * int(24000 * seconds))
    return out.getvalue()


def phone(symbol, start, end):
    return SimpleNamespace(phoneme=symbol, start=start, end=end)


def test_timed_wav_preserves_audio_and_distinguishes_i_you_fv_th():
    original = wav_file()
    spoken = [
        phone("a", 0.1, 0.2),
        phone("ɪ", 0.2, 0.3),
        phone("j", 0.3, 0.4),
        phone("u", 0.4, 0.5),
        phone("f", 0.5, 0.6),
        phone("θ", 0.6, 0.7),
    ]
    result = attach_timing(original, spoken)
    timeline = read_timing(result)
    assert timeline is not None
    assert [p["phoneme"] for p in timeline["phonemes"]] == ["ay", "y", "uw", "f", "th"]
    assert timeline["phonemes"][0] == {"phoneme": "ay", "start": 0.1, "end": 0.3}
    assert timeline["duration"] == 1
    with wave.open(io.BytesIO(original)) as first, wave.open(io.BytesIO(result)) as second:
        assert second.readframes(second.getnframes()) == first.readframes(first.getnframes())


def test_kokoro_uses_one_timed_synthesis_and_caches_audio_with_its_timing(monkeypatch):
    import threading
    from collections import OrderedDict

    from app import tts

    class Engine:
        has_timings = True
        tokenizer = SimpleNamespace(phonemize=lambda text, lang: text)
        calls = 0

        def create(self, *args, **kwargs):
            return [0] * 24000, 24000

        def create_timed(self, *args, **kwargs):
            self.calls += 1
            return [0] * 24000, 24000, [phone("f", 0.1, 0.4)]

    service = tts.Kokoro.__new__(tts.Kokoro)
    service.engine, service.lock, service.cache = Engine(), threading.Lock(), OrderedDict()
    monkeypatch.setattr(tts, "to_wav", lambda audio, rate: wav_file())
    # Acoustic alignment is verified separately; keep this engine/cache test offline.
    monkeypatch.setattr(tts, "align_timing", lambda wav, cues: attach_timing(wav, [phone("f", 0.1, 0.4)]))
    first = service.synth("Fresh coffee.", "af_heart", 1.0)
    second = service.synth("Fresh coffee.", "af_heart", 1.0)
    assert read_timing(first[0]) is not None
    assert service.engine.calls == 1
    assert first[0] == second[0] and first[2] is False and second[2] is True


def test_joined_reply_keeps_second_chunk_timing_at_its_audio_offset():
    from app.tts import join_wavs

    first = attach_timing(wav_file(), [phone("f", 0.1, 0.3)])
    second = attach_timing(wav_file(), [phone("θ", 0.2, 0.4)])
    joined, duration = join_wavs([first, second])
    timing = read_timing(joined)
    assert timing is not None
    assert duration == 2
    assert timing["phonemes"][1] == {"phoneme": "th", "start": 1.2, "end": 1.4}


def test_missing_timing_never_invents_phonemes_and_invalid_data_is_rejected():
    import pytest

    from app.tts import join_wavs

    plain = wav_file()
    assert attach_timing(plain, []) == plain
    timed = attach_timing(plain, [phone("f", 0.1, 0.4)])
    assert read_timing(join_wavs([timed, plain])[0]) is None
    with pytest.raises(ValueError):
        attach_timing(plain, [phone("f", 0.3, 0.5), phone("v", 0.2, 0.4)])
    with pytest.raises(ValueError):
        attach_timing(plain, [phone("UNKNOWN", 0.1, 0.4)])
    tampered = bytearray(timed)
    tampered[44] ^= 1
    with pytest.raises(ValueError):
        read_timing(bytes(tampered))


def test_stress_token_does_not_insert_an_unspoken_rest_before_the_vowel():
    data = read_timing(attach_timing(wav_file(), [phone("θ", 0.1, 0.2), phone("ˈ", 0.2, 0.25), phone("æ", 0.25, 0.4)]))
    assert data["phonemes"][1]["start"] == 0.2


def test_reduced_article_vowel_keeps_new_cafe_sentences_animated():
    timing = read_timing(attach_timing(wav_file(), [phone("ɐ", 0.1, 0.2), phone("k", 0.2, 0.3)]))
    assert [p["phoneme"] for p in timing["phonemes"]] == ["ah", "k"]


def test_spoken_price_uses_timed_engine_and_shares_normalized_cache(monkeypatch):
    from app import tts

    received = []

    def synth(text, **kwargs):
        received.append(text)
        return [], 24000, [phone("f", 0.1, 0.4)]

    service = tts.Kokoro(
        SimpleNamespace(has_timings=True, create_timed=synth, tokenizer=SimpleNamespace(phonemize=lambda text, lang: text)), "fake"
    )
    monkeypatch.setattr(tts, "to_wav", lambda audio, rate: wav_file())
    monkeypatch.setattr(tts, "align_timing", lambda wav, cues: attach_timing(wav, [phone("f", 0.1, 0.4)]))
    first = service.synth("That is $5.", "af_heart", 1)
    replay = service.synth("That is 5 dollars.", "af_heart", 1)
    assert received == ["That is 5 dollars."]
    assert read_timing(first[0]) is not None
    assert replay[0] == first[0] and replay[2]


def test_unspoken_chunk_preserves_neighboring_reply_phoneme_times():
    import pytest

    from app import tts

    service = tts.Kokoro(SimpleNamespace(has_timings=True), "fake")
    silence, seconds, _ = service.synth("감사합니다!", "af_heart", 1)
    phrase = attach_timing(wav_file(), [phone("f", 0.1, 0.4)])
    joined, duration = tts.join_wavs([silence, phrase])
    timing = read_timing(joined)
    assert timing is not None, "Known silent chunks must not discard the spoken reply's timeline."
    assert duration == pytest.approx(seconds + 1)
    assert timing["phonemes"][0] == {"phoneme": "_", "start": 0, "end": seconds}
    assert timing["phonemes"][1]["start"] == pytest.approx(seconds + 0.1)
