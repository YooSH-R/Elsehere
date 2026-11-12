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
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;

import ch.qos.logback.classic.Level;
import com.elsehere.ai.client.FeedbackInput;
import com.elsehere.ai.client.WorkerFeedback;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.service.AiGateway;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.logging.LogCapture;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.dialogue.service.DialogueServiceTest;
import com.elsehere.feedback.dto.ConversationFeedbackResponse;
import com.elsehere.feedback.dto.DetailedFeedbackItemResponse;
import com.elsehere.feedback.dto.FeedbackResponse;
import com.elsehere.feedback.dto.IdiomResponse;
import com.elsehere.feedback.dto.ReviewsResponse;
import com.elsehere.feedback.dto.SituationExpressionResponse;
import com.elsehere.feedback.dto.SuggestionResponse;
import com.elsehere.feedback.dto.VocabCandidateResponse;
import com.elsehere.feedback.entity.ConversationFeedback;
import com.elsehere.feedback.entity.FeedbackStatus;
import com.elsehere.feedback.entity.TurnFeedback;
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
import tools.jackson.databind.json.JsonMapper;

// 피드백 생성은 AI 워커 결과를 Mock으로 정하고, 저장 결과는 종료 응답·다시 보기(FB-01)·상세 평가(FB-03)로 확인한다.
@DialogueServiceTest
class FeedbackServiceTest {

  // 이미 저장된 피드백의 conversation_feedback.reviews 형식(completed()의 결과). 바꾸면 저장된 피드백을 읽지 못한다.
  private static final String STORED_REVIEWS =
      """
      {"good": ["원하는 것을 분명히 말했어요"],
       "improve": ["공손한 표현을 더 써 보세요"],
       "suggestion": {"userText": "I want latte", "betterText": "I'd like a latte, please."},
       "situationExpressions": [{"expression": "Can I get that to go?",
                                 "meaningKo": "포장해 주실 수 있나요?",
                                 "usageKo": "음료를 가지고 나가고 싶을 때 써요.",
                                 "exampleEn": "Can I get that to go? I'm in a hurry.",
                                 "exampleKo": "포장해 주실 수 있나요? 좀 급해서요."}],
       "vocabCandidates": [{"expression": "latte",
                            "meaningKo": "라떼",
                            "exampleEn": "I'd like an iced latte.",
                            "exampleKo": "아이스 라떼 주세요."},
                           {"expression": "to go",
                            "meaningKo": "포장해서",
                            "exampleEn": "Can I get it to go?",
                            "exampleKo": "포장해 주실 수 있나요?"}]}
      """;

  @Autowired private FeedbackService feedbackService;

  @Autowired private AiGateway aiGateway;

  @Autowired private EntityManager entityManager;

  @Autowired private JsonMapper jsonMapper;

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
  @DisplayName("저장한 평가 원본의 판정과 조건별 예문을 재조회하며 AI를 다시 호출하지 않는다")
  void detailed_preservesAssessmentAndConditionalExpressionExamples() {
    Conversation hotel = conversation(entityManager, userId, "HOTEL_STAFF");
    Turn request = textTurn(entityManager, hotel, 1, "umbrella please");
    hotel.endByUser(Instant.parse("2026-10-27T02:00:00Z"));
    ConversationFeedback stored = ConversationFeedback.of(hotel.getId());
    stored.complete(
        "우산 관련 요청",
        STORED_REVIEWS,
        "test",
        """
        {"turn_reviews":[{"turn_id":"%s","verdict":"needs_clarification",
          "suggestions":[],"expression_examples":[
          {"condition_ko":"빌릴 때","expression":"Could I borrow an umbrella?","meaning_ko":"우산을 빌릴 수 있나요?"},
          {"condition_ko":"판매처를 물을 때","expression":"Where can I buy an umbrella?","meaning_ko":"우산을 어디서 살 수 있나요?"}]}]}
        """
            .formatted(request.getId()));
    entityManager.persist(stored);
    entityManager.persist(TurnFeedback.of(request.getId(), null, "요청 의도를 확인해요.", null, null));
    entityManager.flush();
    entityManager.clear();

    String detail = jsonMapper.writeValueAsString(feedbackService.detailed(userId, hotel.getId()));
    String full = jsonMapper.writeValueAsString(feedbackService.feedback(userId, hotel.getId()));

    for (String response : List.of(detail, full)) {
      assertThat(response)
          .contains("\"verdict\":\"needs_clarification\"")
          .contains("\"expressionExamples\"")
          .contains("Could I borrow an umbrella?")
          .contains("Where can I buy an umbrella?");
    }
    then(aiGateway).should(never()).feedback(any(), any());
  }

