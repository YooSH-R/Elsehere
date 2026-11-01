package com.elsehere.feedback.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static com.elsehere.conversation.DialogueTestData.npcTurn;
import static com.elsehere.conversation.DialogueTestData.textTurn;
import static com.elsehere.conversation.DialogueTestData.usage;
import static com.elsehere.conversation.DialogueTestData.voiceTurn;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.groups.Tuple.tuple;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;

import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.client.FeedbackInput;
import com.elsehere.ai.client.WorkerFeedback;
import com.elsehere.ai.entity.AiRequest;
import com.elsehere.ai.entity.AiRequestKind;
import com.elsehere.ai.entity.AiRequestStatus;
import com.elsehere.ai.repository.AiRequestRepository;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.service.DialogueServiceTest;
import com.elsehere.feedback.dto.ConversationFeedbackResponse;
import com.elsehere.feedback.dto.DetailedFeedbackItemResponse;
import com.elsehere.feedback.dto.FeedbackResponse;
import com.elsehere.feedback.dto.IdiomResponse;
import com.elsehere.feedback.dto.SituationExpressionResponse;
import com.elsehere.feedback.dto.SuggestionResponse;
import com.elsehere.feedback.dto.VocabCandidateResponse;
import com.elsehere.feedback.entity.FeedbackStatus;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.beans.factory.annotation.Autowired;

// 피드백 생성은 AI 워커 결과를 Mock으로 정하고, 저장 결과는 종료 응답·다시 보기(FB-01)·상세 평가(FB-03)로 확인한다.
@DialogueServiceTest
class FeedbackServiceTest {

  @Autowired private FeedbackService feedbackService;

  @Autowired private AiWorkerClient aiWorkerClient;

  @Autowired private AiRequestRepository aiRequestRepository;

  @Autowired private EntityManager entityManager;

  private Long userId;
  private Conversation conversation;
  private Turn voiceTurn;
  private Turn textTurn;

