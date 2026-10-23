"""126/127 입출력 계약의 참조 모델. LLM·HTTP·DB 호출을 수행하지 않는다.

Pydantic 2.x용이다. JSON Schema 자체는 특정 서버 언어에 종속되지 않는다.
"""

from typing import Annotated, Literal, Union

from pydantic import BaseModel, ConfigDict, Field, StringConstraints, TypeAdapter, model_validator

SCHEMA_VERSION = "0.1.0"
Id = Annotated[str, StringConstraints(min_length=1, max_length=128, pattern=r"^[A-Za-z0-9][A-Za-z0-9_.:-]*$")]
Text = Annotated[str, StringConstraints(min_length=1, pattern=r"\S")]
FactKey = Annotated[str, StringConstraints(min_length=1, max_length=128, pattern=r"^[a-z][a-z0-9_.]*$")]


class Contract(BaseModel):
    model_config = ConfigDict(extra="forbid", strict=True)


class Scope(Contract):
    user_id: Id = Field(description="서버가 인증·확인한 사용자 ID. LLM이 생성하거나 선택하지 않는다.")
    npc_id: Id
    conversation_id: Id = Field(description="특정 NPC와의 1회 대화. 로그인 세션과 구분한다.")


class ConversationContext(Contract):
    scene_id: Id
    mode: Literal["free_chat", "ambient", "quest"]
    scene_description: Text
    target_language: Literal["en-US"]
    explanation_language: Literal["ko-KR"]
    end_reason: Literal["user_end", "npc_end"] = Field(description="v0.1은 정상 종료 분석. 중단 대화 정책은 추가 합의한다.")


class DifficultySnapshot(Contract):
    settings_version: Id
    vocabulary_profile: Text = Field(description="합의된 설정 프로필의 실제 적용값. 언어 실력 등급이 아니다.")
    speech_rate_multiplier: Annotated[float, Field(gt=0)] | None
    hint_policy: Literal["on_request"]


class Assistance(Contract):
    assistance_id: Id
    kind: Literal["keyword_hint", "sentence_starter", "full_example", "translation", "model_audio", "replay"]
    content: Text | None = Field(description="이미 제공한 힌트·문장 등. 텍스트가 없는 로컬 재생은 null 가능.")


class Turn(Contract):
    turn_id: Id
    sequence: Annotated[int, Field(ge=1)]
    speaker: Literal["user", "npc", "system"]
    input_type: Literal["voice", "text", "generated", "system"]
    text_status: Literal["final", "needs_review", "failed", "blocked", "system_notice"]
    original_transcript: Text | None = Field(description="STT 원문. 타이핑·NPC·시스템 발화에서는 null.")
    final_text: Text | None = Field(description="사용자 수정 반영 후의 확정 텍스트. 인식 실패·차단은 null 가능.")
    difficulty: DifficultySnapshot
    assistance_ids: list[Id] = Field(description="이 사용자 발화에 실제 사용한 도움 ID. 없으면 빈 배열.")

    @model_validator(mode="after")
    def consistent(self):
        if self.text_status == "final" and self.final_text is None:
            raise ValueError("final 발화에는 final_text가 필요하다")
        if self.speaker == "user" and self.input_type not in ("voice", "text"):
            raise ValueError("사용자 발화 input_type은 voice 또는 text다")
        if self.speaker == "npc" and self.input_type != "generated":
            raise ValueError("NPC 발화 input_type은 generated다")
        if self.speaker == "system" and (self.input_type != "system" or self.text_status != "system_notice"):
            raise ValueError("시스템 안내는 system/system_notice로 구분한다")
        if self.speaker != "user" and self.assistance_ids:
            raise ValueError("도움 사용 ID는 사용자 발화에만 연결한다")
        if self.input_type != "voice" and self.original_transcript is not None:
            raise ValueError("음성이 아닌 발화의 original_transcript는 null이다")
        if len(set(self.assistance_ids)) != len(self.assistance_ids):
            raise ValueError("도움 ID 중복")
        return self