  @Test
  @DisplayName("완성된 피드백은 미확인 개수에 쌓이고 해당 피드백을 읽으면 한 번만 차감한다")
  void unreadCount_accumulatesCompletedFeedbackAndReadingIsIdempotent() {
    given(aiGateway.feedback(any(), any())).willReturn(completed());
    feedbackService.feedbackOnEnd(userId, conversation.getId(), UUID.randomUUID(), false);
    entityManager.flush();
    assertThat(feedbackService.unreadCount(userId)).isEqualTo(1);

    feedbackService.markRead(userId, conversation.getId());
    entityManager.flush();
    entityManager.clear();
    assertThat(feedbackService.unreadCount(userId)).isZero();
    feedbackService.markRead(userId, conversation.getId());
    assertThat(feedbackService.unreadCount(userId)).isZero();
  }

  @Test
  @DisplayName("피드백 조회만으로는 미확인 수가 줄지 않고 완료된 내 피드백만 누적된다")
  void unreadCount_onlyCountsMyCompletedFeedbackAndReadingTheListDoesNotClearIt() {
    given(aiGateway.feedback(any(), any())).willReturn(completed());
    feedbackService.feedbackOnEnd(userId, conversation.getId(), UUID.randomUUID(), false);
    feedbackService.feedback(userId, conversation.getId());
    Conversation second = conversation(entityManager, userId, "CAFE_STAFF");
    ConversationFeedback done = ConversationFeedback.of(second.getId());
    done.complete("완료", STORED_REVIEWS, "test", null);
    entityManager.persist(done);
    Conversation skipped = conversation(entityManager, userId, "CAFE_STAFF");
    ConversationFeedback skippedFeedback = ConversationFeedback.of(skipped.getId());
    skippedFeedback.skip(null);
    entityManager.persist(skippedFeedback);
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");
    Conversation other = conversation(entityManager, otherUserId, "HOTEL_STAFF");
    ConversationFeedback otherFeedback = ConversationFeedback.of(other.getId());
    otherFeedback.complete("완료", STORED_REVIEWS, "test", null);
    entityManager.persist(otherFeedback);
    entityManager.flush();

    assertThat(feedbackService.unreadCount(userId)).isEqualTo(2);
    assertThat(feedbackService.unreadCount(otherUserId)).isEqualTo(1);
    assertThatThrownBy(() -> feedbackService.markRead(userId, other.getId()))
        .isInstanceOf(BusinessException.class)
        .extracting("errorCode")
        .isEqualTo(ErrorCode.NOT_FOUND);
    assertThat(feedbackService.unreadCount(otherUserId)).isEqualTo(1);
  }

  @Test
  @DisplayName(
      "완료된 피드백은 reviews 칸에 저장 형식(good·improve·suggestion·situationExpressions·vocabCandidates)으로 남긴다")
  void feedbackOnEnd_completed_savesReviewsInStoredFormat() {
    given(aiGateway.feedback(any(), any())).willReturn(completed());

    feedbackService.feedbackOnEnd(userId, conversation.getId(), UUID.randomUUID(), false);
    entityManager.flush();
    entityManager.clear();

    String reviews =
        entityManager.find(ConversationFeedback.class, conversation.getId()).getReviews();
    assertThat(jsonMapper.readTree(reviews)).isEqualTo(jsonMapper.readTree(STORED_REVIEWS));
  }

