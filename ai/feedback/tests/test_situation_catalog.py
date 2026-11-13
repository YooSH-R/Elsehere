"""고정 참고 표현의 장소·발화 조건과 추천 결과 후처리."""

import json
import re

from feedback.prompt import SYSTEM_PATH
from feedback.situation_catalog import CATALOG_PATH, keep_useful, situation_candidate

SAMPLE_NAMES = re.compile(r"\b(Kim|Park|Lee|Choi)\b")


def test_catalog_never_hands_the_learner_a_sample_name():
    # 학습자 것이 아닌 이름을 따라 말하게 하지 않는다 (S15P31A305-845).
    items = json.loads(CATALOG_PATH.read_text(encoding="utf-8"))["items"]
    named = [item["id"] for item in items if SAMPLE_NAMES.search(item["expression"] + " " + item["example_en"])]
    assert named == []


def test_new_expressions_leave_out_names_and_other_personal_answers():
    prompt = SYSTEM_PATH.read_text(encoding="utf-8")
    assert "이름(예시 이름 포함)·날짜·방 번호 같은 개인 정보 답은 표현과 예문에 넣지 않는다" in prompt


def test_npc_role_expressions_are_not_learner_practice_targets():
    situations = [
        {"expression": "You're all set.", "meaning_ko": "다 됐어요."},
        {"expression": "What can I get you?", "meaning_ko": "무엇을 드릴까요?", "speaker_role": "npc"},
        {"expression": "Have a good one!", "meaning_ko": "좋은 하루 보내세요!", "speaker_role": "both"},
    ]
    assert [item["expression"] for item in keep_useful(situations, [])] == ["Have a good one!"]


def test_provider_candidates_exclude_staff_only_lines():
    candidate = situation_candidate("HOTEL", ["I'd like to check in for my reservation."])
    assert candidate["expression"] == "I have a reservation."


def test_candidate_matches_the_learner_words_instead_of_other_place_topics():
    candidate = situation_candidate("HOTEL", ["Could you store my luggage for a few hours?"])
    assert "hang on to" in candidate["expression"]


def test_same_words_only_select_a_fixed_example_at_the_matching_shop():
    user_texts = ["크루아상 두 개요."]
    assert situation_candidate("BAKERY", user_texts)["expression"] == "Could I get two croissants, please?"
    assert situation_candidate("BANK", user_texts) is None
    assert situation_candidate("TOWN", user_texts) is None


def test_fixed_example_is_skipped_when_already_said_or_off_topic():
    assert situation_candidate("BAKERY", ["Could I get two croissants, please?"]) is None
    assert situation_candidate("BAKERY", ["Is the post office far from here?"]) is None


def test_keep_useful_drops_spoken_and_mixed_language_items():
    situations = [
        {"expression": "I'll take it.", "meaning_ko": "이걸로 할게요."},
        {"expression": "Do you carry umbrellas?", "meaning_ko": "Do you sell umbrellas?"},
        {"expression": "우산 있어요?", "meaning_ko": "우산 있어요?"},
        {"expression": "It's pouring out there!", "meaning_ko": "밖에 비가 엄청 쏟아져요!"},
    ]
    kept = keep_useful(situations, ["Okay, I'll take it, thanks."])
    assert [item["expression"] for item in kept] == ["It's pouring out there!"]