class BaseInput(Contract):
    schema_version: Literal["0.1.0"]
    request_id: Id = Field(description="서버가 부여한 요청 ID. 재시도 시 같은 분석 버전의 ID를 유지한다.")
    scope: Scope
    conversation_revision: Annotated[int, Field(ge=0)] = Field(
        description="서버가 확정한 대화 텍스트 스냅샷 버전. 전사 수정 뒤 재분석은 새 버전으로 요청한다."
    )
    analysis_config_version: Id = Field(description="서버가 관리하는 프롬프트·모델·평가 규칙 조합의 불변 버전.")
    context: ConversationContext
    turns: list[Turn] = Field(description="실제 순서대로 정리한 대화. 유효 사용자 발화가 없어도 자료 부족 응답을 위해 전달 가능.")
    assistance_events: list[Assistance]

    @model_validator(mode="after")
    def consistent(self):
        ids = [t.turn_id for t in self.turns]
        sequences = [t.sequence for t in self.turns]
        if len(set(ids)) != len(ids) or len(set(sequences)) != len(sequences):
            raise ValueError("turn_id와 sequence는 대화 안에서 유일해야 한다")
        if sequences != sorted(sequences):
            raise ValueError("turns는 sequence 오름차순이어야 한다")
        aids = [a.assistance_id for a in self.assistance_events]
        if len(set(aids)) != len(aids):
            raise ValueError("assistance_id 중복")
        for t in self.turns:
            if not set(t.assistance_ids).issubset(set(aids)):
                raise ValueError("존재하지 않는 도움 ID")
        return self


MemoryKind = Literal["in_game_preference", "in_game_profile", "past_interaction"]


class StoredFact(Contract):
    memory_id: Id = Field(description="기존 DB 식별자. 서버에서 조회한 값만 제공한다.")
    fact_key: FactKey
    kind: MemoryKind
    content: Text


class MemorySnapshot(Contract):
    version: Annotated[int, Field(ge=0)] = Field(description="조회한 사용자·NPC 기억 집합의 버전. 저장 시 최신 버전과 비교한다.")
    facts: list[StoredFact]

    @model_validator(mode="after")
    def unique(self):
        if len({f.memory_id for f in self.facts}) != len(self.facts):
            raise ValueError("memory_id 중복")
        if len({f.fact_key for f in self.facts}) != len(self.facts):
            raise ValueError("v0.1의 활성 사실은 fact_key당 한 개다")
        return self


class MemoryInput(BaseInput):
    memory_snapshot: MemorySnapshot = Field(description="scope의 동일 사용자·NPC에서 서버가 조회한 스냅샷.")


class FeedbackInput(BaseInput):
    pass


class Evidence(Contract):
    turn_id: Id = Field(description="현재 요청 대화의 발화 ID.")
    quote: Text = Field(description="해당 final_text에 정확히 포함된 인용. 수정 전 STT 텍스트를 근거로 삼지 않는다.")


class MemorySummary(Contract):
    text: Text
    evidence: Annotated[list[Evidence], Field(min_length=1)]


class AddFact(Contract):
    action: Literal["add"]
    fact_key: FactKey
    kind: MemoryKind
    content: Text
    evidence: Annotated[list[Evidence], Field(min_length=1)]


class ReplaceFact(Contract):
    action: Literal["replace"]
    memory_id: Id
    fact_key: FactKey
    kind: MemoryKind
    content: Text
    evidence: Annotated[list[Evidence], Field(min_length=1)]


class RetractFact(Contract):
    action: Literal["retract"] = Field(description="기존 사실의 비활성화 후보. 실제 삭제·보존 처리는 BE 정책이다.")
    memory_id: Id
    fact_key: FactKey
    reason_ko: Text
    evidence: Annotated[list[Evidence], Field(min_length=1)]


MemoryChange = Annotated[Union[AddFact, ReplaceFact, RetractFact], Field(discriminator="action")]


class MemoryData(Contract):
    summary: MemorySummary | None = Field(description="근거 있는 현재 대화 요약. 유효 요약이 없으면 null.")
    changes: list[MemoryChange] = Field(description="DB 변경 후보. 분석 완료는 저장 완료를 뜻하지 않는다. 새 사실이 없으면 빈 배열.")


class Communication(Contract):
    judgement: Literal["clear", "partially_clear", "unclear", "not_assessable"]
    explanation_ko: Text
    evidence: list[Evidence]

    @model_validator(mode="after")
    def grounded(self):
        if self.judgement != "not_assessable" and not self.evidence:
            raise ValueError("평가 가능한 의사 전달 판단에는 근거가 필요하다")
        return self


class Strength(Contract):
    comment_ko: Text
    evidence: Annotated[list[Evidence], Field(min_length=1)]
    assisted: bool = Field(description="근거 발화에 도움 사용 기록이 있으면 true.")


