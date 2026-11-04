"""Spring ↔ AI 워커 요청 형식. AI 워커는 상태를 저장하지 않으므로 필요한 것은 Spring이 매 요청에 담아 보낸다.
명세: docs/api/ai-worker.md"""

import re
from typing import Annotated, Literal

from pydantic import BaseModel, Field, field_validator

from .npc_profile_models import NpcProfile

# 대화 난이도 1~3 (백엔드 levelApplied). 1: 천천히 쉬운 단어·짧은 문장, 2: 자연스러운 일상 영어, 3: 원래 말투 그대로.
# NPC 대답의 문장 수·길이·어휘, 말 속도, 힌트·추천 단어 수준이 바뀐다 (판정은 영향 없음). 규칙은 prompts.LEVEL_RULES
Level = Literal[1, 2, 3]


def not_blank(v: str) -> str:
    if not v.strip():
        raise ValueError("빈 문장입니다")
    return v


class Npc(BaseModel):
    id: str
    name: str
    role: str
    facts: str = ""
    scene: str = ""
    tts_voice: str = "af_heart"
    tts_speed: float = Field(default=1.0, ge=0.5, le=2.0)  # 이 NPC의 타고난 말 속도. 난이도 배율을 곱한다
    # 공통 인격 스키마의 인격(백엔드 인격 설정의 npcProfile 그대로). 있으면 NPC 대답을 공통 규칙 + 인격으로 만든다 (app.npc_prompt)
    profile: NpcProfile | None = None


class Line(BaseModel):
    who: Literal["npc", "me"]
    text: str = Field(max_length=1000)
    english: str | None = None  # 학습자 줄: 번역 (대화 종료 API에서 씀)
    rude: bool = False  # 학습자 줄: 무례 판정 결과
    events: list[str] = []  # 학습자 줄: 의도 판정(/v1/judge)이 고른 대화 사건. 대화 중 기분과 이미 센 사건을 거르는 데 쓴다


class Intent(BaseModel):
    id: str
    desc: str


class ItemsSlot(BaseModel):
    """사는 장소(purchase)의 상품. 장소 파일의 catalog에서 Spring이 만든다."""

    catalog: list[str] = Field(min_length=1)
    iced_ok: list[str] = []  # 아이스로 고를 수 있는 상품 (없으면 iced 칸 자체가 없음)
    catalog_word: str = "menu"  # 프롬프트에서 상품 목록을 부르는 말 (menu, products ...)
    vague_words: str = "a general word"  # 종류 없이 말하면 주문으로 받지 않을 말 ("'coffee' or 'drink'")


class OptionSlot(BaseModel):
    """고르기 다음의 선택지 (카페의 매장/포장). 약국처럼 없으면 생략."""

    slot: str  # 응답 slots에 들어갈 이름 (choice)
    labels: list[str] = Field(min_length=1)  # 고를 수 있는 값 (here, to_go)
    prompt: str  # 무엇을 말했을 때 채우는지 ("where they will have it")


class Slots(BaseModel):
    items: ItemsSlot | None = None
    option: OptionSlot | None = None


class State(BaseModel):
    id: str
    desc: str
    intents: list[Intent] = Field(min_length=1)
    slots: Slots = Slots()
    current: dict = {}  # 지금 값 (예: {"order": "a latte"})
    more_intents: list[Intent] = []  # 같은 줄에서 이어서 할 수 있는 다음 단계 의도 (예: 포장 고르기 중의 '결제').
    # 판정은 말한 순서대로 여러 개를 돌려주고, Spring이 차례로 적용한다


class GuardRequest(BaseModel):
    npc: Npc
    history: list[Line] = Field(default=[], max_length=40)  # 판정은 마지막 4줄, 로컬 판정기는 마지막 NPC 줄만 본다
    text: str = Field(max_length=500)

    check_text = field_validator("text")(not_blank)


class JudgeRequest(BaseModel):
    npc: Npc
    scene: str = ""  # 비면 npc.scene
    state: State
    history: list[Line] = Field(default=[], max_length=40)
    text: str = Field(max_length=500)

    check_text = field_validator("text")(not_blank)


class Memory(BaseModel):
    visits: int = 0
    last_order: str | None = None
    card: dict | None = None


class Relationship(BaseModel):
    """사용자 × NPC 관계 (app.npc_relationship). 백엔드가 저장한 값과 마지막 대화 뒤 지난 게임 속 날 수. 처음 만나면 모두 0."""

    recognition: int = Field(default=0, ge=0, le=100)  # 알아봄
    affinity: int = Field(default=0, ge=-100, le=100)  # 호감
    days_since_last_talk: int = Field(default=0, ge=0)


