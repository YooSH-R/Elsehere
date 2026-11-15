import json
import struct

import pytest
from test_api import REPLY, events
from test_speech_timing import phone, wav_file

from app.npc_emotion import EmotionStream
from app.speech_emotion import Mood, attach_emotion, read_emotions
from app.speech_timing import attach_timing, read_timing
from app.tts import join_wavs
from conftest import FakeTts, make_client, stream


def emotion_data(wav):
    offset = 12
    while offset + 8 <= len(wav):
        tag, size = struct.unpack_from("<4sI", wav, offset)
        offset += 8
        if tag == b"e305":
            return json.loads(wav[offset : offset + size])
        offset += size + size % 2
    return None


@pytest.mark.parametrize(
    ("reaction", "expected"),
    [("rude_warning", "frown"), ("rude_repeated", "angry"), ("rude_final", "furious")],
)
def test_confirmed_rudeness_reaction_overrides_smiling_model_in_live_and_saved_voice(provider, reaction, expected):
    provider.answers["npc"] = lambda body: stream(["[[warm_smile:0.4]]Please be respectful. [[laugh:0.5]]That is not okay."])
    with make_client(provider, FakeTts(wav=True)) as client:
        response = events(client.post("/v1/npc/reply", json={**REPLY, "reaction": reaction, "suggest_words": False}))
        chunks = [data for name, data in response if name == "audio"]
        for chunk in chunks:
            wav = client.get("/v1/audio/" + chunk["audio_id"]).content
            assert emotion_data(wav)["cues"][0]["emotion"] == expected
        done = next(data for name, data in response if name == "done")
        assert {phrase["emotion"] for phrase in done["speech_phrases"]} == {expected}
        full = client.get("/v1/audio/" + done["audio_full_id"]).content
        assert {cue["emotion"] for cue in emotion_data(full)["cues"]} == {expected}
        assert done["text"] == "Please be respectful. That is not okay."
        assert len(provider.calls) == 1


@pytest.mark.parametrize(
    ("reaction", "proposed", "expected"),
    [
        ("compliment", "warm_smile", "laugh"),
        ("compliment", "shy", "shy"),
        ("compliment", "laugh", "laugh"),
        ("apology_after_rude", "big_laugh", "relieved"),
        (None, "warm_smile", "warm_smile"),
    ],
)
def test_compliment_and_apology_are_distinct_from_an_ordinary_greeting(provider, reaction, proposed, expected):
    provider.answers["npc"] = lambda body: stream([f"[[{proposed}:0.5]]Thank you for saying that."])
    with make_client(provider, FakeTts(wav=True)) as client:
        response = events(client.post("/v1/npc/reply", json={**REPLY, "reaction": reaction, "suggest_words": False}))
        done = next(data for name, data in response if name == "done")
        assert done["speech_phrases"][0]["emotion"] == expected
        regenerated = client.post("/v1/tts", json={"text": done["text"], "phrases": done["speech_phrases"]})
        assert read_emotions(regenerated.content)["cues"][0]["emotion"] == expected
        assert len(provider.calls) == 1


RUDE_LINE = {"who": "me", "text": "[withheld]", "rude": True}
PLAIN_LINE = {"who": "me", "text": "Okay."}
COMPLIMENT_LINE = {"who": "me", "text": "You are so kind!", "events": ["COMPLIMENT"]}
NPC_LINE = {"who": "npc", "text": "Sure."}


def first_face(provider, history: list[dict], proposed: str | None, reaction: str | None = None) -> str | None:
    marker = f"[[{proposed}:0.5]]" if proposed else ""
    provider.answers["npc"] = lambda body: stream([f"{marker}What would you like?"])
    with make_client(provider, FakeTts(wav=True)) as client:
        body = {**REPLY, "history": history, "reaction": reaction, "suggest_words": False}
        done = next(data for name, data in events(client.post("/v1/npc/reply", json=body)) if name == "done")
        return done["speech_phrases"][0]["emotion"]