  @Test
  @DisplayName("이미 저장된 형식의 피드백을 읽어 다시 보기(FB-01) 응답을 만든다")
  void feedback_readsStoredReviews() {
    ConversationFeedback stored = ConversationFeedback.of(conversation.getId());
    stored.complete("라테를 주문했어요.", STORED_REVIEWS, "test-model", null);
    entityManager.persist(stored);
    entityManager.flush();
    entityManager.clear();

    FeedbackResponse response = feedbackService.feedback(userId, conversation.getId()).feedback();

    assertThat(response.reviews())
        .isEqualTo(new ReviewsResponse(List.of("원하는 것을 분명히 말했어요"), List.of("공손한 표현을 더 써 보세요")));
    assertThat(response.suggestion())
        .isEqualTo(new SuggestionResponse("I want latte", "I'd like a latte, please."));
    assertThat(response.situationExpressions())
        .containsExactly(
            new SituationExpressionResponse(
                "Can I get that to go?",
                "포장해 주실 수 있나요?",
                "음료를 가지고 나가고 싶을 때 써요.",
                "Can I get that to go? I'm in a hurry.",
                "포장해 주실 수 있나요? 좀 급해서요."));
    // 단어장은 단어·숙어·문장을 모두 저장하므로(F-33·VOCAB-02) 숙어 후보도 남긴다.
    assertThat(response.vocabCandidates())
        .containsExactly(
            new VocabCandidateResponse("latte", "라떼", "I'd like an iced latte.", "아이스 라떼 주세요."),
            new VocabCandidateResponse("to go", "포장해서", "Can I get it to go?", "포장해 주실 수 있나요?"));
  }

  @Test
  @DisplayName("워커가 피드백을 만들면 DONE으로 저장하고 요약·잘한 점·아쉬운 점·개선 표현을 돌려준다. 워커 요청에는 요청 ID와 대화를 붙인다")
  void feedbackOnEnd_completed_savesDone() {
    given(aiGateway.feedback(any(), any())).willReturn(completed());
    UUID requestId = UUID.randomUUID();

    FeedbackResponse response =
        feedbackService.feedbackOnEnd(userId, conversation.getId(), requestId, false);

    assertThat(response.status()).isEqualTo(FeedbackStatus.DONE);
    assertThat(response.summary()).isEqualTo("라테를 주문했어요.");
    assertThat(response.situationExpressions())
        .extracting(SituationExpressionResponse::expression, SituationExpressionResponse::usageKo)
        .containsExactly(tuple("Can I get that to go?", "음료를 가지고 나가고 싶을 때 써요."));
    // 단어장은 단어·숙어·문장을 모두 저장하므로(F-33·VOCAB-02) 숙어 후보도 남긴다.
    assertThat(response.vocabCandidates())
        .containsExactly(
            new VocabCandidateResponse("latte", "라떼", "I'd like an iced latte.", "아이스 라떼 주세요."),
            new VocabCandidateResponse("to go", "포장해서", "Can I get it to go?", "포장해 주실 수 있나요?"));
    assertThat(response.reviews().good()).containsExactly("원하는 것을 분명히 말했어요");
    assertThat(response.reviews().improve()).containsExactly("공손한 표현을 더 써 보세요");
    assertThat(response.suggestion())
        .isEqualTo(new SuggestionResponse("I want latte", "I'd like a latte, please."));
    assertThat(response.errorCode()).isNull();
    then(aiGateway)
        .should()
        .feedback(eq(new WorkerTrace(requestId, userId, conversation.getId())), any());
  }

  @Test
  @DisplayName("종료 때 만든 발화별 평가를 상세 평가(FB-03)와 다시 보기(FB-01)가 AI 재호출 없이 돌려준다")
  void detailed_afterCompleted_returnsSavedTurnReviews() {
    given(aiGateway.feedback(any(), any())).willReturn(completed());
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
    then(aiGateway).should().feedback(any(), any());
  }

  @Test
  @DisplayName("워커에는 사용자 말을 원문 그대로, 음성은 전사 원문과 함께, NPC 대사는 generated로 보낸다")
  void feedbackOnEnd_sendsConversationAsContractInput() {
    given(aiGateway.feedback(any(), any())).willReturn(completed());
    UUID requestId = UUID.randomUUID();

    feedbackService.feedbackOnEnd(userId, conversation.getId(), requestId, false);

    ArgumentCaptor<FeedbackInput> captor = ArgumentCaptor.forClass(FeedbackInput.class);
    then(aiGateway).should().feedback(any(), captor.capture());
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
    then(aiGateway).should(never()).feedback(any(), any());
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
    then(aiGateway).should(never()).feedback(any(), any());
  }

  @Test
  @DisplayName("워커가 평가할 근거가 부족하다고 하면 워커가 준 이유로 SKIPPED를 저장한다")
  void feedbackOnEnd_insufficientEvidence_skipsWithProblemCode() {
    given(aiGateway.feedback(any(), any()))
        .willReturn(
            new WorkerFeedback(
                "insufficient_evidence", "NO_USABLE_USER_TURNS", null, null, List.of()));

    FeedbackResponse response =
        feedbackService.feedbackOnEnd(userId, conversation.getId(), UUID.randomUUID(), false);

    assertThat(response.status()).isEqualTo(FeedbackStatus.SKIPPED);
    assertThat(response.errorCode()).isEqualTo("NO_USABLE_USER_TURNS");
  }