class Learner(BaseModel):
    """대답 상대인 학습자. 캐릭터 기본형(F·M)과 닉네임이며, NPC가 부르는 호칭을 정할 때만 쓴다."""

    nickname: str | None = Field(default=None, max_length=32)
    avatar_type: Literal["F", "M"] | None = None


class ReplyRequest(BaseModel):
    npc: Npc
    memory: Memory | None = None
    relationship: Relationship | None = None  # 있으면 알아봄·호감·기분을 말로 풀어 지시문에 넣고, 알아볼 때만 평범한 기억을 떠올린다
    history: list[Line] = Field(default=[], max_length=40)
    text: str = Field(max_length=500)  # 학습자 원문 그대로 (한국어 섞인 채)
    instruction: str = Field(default="", max_length=3000)  # Spring 규칙이 정한 이번 대답에서 할 일. 퀘스트 단계 지시와 진행 규칙을 합치면 1500자 안팎이다
    tts: bool = True
    level: Level = 2
    suggest_words: bool = False  # true면 대답이 끝난 뒤 추천 단어를 만들어 스트림의 words 이벤트로 (음성 합성과 동시에)
    goal: str = Field(default="", max_length=300)  # 추천 단어용: 지금 상태에서 학습자가 할 일
    learner: Learner | None = None  # 있으면 캐릭터에 맞는 호칭만, 없으면 성별을 짐작한 호칭(Mr.·Ms.·sir·ma'am)을 쓰지 않는다


class TtsRequest(BaseModel):
    text: str = Field(min_length=1, max_length=400)
    voice: str = "af_heart"
    speed: float = Field(default=1.0, ge=0.5, le=2.0)


class HintRequest(BaseModel):
    npc: Npc
    scene: str = ""
    goal: str
    history: list[Line] = Field(default=[], max_length=40)
    level: Level = 2


class MeaningRequest(BaseModel):
    """NPC 대사 한 줄의 한국어 뜻과 단어 풀이 (백엔드 CONV-04 뜻 보기)."""

    npc: Npc
    text: str = Field(max_length=1000)  # NPC가 한 말
    level: Level = 2  # 단어 풀이에 어떤 말을 고를지 (쉬운 단계일수록 기본 단어도)

    check_text = field_validator("text")(not_blank)


class SuggestRequest(BaseModel):
    """학습자가 다음에 쓸 만한 단어 3개 (백엔드 CONV-01·03 suggestWords). history의 마지막 NPC 줄에 대한 대답용."""

    npc: Npc
    scene: str = ""  # 비면 npc.scene
    goal: str = Field(default="", max_length=300)  # 지금 상태에서 학습자가 할 일 (없으면 자연스러운 대답)
    history: list[Line] = Field(default=[], max_length=40)
    level: Level = 2


class MemoryRequest(BaseModel):
    """대화가 끝난 뒤 NPC 기억 카드 갱신. 피드백은 /v1/feedback (ai/feedback 계약)."""

    npc: Npc
    outcome: str  # 대화 결과 한 줄 (예: "주문 성공")
    history: list[Line] = Field(max_length=200)
    memory_card: dict | None = None  # 이전 카드


class RelationshipRequest(BaseModel):
    """정상 종료된 대화 하나를 알아봄에 반영 (app.npc_relationship). 백엔드가 /v1/memory와 같은 자리에서 부른다.
    호감은 대화 중에 발화마다 이미 바꿨으므로 여기서 계산하지 않는다."""

    npc: Npc  # profile이 있으면 인격 카드의 성격으로 알아보는 속도를 정한다
    relationship: Relationship | None = None  # 저장된 관계와 마지막 대화 뒤 지난 게임 날 수. 처음 만나면 없음
    history: list[Line] = Field(max_length=200)  # 학습자가 말한 대화인지만 본다

    @field_validator("history")
    @classmethod
    def learner_spoke(cls, v: list[Line]) -> list[Line]:
        if not any(line.who == "me" for line in v):
            raise ValueError("학습자가 말하지 않은 대화는 관계에 반영하지 않습니다")
        return v


# ---------- NPC 인격 카드 조합 (어드민 랜덤 생성, app.npc_persona) ----------
Axis = Literal["openness", "conscientiousness", "extraversion", "agreeableness", "neuroticism"]
Role = Literal["STAFF", "PASSERBY"]
Leans = dict[Axis, Literal["high", "low"]]  # 이 성격 쪽 NPC만 고른다 (축: high/low)
Phrase = Annotated[str, Field(min_length=1, max_length=200, pattern=r"\S")]


