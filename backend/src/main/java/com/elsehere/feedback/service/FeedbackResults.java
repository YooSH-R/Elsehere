package com.elsehere.feedback.service;

import com.elsehere.ai.client.WorkerFeedback;
import com.elsehere.ai.client.WorkerUsage;
import com.elsehere.feedback.entity.FeedbackReviews;
import com.elsehere.feedback.entity.TurnFeedback;
import java.util.ArrayList;
import java.util.List;
import java.util.Objects;
import java.util.Optional;

/**
 * 피드백 결과를 화면 형식으로 바꾼다. 대응은 AI 명세 2.7절을 따른다: 요약 ← conversation_summary.text_ko, 잘한 점·아쉬운 점 ←
 * review_tags의 label_ko, 개선 표현 ← summary_suggestion_id가 가리키는 제안, 이 장면에서 자주 쓰는 표현 ←
 * situation_expressions.
 */
final class FeedbackResults {

  private FeedbackResults() {}

  static String summary(WorkerFeedback.Result result) {
    return result.conversationSummary() == null ? null : result.conversationSummary().textKo();
  }

  static FeedbackReviews reviews(WorkerFeedback.Result result) {
    return new FeedbackReviews(
        tagLabels(result, "strength"),
        tagLabels(result, "improvement"),
        suggestion(result),
        situationExpressions(result),
        vocabCandidates(result));
  }

  /**
   * 단어장에 저장할 수 있는 후보. 단어장은 단어·숙어·문장을 모두 저장하므로(VOCAB-02) 종류와 상관없이 그대로 둔다.
   */
  private static List<FeedbackReviews.VocabCandidate> vocabCandidates(
      WorkerFeedback.Result result) {
    return nonNull(result.vocabCandidates()).stream()
        .map(
            item ->
                new FeedbackReviews.VocabCandidate(
                    item.expression(), item.meaningKo(), item.exampleEn(), item.exampleKo()))
        .toList();
  }

  /** 이 장면에서 자주 쓰는 표현. 사용자 발화 평가와 별개로 화면의 회화 칸에 보여 준다. */
  private static List<FeedbackReviews.SituationExpression> situationExpressions(
      WorkerFeedback.Result result) {
    return nonNull(result.situationExpressions()).stream()
        .map(
            item ->
                new FeedbackReviews.SituationExpression(
                    item.expression(),
                    item.meaningKo(),
                    item.usageKo(),
                    item.exampleEn(),
                    item.exampleKo()))
        .toList();
  }

  /**
   * 사용자 발화마다 상세 평가 한 행. 고칠 표현은 첫 제안, 회화 표현은 그 발화에 붙은 회화 설명이다.
   *
   * <p>결과의 turn_id는 우리가 보낸 DB 턴 ID 문자열이다. 숫자가 아니면 건너뛴다.
   */
  static List<TurnFeedback> turnFeedbacks(WorkerFeedback.Result result) {
    List<TurnFeedback> feedbacks = new ArrayList<>();
    for (WorkerFeedback.TurnReview review : nonNull(result.turnReviews())) {
      Optional<Long> turnId = parseTurnId(review.turnId());
      if (turnId.isEmpty()) {
        continue;
      }
      Optional<WorkerFeedback.Suggestion> first =
          nonNull(review.suggestions()).stream().findFirst();
      String reason =
          review.explanationKo() != null
              ? review.explanationKo()
              : first.map(WorkerFeedback.Suggestion::explanationKo).orElse("");
      Optional<WorkerFeedback.SpokenUsageNote> note =
          nonNull(result.spokenUsageNotes()).stream()
              .filter(n -> n.source() != null && review.turnId().equals(n.source().turnId()))
              .findFirst();
      feedbacks.add(
          TurnFeedback.of(
              turnId.get(),
              first.map(WorkerFeedback.Suggestion::suggestedExpression).orElse(null),
              reason,
              note.map(WorkerFeedback.SpokenUsageNote::expression).orElse(null),
              note.map(WorkerFeedback.SpokenUsageNote::meaningKo).orElse(null)));
    }
    return feedbacks;
  }

  static String firstModel(List<WorkerUsage> usage) {
    return usage.stream().map(WorkerUsage::model).filter(Objects::nonNull).findFirst().orElse(null);
  }

  private static List<String> tagLabels(WorkerFeedback.Result result, String kind) {
    return nonNull(result.reviewTags()).stream()
        .filter(tag -> kind.equals(tag.kind()))
        .map(WorkerFeedback.ReviewTag::labelKo)
        .toList();
  }

  private static FeedbackReviews.Suggestion suggestion(WorkerFeedback.Result result) {
    String suggestionId = result.summarySuggestionId();
    if (suggestionId == null) {
      return null;
    }
    for (WorkerFeedback.TurnReview review : nonNull(result.turnReviews())) {
      for (WorkerFeedback.Suggestion suggestion : nonNull(review.suggestions())) {
        if (suggestionId.equals(suggestion.suggestionId())) {
          return new FeedbackReviews.Suggestion(
              review.originalText(), suggestion.suggestedExpression());
        }
      }
    }
    return null;
  }

  private static Optional<Long> parseTurnId(String turnId) {
    try {
      return Optional.of(Long.parseLong(turnId));
    } catch (NumberFormatException exception) {
      return Optional.empty();
    }
  }

  private static <T> List<T> nonNull(List<T> list) {
    return list == null ? List.of() : list;
  }
}
