"""ai/tools/tts_roundtrip.py: TTS 왕복 평가의 채점(받아쓴 글과 원문 비교)과 평가 문장 파일. 합성·받아쓰기 모델은 부르지 않는다."""

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import tts_roundtrip

AI = Path(__file__).resolve().parents[2]


def expand_not(text: str) -> str:
    """Whisper 영어 정규화처럼 곧은 아포스트로피의 n't만 푼다."""
    return text.replace("n't", " not")


def test_curly_apostrophe_matches_straight_one():
    assert tts_roundtrip.score("That’s not okay.", "That's not okay.")["wer"] == 0


def test_curly_apostrophe_reaches_normalizer_as_straight_one():
    assert tts_roundtrip.words("I don’t know.", expand_not) == ["i", "do", "not", "know"]


def test_case_and_punctuation_are_ignored():
    assert tts_roundtrip.score("Hi! Welcome in.", "hi, welcome in")["wer"] == 0


def test_joined_or_split_words_count_as_same():
    assert tts_roundtrip.score("I got my haircut.", "I got my hair cut.")["wer"] == 0
    assert tts_roundtrip.score("It’s lowkey good.", "It's low-key good.")["wer"] == 0


def test_split_word_is_joined_even_if_its_first_part_appears_elsewhere():
    ref = "Check-in is from 3 p.m., and check-out is by 11 a.m."
    hyp = "Check-in is from 3 p.m. and checkout is by 11 a.m."

    assert tts_roundtrip.score(ref, hyp)["wer"] == 0


def test_misread_words_are_reported_as_one_span():
    result = tts_roundtrip.score("Comin’ right up!", "Common write-up.")

    assert result["wer"] == 2 / 3
    assert result["errors"] == [{"ref": "comin right", "hyp": "common write"}]


def test_dropped_and_added_words_are_reported():
    result = tts_roundtrip.score("Your room is 305.", "Your room is 3 0 5.")

    assert result["errors"] == [{"ref": "305", "hyp": "3 0 5"}]
    assert result["wer"] == 3 / 4


def test_summary_weights_wer_by_sentence_length():
    rows = [
        {"tags": ["price"], "ref_words": 4, "wer": 0.5, "errors": [{"ref": "x", "hyp": "y"}]},
        {"tags": ["plain"], "ref_words": 6, "wer": 0.0, "errors": []},
    ]

    groups = {g["group"]: g for g in tts_roundtrip.summarize(rows)}

    assert groups["전체"] == {"group": "전체", "sentences": 2, "wer": 0.2, "exact": 1, "with_errors": 1}
    assert groups["price"]["wer"] == 0.5
    assert groups["plain"]["exact"] == 1


def test_set_file_rows_are_valid():
    rows = tts_roundtrip.load_set(tts_roundtrip.DEFAULT_SET)
    npc_ids = {json.loads(p.read_text(encoding="utf-8"))["npcId"] for p in (AI / "npcs").glob("*.json")}

    assert len({r["id"] for r in rows}) == len(rows)
    for r in rows:
        assert r["text"].strip(), r["id"]
        assert r["npc"] in npc_ids, r["id"]
        assert r["tags"] and set(r["tags"]) <= tts_roundtrip.TAGS, r["id"]
