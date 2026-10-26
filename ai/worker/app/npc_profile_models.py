# 자동 생성 파일. 직접 고치지 말고 원본 스키마를 고친 뒤 다시 만든다.
#   원본: docs/api/npc-ai-contracts/npc_shared_schemas_v2.json
#   다시 만들기: python ai/tools/generate_npc_profile_models.py

from __future__ import annotations

from typing import Annotated, Literal

from pydantic import BaseModel, ConfigDict, Field

type Identifier = Annotated[str, Field(max_length=128, min_length=1, pattern="\\S")]


class PersonalityTrait(BaseModel):
    model_config = ConfigDict(
        extra="forbid",
    )
    name: Annotated[str, Field(max_length=60, min_length=1, pattern="\\S")]
    """
    성격 특성의 짧은 이름.
    """
    description: Annotated[str, Field(max_length=300, min_length=1, pattern="\\S")]
    """
    그 특성이 이 NPC에게 어떻게 드러나는지에 대한 설명. 단순 라벨보다 구체적인 행동 경향.
    """


class PersonalityDimensions(BaseModel):
    """
    선택적 Big Five 형식의 0~1 좌표. 실제 심리검사 점수가 아니며 인격의 전부가 아니다. 제공 시 다섯 값 모두 필요. 미설정은 null 또는 필드 생략; 0은 미설정이 아니다.
    """

    model_config = ConfigDict(
        extra="forbid",
    )
    openness: Annotated[float, Field(ge=0.0, le=1.0)]
    """
    새로운 경험과 생각을 받아들이는 경향의 창작용 상대값.
    """
    conscientiousness: Annotated[float, Field(ge=0.0, le=1.0)]
    """
    계획·책임·정확성을 중시하는 경향의 창작용 상대값.
    """
    extraversion: Annotated[float, Field(ge=0.0, le=1.0)]
    """
    사회적 상호작용에 적극적으로 나서는 경향의 창작용 상대값.
    """
    agreeableness: Annotated[float, Field(ge=0.0, le=1.0)]
    """
    협조·배려를 우선하는 경향의 창작용 상대값.
    """
    neuroticism: Annotated[float, Field(ge=0.0, le=1.0)]
    """
    걱정·긴장 등 정서적 반응의 민감도를 표현하는 창작용 상대값.
    """


class Personality(BaseModel):
    model_config = ConfigDict(
        extra="forbid",
    )
    traits: Annotated[list[PersonalityTrait], Field(max_length=12, min_length=1)]
    dimensions: PersonalityDimensions | None = None
    """
    선택 사항. 미설정 시 수치 성향을 추정해서 채우지 않는다.
    """


type Catchphrase = Annotated[str, Field(max_length=120, min_length=1, pattern="\\S")]


class SpeechStyle(BaseModel):
    """
    언어 표현 경향. TTS 목소리·속도 설정이 아니다. 말버릇을 매 턴 반드시 반복하라는 지시가 아니다.
    """

    model_config = ConfigDict(
        extra="forbid",
    )
    tone: Annotated[str, Field(max_length=120, min_length=1, pattern="\\S")]
    formality: Literal["casual", "neutral", "formal"]
    verbosity: Literal["short", "medium", "long"]
    humor: Annotated[str | None, Field(max_length=200, min_length=1, pattern="\\S")] = None
    directness: Literal["indirect", "balanced", "direct"] | None = None
    catchphrases: Annotated[list[Catchphrase] | None, Field(max_length=6, min_length=0)] = None


type Boundary = Annotated[str, Field(max_length=300, min_length=1, pattern="\\S")]


class SocialBehavior(BaseModel):
    """
    사회적 반응의 기본 경향. 객체를 제공하면 네 키가 모두 필요. 미설정 설명은 null, 경계 목록은 [] 사용. 사용자별 현재 친밀도나 관계 이력이 아니다.
    """

    model_config = ConfigDict(
        extra="forbid",
    )
    first_meeting: Annotated[str | None, Field(alias="firstMeeting", max_length=400, min_length=1, pattern="\\S")]
    familiarity_expression: Annotated[str | None, Field(alias="familiarityExpression", max_length=400, min_length=1, pattern="\\S")]
    conflict_response: Annotated[str | None, Field(alias="conflictResponse", max_length=400, min_length=1, pattern="\\S")]
    boundaries: Annotated[list[Boundary], Field(max_length=8, min_length=0)]