  @Test
  @DisplayName("워커가 생성에 실패했다고 하면 FAILED와 원인 코드를 저장한다")
  void feedbackOnEnd_workerFailed_savesFailed() {
    given(aiGateway.feedback(any(), any()))
        .willReturn(new WorkerFeedback("failed", "AI_TIMEOUT", null, null, List.of(usage("fb"))));
    UUID requestId = UUID.randomUUID();

    FeedbackResponse response =
        feedbackService.feedbackOnEnd(userId, conversation.getId(), requestId, false);

    assertThat(response.status()).isEqualTo(FeedbackStatus.FAILED);
    assertThat(response.errorCode()).isEqualTo("AI_TIMEOUT");
  }

  @Test
  @DisplayName("워커에 닿지 못하면 대화 종료는 그대로 두고 FAILED와 API 오류 코드를 저장한다")
  void feedbackOnEnd_workerUnavailable_savesFailed() {
    given(aiGateway.feedback(any(), any()))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));

    FeedbackResponse response =
        feedbackService.feedbackOnEnd(userId, conversation.getId(), UUID.randomUUID(), false);

    assertThat(response.status()).isEqualTo(FeedbackStatus.FAILED);
    assertThat(response.errorCode()).isEqualTo("AI_TEMPORARILY_UNAVAILABLE");
  }

  @Test
  @DisplayName("피드백을 만들지 못하면 대화 ID와 원인 코드를 WARN으로 남긴다")
  void feedbackOnEnd_workerFailed_logsFeedbackFailed() {
    given(aiGateway.feedback(any(), any()))
        .willReturn(new WorkerFeedback("failed", "AI_TIMEOUT", null, null, List.of(usage("fb"))));

    try (LogCapture logs = LogCapture.atInfo(FeedbackService.class)) {
      feedbackService.feedbackOnEnd(userId, conversation.getId(), UUID.randomUUID(), false);

      assertThat(logs.messages(Level.WARN))
          .containsExactly(
              "feedback_failed conversation_id=" + conversation.getId() + " reason=AI_TIMEOUT");
    }
  }

  @Test
  @DisplayName("피드백을 건너뛰면 대화 ID와 이유를 INFO로 남기고, 사용자가 건너뛰었으면 이유를 USER_SKIPPED로 쓴다")
  void feedbackOnEnd_skipped_logsFeedbackSkipped() {
    Conversation silent = conversation(entityManager, userId, "HOTEL_STAFF");
    npcTurn(entityManager, silent, 1, "Welcome to the hotel.");
    silent.endByUser(Instant.parse("2026-10-27T01:06:00Z"));

    try (LogCapture logs = LogCapture.atInfo(FeedbackService.class)) {
      feedbackService.feedbackOnEnd(userId, conversation.getId(), UUID.randomUUID(), true);
      feedbackService.feedbackOnEnd(userId, silent.getId(), UUID.randomUUID(), false);

      assertThat(logs.messages(Level.INFO))
          .containsExactly(
              "feedback_skipped conversation_id=" + conversation.getId() + " reason=USER_SKIPPED",
              "feedback_skipped conversation_id="
                  + silent.getId()
                  + " reason=NO_USABLE_USER_TURNS");
    }
  }

  @Test
  @DisplayName("실패한 피드백은 다시 만들기(FB-02)로 다시 만들어 DONE과 상세 평가를 저장한다")
  void regenerate_afterFailure_savesDone() {
    given(aiGateway.feedback(any(), any()))
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
    given(aiGateway.feedback(any(), any())).willReturn(completed());
    FeedbackResponse first =
        feedbackService.feedbackOnEnd(userId, conversation.getId(), UUID.randomUUID(), false);

    FeedbackResponse again =
        feedbackService.feedbackOnEnd(userId, conversation.getId(), UUID.randomUUID(), true);
    FeedbackResponse regenerated =
        feedbackService.regenerate(userId, conversation.getId(), UUID.randomUUID());

    assertThat(again).isEqualTo(first);
    assertThat(regenerated).isEqualTo(first);
    then(aiGateway).should().feedback(any(), any());
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
