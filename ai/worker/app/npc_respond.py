"""NPC 추론 API (계약 v2, POST /internal/ai/npc/respond). 백엔드가 매번 보내는 인격·상황·이력으로 NPC의 다음 대사를 만든다.

무상태: npcId로 아무것도 조회하지 않고 받은 것만 쓴다. 대사는 app.npc_prompt의 공통 시스템 프롬프트로 만든다.
인격(npcProfile)은 공통 스키마에서 생성한 모델(app.npc_profile_models)로 검사하고, 그 바깥의 요청 형식만 여기 적는다.
계약: docs/api/npc-ai-contracts/02_npc_inference_api_spec_v2.md
"""

import json
from typing import Annotated, Literal

from pydantic import BaseModel, ConfigDict, Field, ValidationError, model_validator

from .llm import LLM, Usage
from .npc_profile_models import NpcProfile
from .npc_prompt import npc_messages

MAX_REPLY_TOKENS = 150  # 대답 길이는 학습 수준 규칙이 정하고, 이 값은 넘치지 않게 막는 상한 (ai/tools/npc_chat.py와 같은 값)
REPLY_TEMPERATURE = 0.6  # 같은 말에도 매번 똑같이 답하지 않게 (기존 NPC 스트림과 같은 값)

# 계약의 문자열 규칙: 1자 이상, 공백만은 안 됨, 칸마다 길이 상한
Identifier = Annotated[str, Field(min_length=1, max_length=128, pattern=r"\S")]
Line = Annotated[str, Field(min_length=1, max_length=2000, pattern=r"\S")]
Fact = Annotated[str, Field(min_length=1, max_length=500, pattern=r"\S")]


class ContractModel(BaseModel):
    model_config = ConfigDict(extra="forbid")  # 계약에 없는 키는 거부


class Scenario(ContractModel):
    scenarioId: Identifier | None
    locationId: Identifier
    situation: Line
    facts: Annotated[list[Fact], Field(max_length=30)]


class HistoryMessage(ContractModel):
    speaker: Literal["NPC", "USER"]
    text: Line


class NpcRespondRequest(ContractModel):
    """모든 키가 필수다 (userText·scenarioId는 null일 수는 있어도 빠지면 안 됨)."""

    requestId: Identifier
    sessionId: Identifier
    npcId: Identifier
    turnType: Literal["NPC_OPENING", "USER_MESSAGE"]
    userText: Line | None
    userLevel: Literal["A1", "A2", "B1", "B2", "C1", "C2"]
    npcProfile: NpcProfile
    scenario: Scenario
    conversationHistory: Annotated[list[HistoryMessage], Field(max_length=20)]

    @model_validator(mode="after")
    def check_turn(self) -> "NpcRespondRequest":
        """첫 대사·후속 대사 조건 (계약 4.1·6). 스키마의 if/then과, 스키마로는 못 거르는 '마지막 이력 화자는 NPC'를 함께 본다."""
        if self.turnType == "NPC_OPENING":
            if self.userText is not None or self.conversationHistory:
                raise ValueError("NPC_OPENING에는 userText가 null, conversationHistory가 []여야 합니다")
        elif self.userText is None or not self.conversationHistory or self.conversationHistory[-1].speaker != "NPC":
            raise ValueError("USER_MESSAGE에는 userText와, 마지막 화자가 NPC인 conversationHistory가 필요합니다")
        return self


class ConversationControl(BaseModel):
    action: Literal["CONTINUE", "OFFER_END"]
    reason: Literal["USER_END_INTENT", "NATURAL_WRAP_UP"] | None


class NpcRespondResponse(BaseModel):
    requestId: str
    sessionId: str
    npcId: str
    replyText: str
    conversationControl: ConversationControl


class ContractError(Exception):
    """계약 9절 오류. 이 경로는 Worker 공통 오류 형식이 아니라 {requestId, error: {code, message, retryable}}로 답한다."""

    def __init__(self, http: int, code: str, message: str, retryable: bool, request_id: str | None):
        super().__init__(message)
        self.http, self.code, self.message, self.retryable, self.request_id = http, code, message, retryable, request_id

    def body(self) -> dict:
        return {"requestId": self.request_id, "error": {"code": self.code, "message": self.message[:1000], "retryable": self.retryable}}


def readable_request_id(payload: object) -> str | None:
    """오류 응답에 실을 requestId. 요청이 망가져 유효한 ID를 읽을 수 없으면 None (계약 9절)."""
    value = payload.get("requestId") if isinstance(payload, dict) else None
    return value if isinstance(value, str) and value.strip() and len(value) <= 128 else None


def load_payload(raw: bytes) -> object:
    """본문을 JSON으로 읽는다. JSON이 아니면 400 INVALID_JSON이고, ID를 읽을 수 없으니 requestId는 None."""
    try:
        return json.loads(raw)
    except ValueError as e:  # 깨진 JSON, UTF-8이 아닌 바이트 모두
        raise ContractError(400, "INVALID_JSON", f"본문이 JSON이 아닙니다 ({e})", False, None) from e


def parse_request(payload: object) -> NpcRespondRequest:
    """계약 형식 검사. 어기면 422 INVALID_REQUEST (재시도해도 같은 결과라 retryable=false)."""
    try:
        return NpcRespondRequest.model_validate(payload)
    except ValidationError as e:
        where = "; ".join(f"{'.'.join(str(x) for x in err['loc']) or '(본문)'}: {err['msg']}" for err in e.errors()[:5])
        raise ContractError(422, "INVALID_REQUEST", f"요청 형식 오류 ({where})", False, readable_request_id(payload)) from e


async def respond(llm: LLM, request: NpcRespondRequest) -> NpcRespondResponse:
    """요청 하나로 NPC 대사 하나. ID 세 개는 모델이 아니라 요청에서 그대로 옮긴다 (계약 7절)."""
    messages = npc_messages(request.model_dump(mode="json", by_alias=True))
    reply = await llm.chat("npc", "npc", messages, Usage(), temperature=REPLY_TEMPERATURE, max_tokens=MAX_REPLY_TOKENS)
    if not reply.strip():  # 계약상 replyText는 공백만이면 안 된다. 틀린 출력을 정상 응답으로 보내지 않는다 (계약 7절)
        raise ContractError(502, "INVALID_AI_OUTPUT", "모델이 빈 대사를 돌려줬습니다", True, request.requestId)
    return NpcRespondResponse(
        requestId=request.requestId,
        sessionId=request.sessionId,
        npcId=request.npcId,
        replyText=reply,
        conversationControl=ConversationControl(action="CONTINUE", reason=None),
    )