@pytest.mark.parametrize(
    ("history", "expected"),
    [
        ([RUDE_LINE, NPC_LINE], "frown"),  # 무례 바로 뒤: 강한 짜증
        ([RUDE_LINE, NPC_LINE, PLAIN_LINE, NPC_LINE, PLAIN_LINE, NPC_LINE], "indifferent"),  # 가라앉는 중: 약한 짜증
    ],
)
def test_a_lingering_annoyance_keeps_the_npc_from_smiling_in_later_turns(provider, history, expected):
    """무례가 있던 턴(서버 reaction)이 지나도 짜증이 남은 동안에는 모델이 웃는 표정을 골라도 차가운 얼굴로 바꾼다.
    분노를 강제하지는 않는다(표정 문서의 '무례 뒤 평범한 말에는 분노를 강제하지 않는다')."""
    assert first_face(provider, history, "warm_smile") == expected
    assert first_face(provider, history, "laugh") == expected


def test_an_annoyed_npc_without_a_face_marker_does_not_fall_back_to_a_smile(provider):
    """표시가 없으면 Unity가 기본 기분(인자한 미소)으로 재생하므로, 짜증이 남았으면 차가운 얼굴을 정해 준다."""
    assert first_face(provider, [RUDE_LINE, NPC_LINE], None) == "frown"


def test_an_annoyed_npc_may_still_look_concerned_or_calm(provider):
    """반대인 표정만 막고, 짜증과 어울리는 다른 표정은 그대로 둔다."""
    assert first_face(provider, [RUDE_LINE, NPC_LINE], "concerned") == "concerned"


def test_a_good_mood_keeps_the_npc_from_frowning(provider):
    assert first_face(provider, [COMPLIMENT_LINE, NPC_LINE], "frown") == "warm_smile"
    assert first_face(provider, [COMPLIMENT_LINE, NPC_LINE], "curious") == "curious"


def test_a_confirmed_reaction_this_turn_wins_over_the_lingering_mood(provider):
    """이번 턴에 서버가 사과를 확정하면 남은 짜증보다 사과 반응(relieved)을 따른다."""
    assert first_face(provider, [RUDE_LINE, NPC_LINE], "big_laugh", reaction="apology_after_rude") == "relieved"


def test_forced_reaction_does_not_split_synthesis_at_discarded_mood_markers(provider):
    provider.answers["npc"] = lambda body: stream(["[[warm_smile:0.4]]Please [[laugh:0.8]]stop that."])
    tts = FakeTts(wav=True)
    with make_client(provider, tts) as client:
        response = events(client.post("/v1/npc/reply", json={**REPLY, "reaction": "rude_repeated", "suggest_words": False}))
        done = next(data for name, data in response if name == "done")
        assert done["text"] == "Please stop that."
        assert tts.texts == ["Please stop that."]
        assert len(provider.calls) == 1


def test_real_reply_path_carries_phrase_emotions_without_speaking_tags_or_extra_llm_calls(provider):
    provider.answers["npc"] = lambda body: stream(list("[[curious:0.7]]Really? [[warm_smile:0.8]]I'm glad to hear that."))
    tts = FakeTts(wav=True)
    with make_client(provider, tts) as client:
        response = events(client.post("/v1/npc/reply", json={**REPLY, "suggest_words": False}))
        spoken = "".join(data["text"] for name, data in response if name == "delta")
        assert spoken == "Really? I'm glad to hear that."
        chunks = [data for name, data in response if name == "audio"]
        waves = [client.get("/v1/audio/" + data["audio_id"]).content for data in chunks]
        assert [emotion_data(wav)["cues"][0]["emotion"] for wav in waves] == ["curious", "warm_smile"]
        done = next(data for name, data in response if name == "done")
        full = client.get("/v1/audio/" + done["audio_full_id"]).content
        cues = emotion_data(full)["cues"]
        assert cues[1]["start"] == cues[0]["end"]
        assert cues[1]["intensity"] == 0.8
        assert len(provider.calls) == 1
        assert tts.texts == ["Really?", "I'm glad to hear that."]


@pytest.mark.parametrize("split", range(36))
def test_split_markers_never_leak_into_spoken_text(split):
    raw = "[[sad:0.6]]I'm sorry. [[warm_smile:0.7]]Let me help."
    parser = EmotionStream()
    result = parser.feed(raw[:split]) + parser.feed(raw[split:]) + parser.flush()
    assert "".join(text for text, _ in result) == "I'm sorry. Let me help."
    by_mood = {name: "".join(text for text, mood in result if mood and mood.emotion == name) for name in ("sad", "warm_smile")}
    assert by_mood == {"sad": "I'm sorry. ", "warm_smile": "Let me help."}