class FeedbackItem(Contract):
    item_id: Id = Field(description="현재 결과 안의 로컬 식별자. DB 식별자는 BE가 부여한다.")
    kind: Literal["grammar_error", "vocabulary_error", "natural_alternative", "context_fit", "needs_clarification"]
    source: Evidence
    suggested_expression: Text | None = Field(description="의미가 분명한 경우의 영어 교정·대안. 의미 확인이 필요하면 null.")
    explanation_ko: Text
    assisted: bool

    @model_validator(mode="after")
    def consistent(self):
        if self.kind == "needs_clarification" and self.suggested_expression is not None:
            raise ValueError("의미 불명확 발화에 정답 문장을 단정하지 않는다")
        if self.kind != "needs_clarification" and self.suggested_expression is None:
            raise ValueError("교정·대안 항목에는 suggested_expression이 필요하다")
        return self


class NextPractice(Contract):
    source_item_id: Id
    instruction_ko: Text
    model_sentence: Text


class FeedbackData(Contract):
    communication: Communication
    strengths: Annotated[list[Strength], Field(max_length=1)] = Field(description="짧은 피드백의 잘한 점. 근거가 없으면 빈 배열.")
    items: list[FeedbackItem] = Field(description="선택형 상세 평가. 오류가 없으면 빈 배열이며 오류를 억지로 생성하지 않는다.")
    summary_improvement_id: Id | None = Field(description="짧은 피드백에서 보여 줄 items의 항목 1개. 없으면 null.")
    next_practice: NextPractice | None

    @model_validator(mode="after")
    def linked(self):
        ids = [i.item_id for i in self.items]
        if len(set(ids)) != len(ids):
            raise ValueError("피드백 item_id 중복")
        if self.summary_improvement_id is not None and self.summary_improvement_id not in ids:
            raise ValueError("summary_improvement_id가 items에 없음")
        if self.items and self.summary_improvement_id is None:
            raise ValueError("상세 개선 항목이 있으면 짧은 피드백의 대표 항목을 고른다")
        if self.next_practice is not None:
            item = next((i for i in self.items if i.item_id == self.next_practice.source_item_id), None)
            if item is None or item.kind == "needs_clarification":
                raise ValueError("다음 연습은 실제 교정·대안 항목에 연결해야 한다")
        return self


class DataIssue(Contract):
    code: Literal["NO_USABLE_USER_TURNS", "INSUFFICIENT_CONTEXT", "AMBIGUOUS_INPUT"]
    message_ko: Text


class QuotaIssue(Contract):
    code: Literal["AI_QUOTA_EXHAUSTED"]
    message_ko: Text
    retryable_now: Literal[False]


class ModelIssue(Contract):
    code: Literal["AI_TIMEOUT", "AI_PROVIDER_ERROR", "INVALID_MODEL_OUTPUT"]
    message_ko: Text
    retryable_now: bool


class ResponseBase(Contract):
    schema_version: Literal["0.1.0"]
    request_id: Id
    scope: Scope
    conversation_revision: Annotated[int, Field(ge=0)]
    analysis_config_version: Id


class MemoryCompleted(ResponseBase):
    status: Literal["completed"]
    base_memory_version: Annotated[int, Field(ge=0)]
    result: MemoryData
    problem: None


class FeedbackCompleted(ResponseBase):
    status: Literal["completed"]
    result: FeedbackData
    problem: None


class Processing(ResponseBase):
    status: Literal["processing"]
    result: None
    problem: None


class Insufficient(ResponseBase):
    status: Literal["insufficient_evidence"]
    result: None
    problem: DataIssue


class QuotaBlocked(ResponseBase):
    status: Literal["blocked_by_quota"]
    result: None
    problem: QuotaIssue


class Failed(ResponseBase):
    status: Literal["failed"]
    result: None
    problem: ModelIssue


MemoryOutput = Annotated[Union[MemoryCompleted, Processing, Insufficient, QuotaBlocked, Failed], Field(discriminator="status")]
FeedbackOutput = Annotated[Union[FeedbackCompleted, Processing, Insufficient, QuotaBlocked, Failed], Field(discriminator="status")]


class MemoryLlmUsable(Contract):
    outcome: Literal["usable"]
    result: MemoryData
    problem: None


class FeedbackLlmUsable(Contract):
    outcome: Literal["usable"]
    result: FeedbackData
    problem: None


class LlmInsufficient(Contract):
    outcome: Literal["insufficient_evidence"]
    result: None
    problem: DataIssue


