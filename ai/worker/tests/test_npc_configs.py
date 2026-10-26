"""ai/tools/npc_configs.py: NPC 설정을 공통 인격 스키마 원본으로 검사 — 생성 모델(Pydantic)이 놓치는 규칙까지."""

import sys
from pathlib import Path

import pytest

from conftest import cafe_with, load_npc_examples

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import npc_configs


def broken_places(config: dict) -> list[str]:
    return [where for where, _ in npc_configs.schema_errors(config)]


def test_contract_examples_pass_the_schema():
    for config in load_npc_examples():
        assert npc_configs.schema_errors(config) == []


def test_an_active_npc_needs_a_voice():
    """조건 규칙: isActive가 true면 voiceId가 있어야 한다. 계약 예시처럼 비활성 NPC는 null이어도 된다."""
    silent = cafe_with("isActive", True)  # 예시의 voiceId는 null
    voiced = {**silent, "voiceId": "af_heart"}

    assert broken_places(silent) == ["$.voiceId"]
    assert broken_places(voiced) == []


@pytest.mark.parametrize(
    ("path", "value"),
    [
        ("npcProfile.values", ["같은 말", "같은 말"]),  # 목록 중복 금지
        ("npcProfile.fears", None),  # 생략은 되지만 null은 안 됨
        ("npcProfile.speechStyle.directness", None),
        ("npcProfile.profileVersion", "1"),  # 글을 숫자로 바꿔 받지 않음
        ("isActive", "false"),  # 글을 참거짓으로 바꿔 받지 않음
    ],
)
def test_rules_the_generated_models_miss_are_reported_where_they_break(path, value):
    assert set(broken_places(cafe_with(path, value))) == {f"$.{path}"}


def test_every_broken_rule_is_reported_at_once():
    """작성자가 한 번에 고칠 수 있게 첫 오류에서 멈추지 않고, 위치 순으로 모두 알려 준다."""
    config = cafe_with("npcProfile.fears", None)
    config["isActive"] = "false"

    assert broken_places(config) == ["$.isActive", "$.npcProfile.fears"]
