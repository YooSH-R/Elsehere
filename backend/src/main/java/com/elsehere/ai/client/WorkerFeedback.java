package com.elsehere.ai.client;

import java.util.List;

/**
 * 대화 종료 피드백(/v1/feedback) 결과 중 백엔드가 쓰는 칸. 전체 형식은 ai/feedback/schemas/feedback.output.schema.json이다.
 *
 * @param status completed, insufficient_evidence, failed
 * @param problemCode insufficient_evidence·failed일 때 이유(NO_USABLE_USER_TURNS, AI_TIMEOUT 등)
 * @param result completed일 때만 있다
 * @param resultJson 검증된 결과 원본. 평가 근거를 잃지 않도록 그대로 보관한다
 */
public record WorkerFeedback(
    String status, String problemCode, Result result, String resultJson, List<WorkerUsage> usage) {

  public boolean completed() {
    return "completed".equals(status) && result != null;
  }

  public boolean insufficientEvidence() {
    return "insufficient_evidence".equals(status);
  }

  /**
   * @param summarySuggestionId 짧은 피드백의 개선 표현 상자에 보여 줄 제안. 없으면 null
   * @param spokenUsageNotes 교정 아래의 회화 설명(0~1개)
   * @param situationExpressions 이 장면에서 자주 쓰는 관용·추천 표현(0~3개). 이 필드가 없던 워커 결과는 null
   */
  public record Result(
      Summary conversationSummary,
      List<ReviewTag> reviewTags,
      List<TurnReview> turnReviews,
      String summarySuggestionId,
      List<SpokenUsageNote> spokenUsageNotes,
      List<SituationExpression> situationExpressions) {}

  public record Summary(String textKo) {}

  /**
   * @param kind strength 또는 improvement
   */
  public record ReviewTag(String kind, String labelKo) {}

  /**
   * @param verdict appropriate, improvement_suggested, needs_clarification, not_assessable
   */
  public record TurnReview(
      String turnId,
      String originalText,
      String verdict,
      String explanationKo,
      List<Suggestion> suggestions) {}

  public record Suggestion(
      String suggestionId, String kind, String suggestedExpression, String explanationKo) {}

  public record SpokenUsageNote(Source source, String expression, String meaningKo) {}

  public record Source(String turnId, String quote) {}

  /**
   * @param usageKo 이 장면에서 언제 쓰는지
   * @param exampleEn 학습용 새 예문. 사용자가 한 말이 아니다
   */
  public record SituationExpression(
      String expression, String meaningKo, String usageKo, String exampleEn, String exampleKo) {}
}
