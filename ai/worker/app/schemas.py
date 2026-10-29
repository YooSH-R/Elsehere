"""Spring ↔ AI 워커 요청 형식. AI 워커는 상태를 저장하지 않으므로 필요한 것은 Spring이 매 요청에 담아 보낸다.
명세: docs/api/ai-worker.md"""

from typing import Literal

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


class ReplyRequest(BaseModel):
    npc: Npc
    memory: Memory | None = None
    history: list[Line] = Field(default=[], max_length=40)
    text: str = Field(max_length=500)  # 학습자 원문 그대로 (한국어 섞인 채)
    instruction: str = Field(default="", max_length=1000)  # Spring 규칙이 정한 이번 대답에서 할 일
    tts: bool = True
    level: Level = 2
    suggest_words: bool = False  # true면 대답이 끝난 뒤 추천 단어를 만들어 스트림의 words 이벤트로 (음성 합성과 동시에)
    goal: str = Field(default="", max_length=300)  # 추천 단어용: 지금 상태에서 학습자가 할 일


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
