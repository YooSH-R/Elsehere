"""공통 인격 스키마에서 자동 생성한 모델(app/npc_profile_models.py)이 스키마·계약 예시와 맞는지."""

import json
import sys
from pathlib import Path

import pytest
from pydantic import TypeAdapter, ValidationError

from app.npc_profile_models import NpcConfiguration, NpcConfigurationList

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
import generate_npc_profile_models

EXAMPLES = generate_npc_profile_models.ROOT / "docs/api/npc-ai-contracts/npc_configuration_examples_v2.json"
CONFIGURATION_LIST = TypeAdapter(NpcConfigurationList)  # 목록은 클래스가 아닌 타입 별칭이라 TypeAdapter로 읽고 내보낸다


def load_examples() -> list[dict]:
    return json.loads(EXAMPLES.read_text(encoding="utf-8"))


def cafe_with(path: str, value) -> dict:
    """계약 예시의 카페 점원에서 path(점으로 구분한 키) 한 곳만 value로 바꾼 설정."""
    cafe = load_examples()[0]
    *parents, key = path.split(".")
    target = cafe
    for name in parents:
        target = target[name]
    target[key] = value
    return cafe


def test_committed_models_are_what_the_schema_generates_now():
    """스키마만 고치고 다시 생성하지 않았거나 생성 파일을 손으로 고쳤으면 실패한다."""
    committed = generate_npc_profile_models.OUTPUT.read_text(encoding="utf-8")
    assert generate_npc_profile_models.render() == committed


def test_contract_examples_load_with_python_field_names():
    cafe, hotel, passerby = CONFIGURATION_LIST.validate_python(load_examples())

    assert cafe.npc_id == "npc_cafe_001"
    assert cafe.npc_profile.speech_style.formality == "casual"
    assert cafe.npc_profile.speech_style.catchphrases == ["Take your time."]
    assert hotel.voice_id is None
    assert hotel.is_active is False
    assert passerby.npc_profile.personality.dimensions is None


def test_loaded_examples_dump_back_to_the_contract_json():
    """백엔드에 넘길 때는 계약 이름(camelCase)으로 되돌리고, 보내지 않은 선택 필드를 null로 채우지 않는다."""
    examples = load_examples()
    loaded = CONFIGURATION_LIST.validate_python(examples)

    assert CONFIGURATION_LIST.dump_python(loaded, mode="json", by_alias=True, exclude_unset=True) == examples


@pytest.mark.parametrize(
    ("path", "value"),
    [
        ("nickname", "Em"),  # 스키마에 없는 키
        ("npcProfile.name", "   "),  # 공백뿐인 글
        ("npcProfile.schemaVersion", "1.0"),  # 다른 구조 버전
        ("npcProfile.speechStyle.formality", "rude"),  # 정해진 값 밖
        ("npcProfile.speechStyle.catchphrases", ["x" * 121]),  # 목록 항목의 길이 상한
        ("npcProfile.personality.traits", []),  # 목록의 최소 개수
    ],
)
def test_models_reject_values_the_schema_rejects(path, value):
    with pytest.raises(ValidationError):
        NpcConfiguration.model_validate(cafe_with(path, value))
