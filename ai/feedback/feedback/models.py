"""v0.3: 대화 요약·리뷰 태그·전체 발화 평가·단어장 후보. 의미 품질 판정은 별도."""

import re
from typing import Annotated, Literal, Union

from pydantic import Field, TypeAdapter, model_validator

from . import contract_v01 as core

VERSION = "0.3.0"


def contains_korean(text):
    return bool(re.search(r"[가-힣ㄱ-ㅎㅏ-ㅣ]", text))


class Context(core.ConversationContext):
    mode: Literal["tutorial", "free_chat", "ambient", "quest"]
    tutorial_goal: core.Text | None

    @model_validator(mode="after")
    def goal_matches_mode(self):
        if (self.mode == "tutorial") != (self.tutorial_goal is not None):
            raise ValueError("tutorial_goal은 tutorial에서만 내용이 필요하고 나머지는 null이다")
        return self


class Difficulty(core.DifficultySnapshot):
    hint_policy: Literal["on_request", "tutorial_guided"]


class Turn(core.Turn):
    difficulty: Difficulty


class FeedbackInput(core.BaseInput):
    schema_version: Literal["0.3.0"]
    context: Context
    turns: list[Turn]


class ConversationSummary(core.Contract):
    text_ko: core.Text
    evidence: Annotated[list[core.Evidence], Field(min_length=1)] = Field(
        description="이번 대화의 확정 사용자/NPC 발화. NPC 인용은 문맥 요약에만 허용."
    )


class ReviewTag(core.Contract):
    tag_id: core.Id
    kind: Literal["strength", "improvement"]
    label_ko: core.Text
    evidence: Annotated[list[core.Evidence], Field(min_length=1)]
    assisted: bool


class Suggestion(core.Contract):
    suggestion_id: core.Id = Field(description="결과 내부 ID. DB 식별자가 아니다.")
    kind: Literal["grammar_error", "vocabulary_error", "natural_alternative", "context_fit", "idiomatic_expression"]
    source: core.Evidence
    suggested_expression: core.Text
    explanation_ko: core.Text


class ExpressionExample(core.Contract):
    condition_ko: core.Text
    expression: core.Text
    meaning_ko: core.Text


class TurnReview(core.Contract):
    review_id: core.Id
    turn_id: core.Id
    original_text: core.Text = Field(description="해당 사용자 final_text 전체와 정확히 일치.")
    verdict: Literal["appropriate", "improvement_suggested", "needs_clarification", "not_assessable"]
    explanation_ko: core.Text
    assisted: bool
    suggestions: list[Suggestion]
    expression_examples: Annotated[list[ExpressionExample], Field(max_length=3)] = Field(default_factory=list)

    @model_validator(mode="after")
    def consistent(self):
        if self.expression_examples:
            if self.verdict == "not_assessable":
                raise ValueError("평가할 언어가 없는 발화에 예문을 만들어 붙이지 않는다")
            expressions = [item.expression.strip().rstrip(".!?").casefold() for item in self.expression_examples]
            if len(expressions) < 2 or len(set(expressions)) != len(expressions):
                raise ValueError("표현 예시는 서로 다른 2~3개 문장이어야 한다")
            if any(contains_korean(item.expression) or not re.search(r"[A-Za-z]", item.expression) for item in self.expression_examples):
                raise ValueError("표현 예시는 영어 문장과 별도의 한글 뜻으로 제공한다")
        if self.verdict in ("needs_clarification", "not_assessable") and self.suggestions:
            raise ValueError("의미 확인 필요/평가 불가 발화에 정답 표현을 단정하지 않는다")
        if self.verdict == "improvement_suggested" and not self.suggestions:
            raise ValueError("개선 제안 판정에는 개선 표현이 필요하다")
        if self.verdict == "appropriate" and any(
            item.kind not in ("natural_alternative", "idiomatic_expression") for item in self.suggestions
        ):
            raise ValueError("적절한 발화를 문법·어휘·상황 오류로 동시에 분류하지 않는다")
        return self


class UserTurnSource(core.Contract):
    type: Literal["user_turn"]
    turn_id: core.Id
    quote: core.Text


class SuggestionSource(core.Contract):
    type: Literal["suggestion"]
    review_id: core.Id
    suggestion_id: core.Id
    quote: core.Text = Field(description="연결된 suggested_expression에 포함된 실제 표현.")


VocabSource = Annotated[Union[UserTurnSource, SuggestionSource], Field(discriminator="type")]


class VocabCandidate(core.Contract):
    candidate_id: core.Id
    kind: Literal["word", "idiom", "sentence"]
    expression: core.Text
    meaning_ko: core.Text
    example_en: core.Text = Field(description="학습용 예문. 실제 사용자 발화라고 표시하지 않는다.")
    example_ko: core.Text
    source: VocabSource