class BackgroundExperience(BaseModel):
    """
    설정된 과거 경험과 현재 성격·행동에 미친 영향. 사용자의 실제 대화에서 얻은 기억과 구분.
    """

    model_config = ConfigDict(
        extra="forbid",
    )
    event: Annotated[str, Field(max_length=400, min_length=1, pattern="\\S")]
    influence: Annotated[str, Field(max_length=400, min_length=1, pattern="\\S")]


type Interest = Annotated[str, Field(max_length=120, min_length=1, pattern="\\S")]


class Background(BaseModel):
    """
    고정된 허구 캐릭터의 배경. 객체를 제공하면 세 키 모두 필요. 동적 메뉴·가격·예약·사용자별 기억은 넣지 않는다.
    """

    model_config = ConfigDict(
        extra="forbid",
    )
    summary: Annotated[str | None, Field(max_length=1500, min_length=1, pattern="\\S")]
    experiences: Annotated[list[BackgroundExperience], Field(max_length=8, min_length=0)]
    interests: Annotated[list[Interest], Field(max_length=10, min_length=0)]


class Habit(BaseModel):
    """
    반복 습관의 맥락과 경향. 애니메이션 명령이나 조건식 실행 코드가 아니다.
    """

    model_config = ConfigDict(
        extra="forbid",
    )
    trigger: Annotated[str, Field(max_length=200, min_length=1, pattern="\\S")]
    tendency: Annotated[str, Field(max_length=300, min_length=1, pattern="\\S")]


type Value = Annotated[str, Field(max_length=200, min_length=1, pattern="\\S")]


type Motivation = Annotated[str, Field(max_length=300, min_length=1, pattern="\\S")]


type Fear = Annotated[str, Field(max_length=200, min_length=1, pattern="\\S")]


type BehaviorRule = Annotated[str, Field(max_length=300, min_length=1, pattern="\\S")]


class NpcProfile(BaseModel):
    """
    설정 저장·초기 등록·추론 입력이 공유하는 유일한 인격 구조. MVP는 직접 작성. 선택 필드 생략은 미설정이며 서버가 임의의 인격 사실을 채우지 않는다.
    """

    model_config = ConfigDict(
        extra="forbid",
    )
    schema_version: Annotated[Literal["2.0"], Field(alias="schemaVersion")]
    """
    프로필 구조 버전. API 문서 버전·내용 수정 횟수와 구분.
    """
    profile_version: Annotated[int, Field(alias="profileVersion", ge=1)]
    """
    Backend가 관리하는 NPC별 프로필 내용 버전. 구조 마이그레이션을 포함해 저장 내용을 수정하면 증가.
    """
    name: Annotated[str, Field(max_length=80, min_length=1, pattern="\\S")]
    occupation: Annotated[str, Field(max_length=120, min_length=1, pattern="\\S")]
    age_range: Annotated[str | None, Field(alias="ageRange", max_length=40, min_length=1, pattern="\\S")] = None
    personality: Personality
    values: Annotated[list[Value], Field(max_length=8, min_length=0)]
    motivations: Annotated[list[Motivation], Field(max_length=8, min_length=0)]
    fears: Annotated[list[Fear] | None, Field(max_length=6, min_length=0)] = None
    social_behavior: Annotated[SocialBehavior | None, Field(alias="socialBehavior")] = None
    speech_style: Annotated[SpeechStyle, Field(alias="speechStyle")]
    background: Background | None = None
    habits: Annotated[list[Habit] | None, Field(max_length=8, min_length=0)] = None
    behavior_rules: Annotated[list[BehaviorRule], Field(alias="behaviorRules", max_length=12, min_length=0)]


class NpcConfiguration(BaseModel):
    """
    Backend 저장·초기 등록용 설정 데이터. HTTP 요청 본문을 뜻하지 않는다. 실제 현재 위치·사용자별 기억·API 자격 증명은 포함하지 않는다.
    """

    model_config = ConfigDict(
        extra="forbid",
    )
    npc_id: Annotated[Identifier, Field(alias="npcId")]
    npc_profile: Annotated[NpcProfile, Field(alias="npcProfile")]
    default_location_id: Annotated[Identifier, Field(alias="defaultLocationId")]
    voice_id: Annotated[str | None, Field(alias="voiceId", max_length=128, min_length=1, pattern="\\S")]
    is_active: Annotated[bool, Field(alias="isActive")]


type NpcConfigurationList = Annotated[list[NpcConfiguration], Field(max_length=1000, min_length=1)]
"""
초기 등록 파일의 배열. MVP 예시는 3개이며 3개로 스키마를 제한하지 않는다. npcId 중복·참조 존재·검토 여부는 별도 검증.
"""