class CandidateText(BaseModel):
    """가치관·동기·관심사 후보 하나. leans·roles가 없으면 누구나 고를 수 있다."""

    text: Phrase
    leans: Leans | None = Field(default=None, min_length=1)
    roles: list[Role] | None = Field(default=None, min_length=1)


class CandidateOccupation(BaseModel):
    text: Annotated[str, Field(min_length=1, max_length=120, pattern=r"\S")]
    role: Role
    place: str | None = None  # 직원은 일하는 장소
    ages: list[Phrase] | None = Field(default=None, min_length=1)  # 이 직업의 나이대. 없으면 모든 나이대


class CandidateTrait(BaseModel):
    """성격 특징 후보 하나. 성격 축마다 NPC가 기운 쪽에서 하나씩 고르므로 축은 하나만."""

    name: Annotated[str, Field(min_length=1, max_length=60, pattern=r"\S")]
    description: Annotated[str, Field(min_length=1, max_length=300, pattern=r"\S")]
    leans: Leans = Field(min_length=1, max_length=1)


class PersonaCandidates(BaseModel):
    """백엔드 DB의 활성 후보. 보낸 목록만 AI 기본 재료를 바꾸고, 말투·사회적 행동 규칙은 늘 AI 기본 재료다."""

    names: list[Annotated[str, Field(min_length=1, max_length=80, pattern=r"\S")]] | None = None
    age_ranges: list[Phrase] | None = None
    occupations: list[CandidateOccupation] | None = None
    traits: list[CandidateTrait] | None = None
    values: list[CandidateText] | None = None
    motivations: list[CandidateText] | None = None
    interests: list[CandidateText] | None = None

    def as_ingredients(self) -> dict:
        """재료 파일(ai/npcs/rules/persona-ingredients.json)과 같은 모양. 보내지 않은 목록은 빠진다."""
        lists = self.model_dump(exclude_none=True)
        if "age_ranges" in lists:
            lists["ageRanges"] = lists.pop("age_ranges")
        return lists


class PersonaFixed(BaseModel):
    """어드민에서 직접 고른 칸. 비운 칸만 후보에서 고른다."""

    name: Annotated[str, Field(min_length=1, max_length=80, pattern=r"\S")] | None = None
    occupation: Annotated[str, Field(min_length=1, max_length=120, pattern=r"\S")] | None = None
    age_range: Annotated[str, Field(min_length=1, max_length=40, pattern=r"\S")] | None = None
    values: list[Phrase] | None = Field(default=None, min_length=1, max_length=8)
    motivations: list[Phrase] | None = Field(default=None, min_length=1, max_length=8)
    interests: list[Annotated[str, Field(min_length=1, max_length=120, pattern=r"\S")]] | None = Field(
        default=None, min_length=1, max_length=10
    )

    def as_profile_fields(self) -> dict:
        """인격 카드 칸 이름(ageRange)으로. 비운 칸은 빠진다."""
        fields = self.model_dump(exclude_none=True)
        if "age_range" in fields:
            fields["ageRange"] = fields.pop("age_range")
        return fields


MBTI_FORM = re.compile(r"^[EI][NS][FT][JP](-[AT])?$")


def mbti_form(v: str | None) -> str | None:
    if v is not None and not MBTI_FORM.match(v.upper()):
        raise ValueError("MBTI 형식이 아닙니다 (예: ISTP, ISTP-T)")
    return v and v.upper()


class PersonaGenerateRequest(BaseModel):
    """인격 카드 초안 하나. npc_id가 씨앗이라 같은 요청(재료 포함)이면 같은 NPC다."""

    npc_id: Annotated[str, Field(min_length=1, max_length=128, pattern=r"\S")]
    role: Role
    place: Annotated[str, Field(min_length=1, max_length=64, pattern=r"\S")]  # 기본 장소 (예: CAFE)
    mbti: str | None = None  # 비우면 npc_id로 고른다
    fixed: PersonaFixed = PersonaFixed()
    candidates: PersonaCandidates = PersonaCandidates()
    taken_names: list[str] = Field(default=[], max_length=1000)  # 다른 NPC가 쓰는 이름 (고르지 않음)
    narrate: bool = False  # true면 LLM이 사연(배경 이야기·겪은 일)을 쓴다

    check_mbti = field_validator("mbti")(mbti_form)


class PersonaDeriveRequest(BaseModel):
    """저장된 인격 카드의 자동 칸(성격 5축·행동 조각)을 MBTI로 다시 계산. 카드는 공통 인격 모델 그대로(camelCase)."""

    npc_id: Annotated[str, Field(min_length=1, max_length=128, pattern=r"\S")]  # 행동 조각 씨앗
    mbti: str
    profile: dict

    check_mbti = field_validator("mbti")(mbti_form)
