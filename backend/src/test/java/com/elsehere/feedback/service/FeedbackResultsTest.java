package com.elsehere.feedback.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.ai.client.WorkerFeedback;
import com.elsehere.ai.client.WorkerUsage;
import com.elsehere.feedback.entity.FeedbackReviews;
import com.elsehere.feedback.entity.TurnFeedback;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

// 정상 결과의 변환은 FeedbackServiceTest에서 저장 결과로 확인하고, 여기서는 칸이 비거나 어긋난 결과를 본다.
class FeedbackResultsTest {

  @Test
  @DisplayName("발화 평가에 설명이 없으면 첫 제안의 설명을 이유로 쓴다")
  void turnFeedbacks_withoutExplanation_usesSuggestionExplanation() {
    WorkerFeedback.Result result =
        result(
            List.of(
                new WorkerFeedback.TurnReview(
                    "7",
                    "I want latte",
                    "improvement_suggested",
                    null,
                    List.of(
                        new WorkerFeedback.Suggestion(
                            "s1", "natural_alternative", "I'd like a latte.", "더 공손해요.")))),
            null);

    List<TurnFeedback> feedbacks = FeedbackResults.turnFeedbacks(result);

    assertThat(feedbacks).hasSize(1);
    assertThat(feedbacks.get(0).getTurnId()).isEqualTo(7L);
    assertThat(feedbacks.get(0).getBetterText()).isEqualTo("I'd like a latte.");
    assertThat(feedbacks.get(0).getReason()).isEqualTo("더 공손해요.");
  }

  @Test
  @DisplayName("설명도 제안도 없으면 이유는 빈 문자열이고 고칠 표현은 없다")
  void turnFeedbacks_withoutExplanationAndSuggestion_leavesEmptyReason() {
    WorkerFeedback.Result result =
        result(List.of(new WorkerFeedback.TurnReview("7", "Hi", "appropriate", null, null)), null);

    TurnFeedback feedback = FeedbackResults.turnFeedbacks(result).get(0);

    assertThat(feedback.getBetterText()).isNull();
    assertThat(feedback.getReason()).isEmpty();
  }

  @Test
  @DisplayName("우리가 보내지 않은 형식의 턴 ID(숫자가 아님)는 건너뛴다")
  void turnFeedbacks_nonNumericTurnId_isSkipped() {
    WorkerFeedback.Result result =
        result(
            List.of(
                new WorkerFeedback.TurnReview("turn_a", "Hi", "appropriate", "좋아요.", List.of()),
                new WorkerFeedback.TurnReview("8", "Hello", "appropriate", "좋아요.", List.of())),
            null);

    assertThat(FeedbackResults.turnFeedbacks(result))
        .extracting(TurnFeedback::getTurnId)
        .containsExactly(8L);
  }

  @Test
  @DisplayName("개선 표현 ID가 어느 제안과도 맞지 않으면 개선 표현 없이 잘한 점·아쉬운 점만 담는다")
  void reviews_unknownSummarySuggestion_hasNoSuggestion() {
    WorkerFeedback.Result result =
        new WorkerFeedback.Result(
            null,
            List.of(new WorkerFeedback.ReviewTag("strength", "잘 말했어요")),
            List.of(),
            "missing",
            null,
            null,
            null);

    FeedbackReviews reviews = FeedbackResults.reviews(result);

    assertThat(reviews.good()).containsExactly("잘 말했어요");
    assertThat(reviews.improve()).isEmpty();
    assertThat(reviews.suggestion()).isNull();
    assertThat(FeedbackResults.summary(result)).isNull();
  }

  @Test
  @DisplayName("결과의 목록 칸이 비어(null) 있어도 빈 평가로 바꾼다")
  void nullLists_becomeEmpty() {
    WorkerFeedback.Result result =
        new WorkerFeedback.Result(null, null, null, null, null, null, null);

    assertThat(FeedbackResults.turnFeedbacks(result)).isEmpty();
    assertThat(FeedbackResults.reviews(result))
        .isEqualTo(new FeedbackReviews(List.of(), List.of(), null, List.of(), List.of()));
  }

  @Test
  @DisplayName("단어장은 단어·숙어·문장을 모두 저장하므로 단어장 후보를 종류와 상관없이 뜻·예문과 함께 옮긴다")
  void reviews_keepsWordIdiomAndSentenceVocabCandidates() {
    WorkerFeedback.Result result =
        new WorkerFeedback.Result(
            null,
            List.of(),
            List.of(),
            null,
            List.of(),
            List.of(),
            List.of(
                new WorkerFeedback.VocabCandidate(
                    "word", "latte", "라떼", "I'd like an iced latte.", "아이스 라떼 주세요."),
                new WorkerFeedback.VocabCandidate(
                    "word", "iced latte", "아이스 라떼", "One iced latte, please.", "아이스 라떼 하나 주세요."),
                new WorkerFeedback.VocabCandidate(
                    "idiom", "to go", "포장해서", "Can I get it to go?", "포장해 주실 수 있나요?"),
                new WorkerFeedback.VocabCandidate(
                    "sentence", "I'd like a latte.", "라떼 주세요.", "I'd like a latte.", "라떼 주세요.")));

    assertThat(FeedbackResults.reviews(result).vocabCandidates())
        .extracting(FeedbackReviews.VocabCandidate::expression)
        .containsExactly("latte", "iced latte", "to go", "I'd like a latte.");
  }

  @Test
  @DisplayName("이 장면에서 자주 쓰는 표현을 뜻·쓰는 때·예문과 함께 화면 형식으로 옮긴다")
  void reviews_carriesSituationExpressions() {
    WorkerFeedback.Result result =
        new WorkerFeedback.Result(
            null,
            List.of(),
            List.of(),
            null,
            List.of(),
            List.of(
                new WorkerFeedback.SituationExpression(
                    "Could I leave my bags here?",
                    "짐을 여기 맡겨도 될까요?",
                    "체크인 전에 짐을 맡기고 싶을 때 써요.",
                    "Could I leave my bags here until check-in?",
                    "체크인 전까지 짐을 여기 맡겨도 될까요?")),
            List.of());

    assertThat(FeedbackResults.reviews(result).situationExpressions())
        .containsExactly(
            new FeedbackReviews.SituationExpression(
                "Could I leave my bags here?",
                "짐을 여기 맡겨도 될까요?",
                "체크인 전에 짐을 맡기고 싶을 때 써요.",
                "Could I leave my bags here until check-in?",
                "체크인 전까지 짐을 여기 맡겨도 될까요?"));
  }

  @Test
  @DisplayName("호출 기록의 모델은 사용량 중 모델 이름이 있는 첫 항목이다")
  void firstModel_skipsUsageWithoutModel() {
    List<WorkerUsage> usage =
        List.of(
            new WorkerUsage("guard", null, 0, 0, null, 0.1, false),
            new WorkerUsage("feedback", "gpt-test", 10, 5, null, 0.2, false));

    assertThat(FeedbackResults.firstModel(usage)).isEqualTo("gpt-test");
    assertThat(FeedbackResults.firstModel(List.of())).isNull();
  }

  private static WorkerFeedback.Result result(
      List<WorkerFeedback.TurnReview> turnReviews, String summarySuggestionId) {
    return new WorkerFeedback.Result(
        null, List.of(), turnReviews, summarySuggestionId, List.of(), List.of(), List.of());
  }
}