def test_invalid_and_truncated_markers_fall_back_without_being_read_aloud():
    parser = EmotionStream()
    parts = parser.feed("[[sad:nan]]Hello. [[unknown:1]]Welcome. [[curious:") + parser.flush()
    assert "".join(text for text, _ in parts) == "Hello. Welcome. "
    assert all(mood is None for _, mood in parts)


def test_metadata_preserves_pcm_phonemes_and_replay_offsets_and_rejects_other_audio():
    voiced = attach_timing(wav_file(), [phone("f", 0.1, 0.3)])
    first = attach_emotion(voiced, Mood("concerned", 0.6))
    assert read_timing(first) == read_timing(voiced)
    second = attach_emotion(voiced, Mood("warm_smile", 0.7))
    joined, duration = join_wavs([first, second])
    assert duration == 2
    assert read_timing(joined)["phonemes"][1]["start"] == 1.1
    assert read_emotions(joined)["cues"][1]["start"] == 1
    tampered = bytearray(first)
    tampered[44] ^= 1
    with pytest.raises(ValueError, match="do not belong"):
        read_emotions(bytes(tampered))


def test_late_voice_keeps_the_same_emotion_in_whole_reply(provider):
    provider.answers["npc"] = lambda body: stream(["[[frown:0.6]]Please wait."])
    with make_client(provider, FakeTts(wav=True, delay=0.03), tts_chunk_budget_s=0.01) as client:
        response = events(client.post("/v1/npc/reply", json={**REPLY, "suggest_words": False}))
        assert next(data for name, data in response if name == "audio")["tts"] is False
        done = next(data for name, data in response if name == "done")
        full = client.get("/v1/audio/" + done["audio_full_id"]).content
        assert read_emotions(full)["cues"][0]["emotion"] == "frown"


def test_invalid_emotion_duration_is_rejected_without_losing_joined_voice():
    first = attach_emotion(wav_file(), Mood("curious", 0.6))
    invalid = first.replace(b'"duration":1.0', b'"duration":NaN')
    with pytest.raises(ValueError, match="do not belong"):
        read_emotions(invalid)
    joined, duration = join_wavs([invalid, first])
    assert duration == 2
    assert read_emotions(joined)["cues"] == [{"start": 1, "end": 2, "emotion": "curious", "intensity": 0.6}]


def test_timed_out_reply_retains_phrases_for_later_synthesis_without_another_llm_call(provider):
    provider.answers["npc"] = lambda body: stream(["[[concerned:0.7]]Are you okay? [[warm_smile:0.6]]Let me help."])
    with make_client(provider, FakeTts(wav=True, delay=0.1), tts_chunk_budget_s=0.001, tts_full_budget_s=0.002) as client:
        reply = events(client.post("/v1/npc/reply", json={**REPLY, "suggest_words": False}))
        done = next(data for name, data in reply if name == "done")
        assert done["audio_full_id"] is None
        phrases = done["speech_phrases"]
        assert phrases == [
            {"text": "Are you okay?", "emotion": "concerned", "intensity": 0.7},
            {"text": "Let me help.", "emotion": "warm_smile", "intensity": 0.6},
        ]
        response = client.post("/v1/tts", json={"text": done["text"], "phrases": phrases})
        assert response.status_code == 200
        cues = read_emotions(response.content)["cues"]
        assert [cue["emotion"] for cue in cues] == ["concerned", "warm_smile"]
        assert cues[1]["start"] == cues[0]["end"]
        assert len(provider.calls) == 1


@pytest.mark.parametrize(
    "phrases",
    [
        [{"text": "Unrelated words", "emotion": "sad", "intensity": 0.7}],
        [{"text": "Hello.", "emotion": "unknown", "intensity": 0.5}],
        [{"text": "Hello.", "emotion": "sad", "intensity": 2}],
    ],
)
def test_recreated_speech_rejects_mismatched_or_invalid_emotion_phrases(provider, phrases):
    tts = FakeTts(wav=True)
    with make_client(provider, tts) as client:
        assert client.post("/v1/tts", json={"text": "Hello.", "phrases": phrases}).status_code == 400
        assert tts.texts == []