class SituationExpression(core.Contract):
    """이번 장면에서 자주 쓰는 관용·추천 표현. 사용자 발화 평가와 별개로, 다음에 같은 상황에서 써 볼 표현을 알려 준다."""

    expression: core.Text
    meaning_ko: core.Text
    usage_ko: core.Text = Field(description="이 장면에서 언제 쓰는지 한 문장")
    example_en: core.Text = Field(description="학습용 새 예문. 실제 사용자 발화라고 표시하지 않는다.")
    example_ko: core.Text


class FeedbackData(core.Contract):
    conversation_summary: ConversationSummary
    communication: core.Communication
    review_tags: Annotated[list[ReviewTag], Field(max_length=3)] = Field(
        description="이번 UI 계약의 제안 상한 3개. 근거 없으면 빈 배열. 공식 평가 척도가 아니다."
    )
    turn_reviews: Annotated[list[TurnReview], Field(min_length=1)]
    summary_suggestion_id: core.Id | None
    vocab_candidates: list[VocabCandidate]
    # 기존 결과와 호환되도록 없으면 빈 배열이다. 생성할 때는 gms.feedback_response_format이 2~3개를 요구한다.
    situation_expressions: Annotated[list[SituationExpression], Field(max_length=3)] = Field(default_factory=list)

    @model_validator(mode="after")
    def references(self):
        reviews = {review.review_id: review for review in self.turn_reviews}
        if len(reviews) != len(self.turn_reviews):
            raise ValueError("review_id 중복")
        if len({review.turn_id for review in self.turn_reviews}) != len(self.turn_reviews):
            raise ValueError("같은 발화를 두 번 평가했다")
        suggestions = [item for review in self.turn_reviews for item in review.suggestions]
        if len({item.suggestion_id for item in suggestions}) != len(suggestions):
            raise ValueError("suggestion_id는 결과 전체에서 유일해야 한다")
        ids = {item.suggestion_id for item in suggestions}
        if self.summary_suggestion_id is not None and self.summary_suggestion_id not in ids:
            raise ValueError("대표 개선 표현이 실제 제안을 가리키지 않는다")
        if suggestions and self.summary_suggestion_id is None:
            raise ValueError("개선 표현이 있으면 대표 하나를 지정한다")
        if len({tag.tag_id for tag in self.review_tags}) != len(self.review_tags):
            raise ValueError("tag_id 중복")
        if len({item.candidate_id for item in self.vocab_candidates}) != len(self.vocab_candidates):
            raise ValueError("candidate_id 중복")
        expressions = [" ".join(item.expression.split()).casefold() for item in self.vocab_candidates]
        if len(set(expressions)) != len(expressions):
            raise ValueError("같은 표현의 단어장 후보 중복")
        situation = [" ".join(item.expression.split()).casefold() for item in self.situation_expressions]
        if len(set(situation)) != len(situation):
            raise ValueError("같은 상황 표현 중복")
        for item in self.vocab_candidates:
            if item.expression != item.source.quote:
                raise ValueError("저장할 표현은 출처 quote와 정확히 일치해야 한다")
            if item.source.type == "suggestion":
                review = reviews.get(item.source.review_id)
                suggestion = (
                    None if review is None else next((s for s in review.suggestions if s.suggestion_id == item.source.suggestion_id), None)
                )
                if suggestion is None or item.source.quote not in suggestion.suggested_expression:
                    raise ValueError("단어장 후보의 개선 표현 출처가 없거나 일치하지 않는다")
        return self


class SpokenUsageSelection(core.Contract):
    catalog_id: core.Id
    turn_id: core.Id
    quote: core.Text


class SpokenUsageNote(core.Contract):
    catalog_id: core.Id
    source: core.Evidence
    expression: core.Text
    meaning_ko: core.Text
    usage_ko: core.Text
    example_en: core.Text
    example_ko: core.Text
    example_kind: Literal["learning_example"]
    sources: Annotated[list[core.Text], Field(min_length=1)]


class EnrichedFeedbackData(FeedbackData):
    # 서버가 검토된 카탈로그에서 덧붙인다. 모델은 설명문/출처를 생성하지 않는다.
    spoken_usage_notes: Annotated[list[SpokenUsageNote], Field(max_length=1)] = Field(default_factory=list)


class Completed(core.FeedbackCompleted):
    schema_version: Literal["0.3.0"]
    result: EnrichedFeedbackData


class Processing(core.Processing):
    schema_version: Literal["0.3.0"]


class Insufficient(core.Insufficient):
    schema_version: Literal["0.3.0"]


class QuotaBlocked(core.QuotaBlocked):
    schema_version: Literal["0.3.0"]


class Failed(core.Failed):
    schema_version: Literal["0.3.0"]


class LlmUsable(core.Contract):
    outcome: Literal["usable"]
    result: FeedbackData
    problem: None
    spoken_usage_selection: SpokenUsageSelection | None = None


