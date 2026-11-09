"""'이 상황에서 자주 써요' 후보 목록과 후처리. 모델 호출 없이 확인한다."""

import json

from feedback.situation_catalog import CATALOG_PATH, keep_useful, situation_candidates


class _Context:
    def __init__(self, scene_id, scene_description):
        self.scene_id = scene_id
        self.scene_description = scene_description


def test_npc_role_expressions_are_not_learner_practice_targets():
    situations = [
        {"expression": "You're all set.", "meaning_ko": "다 됐어요."},
        {"expression": "What can I get you?", "meaning_ko": "무엇을 드릴까요?", "speaker_role": "npc"},
        {"expression": "Have a good one!", "meaning_ko": "좋은 하루 보내세요!", "speaker_role": "both"},
    ]
    assert [item["expression"] for item in keep_useful(situations, [])] == ["Have a good one!"]


def test_provider_candidates_exclude_staff_only_lines():
    candidates = situation_candidates(_Request("HOTEL", "호텔 체크인 예약"))
    assert all(item["expression"] != "You're all set." for item in candidates)


class _Request:
    def __init__(self, scene_id, scene_description=""):
        self.context = _Context(scene_id, scene_description)


def test_catalog_has_enough_expressions_for_every_first_day_scene():
    items = json.loads(CATALOG_PATH.read_text(encoding="utf-8"))["items"]
    for scene in ("HOTEL", "CAFE", "SHOP", "TOWN"):
        own = [item for item in items if scene in item["scenes"]]
        assert len(own) >= 6, scene
        assert sum(item["kind"] == "idiom" for item in own) >= 3, scene


def test_candidates_follow_the_stage_and_put_idioms_first():
    request = _Request("HOTEL", "호텔 · 호텔 직원 Jake · 체크인까지 시간이 남았어요. 할 일: 예약 확인, 캐리어 맡기기")

    candidates = situation_candidates(request)

    assert 0 < len(candidates) <= 8
    assert candidates[0]["kind"] == "idiom"
    assert "hang on to" in candidates[0]["expression"]


def test_candidates_never_cross_into_another_scene():
    items = {item["id"]: item for item in json.loads(CATALOG_PATH.read_text(encoding="utf-8"))["items"]}

    candidates = situation_candidates(_Request("SHOP", "드럭스토어 · 우산 사기"))

    assert candidates
    assert all("SHOP" in items[item["id"]]["scenes"] for item in candidates)


def test_keep_useful_drops_spoken_and_mixed_language_items():
    situations = [
        {"expression": "I'll take it.", "meaning_ko": "이걸로 할게요."},
        {"expression": "Do you carry umbrellas?", "meaning_ko": "Do you sell umbrellas?"},
        {"expression": "우산 있어요?", "meaning_ko": "우산 있어요?"},
        {"expression": "It's pouring out there!", "meaning_ko": "밖에 비가 엄청 쏟아져요!"},
    ]

    kept = keep_useful(situations, ["Okay, I'll take it, thanks."])

    assert [item["expression"] for item in kept] == ["It's pouring out there!"]