  // 카페 직원과 음성 한 번, 텍스트 한 번 말하고 사용자가 끝낸 대화
  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    conversation = conversation(entityManager, userId, "CAFE_STAFF");
    npcTurn(entityManager, conversation, 1, "Hi! What can I get for you?");
    voiceTurn = voiceTurn(entityManager, conversation, 2, "I want latte", "i want latte");
    npcTurn(entityManager, conversation, 3, "Sure. Hot or iced?");
    textTurn = textTurn(entityManager, conversation, 4, "Iced please");
    npcTurn(entityManager, conversation, 5, "Here you go.");
    conversation.endByUser(Instant.parse("2026-10-27T01:05:00Z"));
    entityManager.flush();
  }

  @Test
  @DisplayName("워커가 피드백을 만들면 DONE으로 저장하고 요약·잘한 점·아쉬운 점·개선 표현을 돌려주며 호출 기록을 남긴다")
  void feedbackOnEnd_completed_savesDone() {
    given(aiWorkerClient.feedback(any(), any())).willReturn(completed());
    UUID requestId = UUID.randomUUID();

    FeedbackResponse response =
        feedbackService.feedbackOnEnd(userId, conversation.getId(), requestId, false);

    assertThat(response.status()).isEqualTo(FeedbackStatus.DONE);
    assertThat(response.summary()).isEqualTo("라테를 주문했어요.");
    assertThat(response.situationExpressions())
        .extracting(SituationExpressionResponse::expression, SituationExpressionResponse::usageKo)
        .containsExactly(tuple("Can I get that to go?", "음료를 가지고 나가고 싶을 때 써요."));
    assertThat(response.vocabCandidates())
        .containsExactly(
            new VocabCandidateResponse("latte", "라떼", "I'd like an iced latte.", "아이스 라떼 주세요."));
    assertThat(response.reviews().good()).containsExactly("원하는 것을 분명히 말했어요");
    assertThat(response.reviews().improve()).containsExactly("공손한 표현을 더 써 보세요");
    assertThat(response.suggestion())
        .isEqualTo(new SuggestionResponse("I want latte", "I'd like a latte, please."));
    assertThat(response.errorCode()).isNull();
    AiRequest record =
        aiRequestRepository.findByRequestIdAndKind(requestId, AiRequestKind.FEEDBACK).orElseThrow();
    assertThat(record.getStatus()).isEqualTo(AiRequestStatus.OK);
    assertThat(record.getModel()).isEqualTo("test-model");
  }

  @Test
  @DisplayName("종료 때 만든 발화별 평가를 상세 평가(FB-03)와 다시 보기(FB-01)가 AI 재호출 없이 돌려준다")
  void detailed_afterCompleted_returnsSavedTurnReviews() {
    given(aiWorkerClient.feedback(any(), any())).willReturn(completed());
    feedbackService.feedbackOnEnd(userId, conversation.getId(), UUID.randomUUID(), false);

    List<DetailedFeedbackItemResponse> detailed =
        feedbackService.detailed(userId, conversation.getId()).items();
    ConversationFeedbackResponse review = feedbackService.feedback(userId, conversation.getId());

    assertThat(detailed).hasSize(2);
    DetailedFeedbackItemResponse first = detailed.get(0);
    assertThat(first.turnId()).isEqualTo(voiceTurn.getId());
    assertThat(first.userText()).isEqualTo("I want latte");
    assertThat(first.betterText()).isEqualTo("I'd like a latte, please.");
    assertThat(first.reason()).isEqualTo("관사와 please를 붙이면 자연스러워요.");
    assertThat(first.idiom()).isEqualTo(new IdiomResponse("I'd like ~", "~로 주세요"));
    assertThat(first.hasModelSpeech()).isFalse();
    DetailedFeedbackItemResponse second = detailed.get(1);
    assertThat(second.turnId()).isEqualTo(textTurn.getId());
    assertThat(second.betterText()).isNull();
    assertThat(second.reason()).isEqualTo("자연스러운 말이에요.");
    assertThat(second.idiom()).isNull();

    assertThat(review.npc().npcCode()).isEqualTo("CAFE_STAFF");
    assertThat(review.sceneCode()).isEqualTo("CAFE");
    assertThat(review.userTurnCount()).isEqualTo(2);
    assertThat(review.feedback().status()).isEqualTo(FeedbackStatus.DONE);
    assertThat(review.detailedFeedback()).isEqualTo(detailed);
    then(aiWorkerClient).should().feedback(any(), any());
  }

  @Test
  @DisplayName("워커에는 사용자 말을 원문 그대로, 음성은 전사 원문과 함께, NPC 대사는 generated로 보낸다")
  void feedbackOnEnd_sendsConversationAsContractInput() {
    given(aiWorkerClient.feedback(any(), any())).willReturn(completed());
    UUID requestId = UUID.randomUUID();

    feedbackService.feedbackOnEnd(userId, conversation.getId(), requestId, false);

    ArgumentCaptor<FeedbackInput> captor = ArgumentCaptor.forClass(FeedbackInput.class);
    then(aiWorkerClient).should().feedback(any(), captor.capture());
    FeedbackInput input = captor.getValue();
    assertThat(input.schemaVersion()).isEqualTo("0.3.0");
    assertThat(input.requestId()).isEqualTo(requestId.toString());
    assertThat(input.scope())
        .isEqualTo(
            new FeedbackInput.Scope(
                String.valueOf(userId), "CAFE_STAFF", String.valueOf(conversation.getId())));
    assertThat(input.conversationRevision()).isEqualTo(5);
    assertThat(input.context().sceneId()).isEqualTo("CAFE");
    assertThat(input.context().mode()).isEqualTo("free_chat");
    assertThat(input.context().endReason()).isEqualTo("user_end");
    assertThat(input.turns())
        .extracting(
            FeedbackInput.Turn::speaker,
            FeedbackInput.Turn::inputType,
            FeedbackInput.Turn::textStatus,
            FeedbackInput.Turn::originalTranscript,
            FeedbackInput.Turn::finalText)
        .containsExactly(
            tuple("npc", "generated", "final", null, "Hi! What can I get for you?"),
            tuple("user", "voice", "final", "i want latte", "I want latte"),
            tuple("npc", "generated", "final", null, "Sure. Hot or iced?"),
            tuple("user", "text", "final", null, "Iced please"),
            tuple("npc", "generated", "final", null, "Here you go."));
    assertThat(input.turns().get(1).turnId()).isEqualTo(String.valueOf(voiceTurn.getId()));
  }

  @Test
  @DisplayName("사용자가 피드백을 건너뛰면 AI를 부르지 않고 SKIPPED로 저장한다")
  void feedbackOnEnd_skip_savesSkippedWithoutAi() {
    FeedbackResponse response =
        feedbackService.feedbackOnEnd(userId, conversation.getId(), UUID.randomUUID(), true);

    assertThat(response.status()).isEqualTo(FeedbackStatus.SKIPPED);
    assertThat(response.errorCode()).isNull();
    assertThat(response.summary()).isNull();
    then(aiWorkerClient).should(never()).feedback(any(), any());
  }

  @Test
  @DisplayName("사용자 발화가 없는 대화는 AI를 부르지 않고 NO_USABLE_USER_TURNS로 건너뛴다")
  void feedbackOnEnd_withoutUserTurns_skips() {
    Conversation silent = conversation(entityManager, userId, "HOTEL_STAFF");
    npcTurn(entityManager, silent, 1, "Welcome to the hotel.");
    silent.endByUser(Instant.parse("2026-10-27T01:06:00Z"));

    FeedbackResponse response =
        feedbackService.feedbackOnEnd(userId, silent.getId(), UUID.randomUUID(), false);

    assertThat(response.status()).isEqualTo(FeedbackStatus.SKIPPED);
    assertThat(response.errorCode()).isEqualTo("NO_USABLE_USER_TURNS");
    then(aiWorkerClient).should(never()).feedback(any(), any());
  }

  @Test
  @DisplayName("워커가 평가할 근거가 부족하다고 하면 워커가 준 이유로 SKIPPED를 저장한다")
  void feedbackOnEnd_insufficientEvidence_skipsWithProblemCode() {
    given(aiWorkerClient.feedback(any(), any()))
        .willReturn(
            new WorkerFeedback(
                "insufficient_evidence", "NO_USABLE_USER_TURNS", null, null, List.of()));

    FeedbackResponse response =
        feedbackService.feedbackOnEnd(userId, conversation.getId(), UUID.randomUUID(), false);

    assertThat(response.status()).isEqualTo(FeedbackStatus.SKIPPED);
    assertThat(response.errorCode()).isEqualTo("NO_USABLE_USER_TURNS");
  }

  @Test
  @DisplayName("워커가 생성에 실패했다고 하면 FAILED와 원인 코드를 저장하고 호출 기록을 FAILED로 남긴다")
  void feedbackOnEnd_workerFailed_savesFailed() {
    given(aiWorkerClient.feedback(any(), any()))
        .willReturn(new WorkerFeedback("failed", "AI_TIMEOUT", null, null, List.of(usage("fb"))));
    UUID requestId = UUID.randomUUID();

    FeedbackResponse response =
        feedbackService.feedbackOnEnd(userId, conversation.getId(), requestId, false);

    assertThat(response.status()).isEqualTo(FeedbackStatus.FAILED);
    assertThat(response.errorCode()).isEqualTo("AI_TIMEOUT");
    assertThat(
            aiRequestRepository
                .findByRequestIdAndKind(requestId, AiRequestKind.FEEDBACK)
                .orElseThrow()
                .getStatus())
        .isEqualTo(AiRequestStatus.FAILED);
  }

  @Test
  @DisplayName("워커에 닿지 못하면 대화 종료는 그대로 두고 FAILED와 API 오류 코드를 저장한다")
  void feedbackOnEnd_workerUnavailable_savesFailed() {
    given(aiWorkerClient.feedback(any(), any()))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));

    FeedbackResponse response =
        feedbackService.feedbackOnEnd(userId, conversation.getId(), UUID.randomUUID(), false);

    assertThat(response.status()).isEqualTo(FeedbackStatus.FAILED);
    assertThat(response.errorCode()).isEqualTo("AI_TEMPORARILY_UNAVAILABLE");
  }

  @Test
  @DisplayName("실패한 피드백은 다시 만들기(FB-02)로 다시 만들어 DONE과 상세 평가를 저장한다")
  void regenerate_afterFailure_savesDone() {
    given(aiWorkerClient.feedback(any(), any()))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE))
        .willReturn(completed());
    feedbackService.feedbackOnEnd(userId, conversation.getId(), UUID.randomUUID(), false);

    FeedbackResponse response =
        feedbackService.regenerate(userId, conversation.getId(), UUID.randomUUID());

    assertThat(response.status()).isEqualTo(FeedbackStatus.DONE);
    assertThat(response.errorCode()).isNull();
    assertThat(feedbackService.detailed(userId, conversation.getId()).items()).hasSize(2);
  }

  @Test
  @DisplayName("이미 만든 피드백은 종료를 다시 요청해도 다시 만들기를 요청해도 AI를 다시 부르지 않고 저장된 결과를 돌려준다")
  void feedbackOnEndAndRegenerate_afterDone_returnSaved() {
    given(aiWorkerClient.feedback(any(), any())).willReturn(completed());
    FeedbackResponse first =
        feedbackService.feedbackOnEnd(userId, conversation.getId(), UUID.randomUUID(), false);

    FeedbackResponse again =
        feedbackService.feedbackOnEnd(userId, conversation.getId(), UUID.randomUUID(), true);
    FeedbackResponse regenerated =
        feedbackService.regenerate(userId, conversation.getId(), UUID.randomUUID());

    assertThat(again).isEqualTo(first);
    assertThat(regenerated).isEqualTo(first);
    then(aiWorkerClient).should().feedback(any(), any());
  }

  @Test
  @DisplayName("피드백을 만든 적 없는 대화를 다시 만들라고 하면 NOT_FOUND다")
  void regenerate_withoutFeedback_throwsNotFound() {
    assertThatThrownBy(
            () -> feedbackService.regenerate(userId, conversation.getId(), UUID.randomUUID()))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  @Test
  @DisplayName("다른 사용자의 대화는 다시 보기·상세 평가·다시 만들기 모두 NOT_FOUND다")
  void otherUsersConversation_throwsNotFound() {
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Other");
    Long conversationId = conversation.getId();

    assertThatThrownBy(() -> feedbackService.feedback(otherUserId, conversationId))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    assertThatThrownBy(() -> feedbackService.detailed(otherUserId, conversationId))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    assertThatThrownBy(
            () -> feedbackService.regenerate(otherUserId, conversationId, UUID.randomUUID()))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
  }

  // 음성 턴에는 고칠 표현과 회화 설명이 있고, 텍스트 턴은 적절해서 고칠 표현이 없다.
  private WorkerFeedback completed() {
    String voiceTurnId = String.valueOf(voiceTurn.getId());
    WorkerFeedback.Result result =
        new WorkerFeedback.Result(
            new WorkerFeedback.Summary("라테를 주문했어요."),
            List.of(
                new WorkerFeedback.ReviewTag("strength", "원하는 것을 분명히 말했어요"),
                new WorkerFeedback.ReviewTag("improvement", "공손한 표현을 더 써 보세요")),
            List.of(
                new WorkerFeedback.TurnReview(
                    voiceTurnId,
                    "I want latte",
                    "improvement_suggested",
                    "관사와 please를 붙이면 자연스러워요.",
                    List.of(
                        new WorkerFeedback.Suggestion(
                            "s1", "natural_alternative", "I'd like a latte, please.", "더 공손해요."))),
                new WorkerFeedback.TurnReview(
                    String.valueOf(textTurn.getId()),
                    "Iced please",
                    "appropriate",
                    "자연스러운 말이에요.",
                    List.of())),
            "s1",
            List.of(
                new WorkerFeedback.SpokenUsageNote(
                    new WorkerFeedback.Source(voiceTurnId, "I want latte"),
                    "I'd like ~",
                    "~로 주세요")),
            List.of(
                new WorkerFeedback.SituationExpression(
                    "Can I get that to go?",
                    "포장해 주실 수 있나요?",
                    "음료를 가지고 나가고 싶을 때 써요.",
                    "Can I get that to go? I'm in a hurry.",
                    "포장해 주실 수 있나요? 좀 급해서요.")),
            List.of(
                new WorkerFeedback.VocabCandidate(
                    "word", "latte", "라떼", "I'd like an iced latte.", "아이스 라떼 주세요."),
                new WorkerFeedback.VocabCandidate(
                    "idiom", "to go", "포장해서", "Can I get it to go?", "포장해 주실 수 있나요?")));
    return new WorkerFeedback(
        "completed", null, result, "{\"conversation_summary\":{}}", List.of(usage("feedback")));
  }
}