class LlmInsufficient(core.LlmInsufficient):
    spoken_usage_selection: None = None


FeedbackOutput = Annotated[Union[Completed, Processing, Insufficient, QuotaBlocked, Failed], Field(discriminator="status")]
FeedbackLlmOutput = Annotated[Union[LlmUsable, LlmInsufficient], Field(discriminator="outcome")]
INPUT = TypeAdapter(FeedbackInput)
OUTPUT = TypeAdapter(FeedbackOutput)
LLM_OUTPUT = TypeAdapter(FeedbackLlmOutput)


def usable_turns(request):
    return [turn for turn in request.turns if turn.speaker == "user" and turn.text_status == "final"]


def surface_only_change(original, suggested):
    """대소문자·끝 문장부호만 다른 교정인가. 음성 원문 표기는 STT가 정하므로 이런 교정은 회화 교정이 아니다."""

    def plain(text):
        return text.strip().rstrip(".!?").casefold()

    return plain(original) == plain(suggested)


def evidence_turn(evidence, turns, *, user_only=True):
    turn = turns.get(evidence.turn_id)
    if turn is None or turn.text_status != "final" or turn.speaker not in ("user", "npc"):
        raise ValueError("근거에는 이번 대화의 확정 사용자/NPC 발화가 필요하다")
    if user_only and turn.speaker != "user":
        raise ValueError("사용자 평가 근거에 NPC 발화를 사용할 수 없다")
    if evidence.quote not in turn.final_text:
        raise ValueError("인용이 final_text에 없다")
    return turn


def validate_pair(request, response):
    """구조·원문·전체 발화 범위·태그·표현 출처 검사. 의미 품질 판정은 별도."""
    request = INPUT.validate_python(request)
    response = OUTPUT.validate_python(response)
    for key in ("schema_version", "request_id", "scope", "conversation_revision", "analysis_config_version"):
        if getattr(request, key) != getattr(response, key):
            raise ValueError(f"요청과 응답의 {key} 불일치")
    users = usable_turns(request)
    if response.status == "insufficient_evidence" and response.problem.code == "NO_USABLE_USER_TURNS" and users:
        raise ValueError("유효 발화가 있는데 NO_USABLE_USER_TURNS로 응답했다")
    if response.status != "completed":
        return request, response
    if not users:
        raise ValueError("유효 사용자 발화 없이 completed를 반환할 수 없다")
    turns = {turn.turn_id: turn for turn in request.turns}
    result = response.result
    if [review.turn_id for review in result.turn_reviews] != [turn.turn_id for turn in users]:
        raise ValueError("확정 사용자 발화를 원래 순서대로 빠짐없이 한 번씩 평가해야 한다")
    for evidence in result.conversation_summary.evidence:
        evidence_turn(evidence, turns, user_only=False)
    for evidence in result.communication.evidence:
        evidence_turn(evidence, turns)
    for tag in result.review_tags:
        linked = [evidence_turn(e, turns) for e in tag.evidence]
        if tag.assisted != any(turn.assistance_ids for turn in linked):
            raise ValueError("태그의 도움 사용 표시 불일치")
    for review in result.turn_reviews:
        turn = turns[review.turn_id]
        if review.original_text != turn.final_text:
            raise ValueError("평가 원문이 해당 final_text 전체와 다르다")
        if review.assisted != bool(turn.assistance_ids):
            raise ValueError("발화 평가의 도움 사용 표시 불일치")
        if contains_korean(turn.final_text) and review.verdict in ("appropriate", "improvement_suggested"):
            if not any(
                item.kind == "context_fit"
                and re.search(r"[A-Za-z]", item.suggested_expression)
                and not contains_korean(item.suggested_expression)
                for item in review.suggestions
            ):
                raise ValueError("의도가 분명한 한국어 발화에는 같은 의미의 영어 표현이 필요하다")
        for item in review.suggestions:
            if item.source.turn_id != review.turn_id:
                raise ValueError("개선 표현의 근거는 해당 평가 발화여야 한다")
            evidence_turn(item.source, turns)
            if turn.input_type == "voice" and surface_only_change(turn.final_text, item.suggested_expression):
                raise ValueError("음성 발화의 대소문자·종결 부호만 바꾼 교정")
    reviews_by_turn = {review.turn_id: review for review in result.turn_reviews}
    for note in result.spoken_usage_notes:
        turn = evidence_turn(note.source, turns)
        if reviews_by_turn[turn.turn_id].verdict in ("needs_clarification", "not_assessable"):
            raise ValueError("불확실한 발화에 회화 용법을 확정하지 않는다")
    for item in result.vocab_candidates:
        if item.source.type == "user_turn":
            turn = evidence_turn(item.source, turns)
            if reviews_by_turn[turn.turn_id].verdict in ("needs_clarification", "not_assessable"):
                raise ValueError("의미가 불확실한 원문에서 단어장 정답을 확정하지 않는다")
    return request, response
