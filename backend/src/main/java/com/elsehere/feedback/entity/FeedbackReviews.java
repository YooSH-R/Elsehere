package com.elsehere.feedback.entity;

import java.util.List;

/**
 * conversation_feedback.reviews에 저장하는 JSON. 이미 저장된 피드백을 읽어야 하므로 칸 이름을 바꾸거나 지우지 않는다.
 *
 * <p>API 응답 DTO와 따로 둔다. 응답 모양을 바꿔도 저장 형식은 그대로이고, 응답을 만들 때 응답 DTO로 옮겨 담는다.
 *
 * @param situationExpressions 이 장면에서 자주 쓰는 표현. 이 필드를 넣기 전에 저장한 피드백은 null
 * @param vocabCandidates 단어장에 저장할 수 있는 단어. 이 필드를 넣기 전에 저장한 피드백은 null
 */
public record FeedbackReviews(
    List<String> good,
    List<String> improve,
    Suggestion suggestion,
    List<SituationExpression> situationExpressions,
    List<VocabCandidate> vocabCandidates) {

  /** 사용자가 한 말과 더 나은 표현. */
  public record Suggestion(String userText, String betterText) {}

  /** 이 장면에서 자주 쓰는 관용·추천 표현과 학습 예문. */
  public record SituationExpression(
      String expression, String meaningKo, String usageKo, String exampleEn, String exampleKo) {}

  /** 단어장에 저장할 수 있는 단어와 학습 예문. */
  public record VocabCandidate(
      String expression, String meaningKo, String exampleEn, String exampleKo) {}
}