MemoryLlmOutput = Annotated[Union[MemoryLlmUsable, LlmInsufficient], Field(discriminator="outcome")]
FeedbackLlmOutput = Annotated[Union[FeedbackLlmUsable, LlmInsufficient], Field(discriminator="outcome")]

ADAPTERS = {
    "memory.input": TypeAdapter(MemoryInput),
    "memory.output": TypeAdapter(MemoryOutput),
    "memory.llm-output": TypeAdapter(MemoryLlmOutput),
    "feedback.input": TypeAdapter(FeedbackInput),
    "feedback.output": TypeAdapter(FeedbackOutput),
    "feedback.llm-output": TypeAdapter(FeedbackLlmOutput),
}


def _evidence(ev, turns, *, user_only):
    t = turns.get(ev.turn_id)
    if t is None or t.text_status != "final" or t.final_text is None:
        raise ValueError("근거가 없거나 확정·유효 발화가 아니다")
    if t.speaker == "system" or (user_only and t.speaker != "user"):
        raise ValueError("사용자 사실·평가에 NPC 또는 시스템 발화를 근거로 사용했다")
    if ev.quote not in t.final_text:
        raise ValueError("인용이 final_text에 정확히 포함되어 있지 않다")
    return t


def validate_pair(kind, request_json, response_json):
    """구조 검증과 현재 요청에 대한 근거·참조 검증. 인증·DB 저장은 수행하지 않는다."""
    if kind not in ("memory", "feedback"):
        raise ValueError("kind는 memory 또는 feedback이다")
    req = ADAPTERS[kind + ".input"].validate_json(request_json)
    resp = ADAPTERS[kind + ".output"].validate_json(response_json)
    if (req.request_id, req.scope, req.conversation_revision, req.analysis_config_version) != (
        resp.request_id,
        resp.scope,
        resp.conversation_revision,
        resp.analysis_config_version,
    ):
        raise ValueError("응답의 요청·사용자·NPC·대화·분석 버전이 입력과 다르다")
    usable_user_turns = any(t.speaker == "user" and t.text_status == "final" for t in req.turns)
    if resp.status == "insufficient_evidence" and resp.problem.code == "NO_USABLE_USER_TURNS" and usable_user_turns:
        raise ValueError("유효 사용자 발화가 있는데 NO_USABLE_USER_TURNS로 응답했다")
    if resp.status != "completed":
        return req, resp
    turns = {t.turn_id: t for t in req.turns}
    if not usable_user_turns:
        raise ValueError("유효 사용자 발화가 없으므로 completed를 반환할 수 없다")
    if kind == "memory":
        if resp.base_memory_version != req.memory_snapshot.version:
            raise ValueError("기억 기준 버전이 요청 스냅샷과 다르다")
        if resp.result.summary is not None:
            for ev in resp.result.summary.evidence:
                _evidence(ev, turns, user_only=False)
        facts = {f.memory_id: f for f in req.memory_snapshot.facts}
        keys = {f.fact_key for f in req.memory_snapshot.facts}
        changed = []
        for ch in resp.result.changes:
            changed.append(ch.fact_key)
            for ev in ch.evidence:
                _evidence(ev, turns, user_only=True)
            if ch.action == "add" and ch.fact_key in keys:
                raise ValueError("기존 fact_key에는 add 대신 replace를 사용한다")
            if ch.action != "add":
                old = facts.get(ch.memory_id)
                if old is None or old.fact_key != ch.fact_key:
                    raise ValueError("변경 대상 memory_id/fact_key가 요청 스냅샷에 없다")
                if ch.action == "replace" and old.kind != ch.kind:
                    raise ValueError("replace는 기존 사실 종류를 유지한다")
        if len(set(changed)) != len(changed):
            raise ValueError("같은 fact_key를 한 결과에서 여러 번 변경할 수 없다")
    else:
        for ev in resp.result.communication.evidence:
            _evidence(ev, turns, user_only=True)
        for strength in resp.result.strengths:
            evidence_turns = [_evidence(ev, turns, user_only=True) for ev in strength.evidence]
            if strength.assisted != any(t.assistance_ids for t in evidence_turns):
                raise ValueError("잘한 점의 도움 사용 표시가 입력과 다르다")
        for item in resp.result.items:
            t = _evidence(item.source, turns, user_only=True)
            if item.assisted != bool(t.assistance_ids):
                raise ValueError("교정 항목의 도움 사용 표시가 입력과 다르다")
    return req, resp
