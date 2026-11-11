package com.elsehere.dialogue.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static com.elsehere.conversation.DialogueTestData.npcTurn;
import static com.elsehere.conversation.DialogueTestData.textTurn;
import static com.elsehere.conversation.DialogueTestData.usage;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.groups.Tuple.tuple;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.anyList;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.argThat;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.times;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.HintResult;
import com.elsehere.ai.client.MeaningResult;
import com.elsehere.ai.client.SuggestResult;
import com.elsehere.ai.client.WorkerLine;
import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.GlossaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.dialogue.dto.HintResponse;
import com.elsehere.dialogue.dto.MeaningResponse;
import com.elsehere.dialogue.dto.SuggestWordsResponse;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import org.assertj.core.api.ThrowableAssert.ThrowingCallable;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.beans.factory.annotation.Autowired;

// NPC 대사 뜻 보기(CONV-04), 힌트(CONV-05), 추천 단어(CONV-12). 워커는 Mock이다.
@DialogueServiceTest
class TurnHelpServiceTest {

  private static final String NPC_LINE = "Great! For here or to go?";
  private static final MeaningResult MEANING =
      new MeaningResult(
          "좋아요! 매장에서 드실 건가요, 가져가실 건가요?",
          List.of(new MeaningResult.Glossary("to go", "포장")),
          List.of(usage("translate")));
  private static final HintResult HINT =
      new HintResult(
          List.of(
              new HintResult.Phrase("For here, please.", "여기서 먹을게요."),
              new HintResult.Phrase("I'll have it here.", "여기서 마실게요.")),
          List.of(new HintResult.Phrase("for here", "매장에서")),
          List.of(usage("hint")));
  private static final SuggestResult SUGGEST =
      new SuggestResult(List.of("for here", "to go"), List.of(usage("suggest")));

  @Autowired private TurnHelpService turnHelpService;

  @Autowired private AiGateway aiGateway;

  @Autowired private EntityManager entityManager;

  private Long userId;
  private Conversation conversation;
  private Turn userTurn;
  private Turn npcTurn;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    conversation = conversation(entityManager, userId, "CAFE_STAFF");
    npcTurn(entityManager, conversation, 1, "Hi! What can I get for you?");
    userTurn = textTurn(entityManager, conversation, 2, "A latte, please.");
    npcTurn = npcTurn(entityManager, conversation, 3, NPC_LINE);
    entityManager.flush();
  }

  @Test
  @DisplayName("처음 뜻 보기를 하면 대화 난이도(초급 1)로 워커를 불러 뜻과 단어 풀이를 돌려주고, 워커 요청에 요청 ID와 대화를 붙인다")
  void meaning_firstRequest_returnsMeaning() {
    given(aiGateway.meaning(any(), any(), eq(NPC_LINE), eq(1))).willReturn(MEANING);
    UUID requestId = UUID.randomUUID();

    MeaningResponse response = turnHelpService.meaning(userId, npcTurn.getId(), requestId);

    assertThat(response.turnId()).isEqualTo(npcTurn.getId());
    assertThat(response.meaningKo()).isEqualTo("좋아요! 매장에서 드실 건가요, 가져가실 건가요?");
    assertThat(response.glossary())
        .extracting(GlossaryResponse::text, GlossaryResponse::meaningKo)
        .containsExactly(tuple("to go", "포장"));
    then(aiGateway)
        .should()
        .meaning(
            eq(new WorkerTrace(requestId, userId, conversation.getId())),
            any(),
            eq(NPC_LINE),
            eq(1));
  }

  @Test
  @DisplayName("한 번 만든 뜻은 저장해 두고 다시 요청하면 워커를 부르지 않는다")
  void meaning_savedTurn_returnsSavedMeaningWithoutAi() {
    given(aiGateway.meaning(any(), any(), anyString(), anyInt())).willReturn(MEANING);
    MeaningResponse first = turnHelpService.meaning(userId, npcTurn.getId(), UUID.randomUUID());
    entityManager.flush();
    entityManager.clear();

    MeaningResponse again = turnHelpService.meaning(userId, npcTurn.getId(), UUID.randomUUID());

    assertThat(again).isEqualTo(first);
    then(aiGateway).should(times(1)).meaning(any(), any(), anyString(), anyInt());
  }

  @Test
  @DisplayName("근무 중인 NPC와의 대화에서 힌트를 만들면 워커에 시드의 응대 상황을 보낸다")
  void hint_onDutyConversation_sendsServiceScene() {
    given(aiGateway.hint(any(), any(), anyList(), anyString(), anyInt())).willReturn(HINT);

    turnHelpService.hint(userId, npcTurn.getId(), UUID.randomUUID());

    ArgumentCaptor<WorkerNpc> npc = ArgumentCaptor.forClass(WorkerNpc.class);
    then(aiGateway).should().hint(any(), npc.capture(), anyList(), anyString(), anyInt());
    assertThat(npc.getValue().scene())
        .startsWith("The counter of the corner cafe.")
        .contains("Current game time: day 1, 08:00");
  }

  @Test
  @DisplayName("근무가 아닌 NPC와의 대화에서 힌트를 만들면 워커에 응대 상황 대신 일상 상황을 보낸다")
  void hint_offDutyConversation_sendsOffDutyScene() {
    conversation.markNpcOffDuty();
    entityManager.flush();
    given(aiGateway.hint(any(), any(), anyList(), anyString(), anyInt())).willReturn(HINT);

    turnHelpService.hint(userId, npcTurn.getId(), UUID.randomUUID());

    then(aiGateway)
        .should()
        .hint(
            any(),
            argThat(npc -> npc.scene().contains("Mia is off work right now")),
            anyList(),
            anyString(),
            anyInt());
  }

  @Test
  @DisplayName("근무가 아닌 NPC와의 대화에서 추천 단어를 만들면 워커에 응대 상황 대신 일상 상황을 보낸다")
  void suggestWords_offDutyConversation_sendsOffDutyScene() {
    conversation.markNpcOffDuty();
    entityManager.flush();
    given(aiGateway.suggest(any(), any(), anyList(), anyString(), anyInt())).willReturn(SUGGEST);

    turnHelpService.suggestWords(userId, npcTurn.getId(), UUID.randomUUID());

    then(aiGateway)
        .should()
        .suggest(
            any(),
            argThat(npc -> npc.scene().contains("Mia is off work right now")),
            anyList(),
            anyString(),
            anyInt());
  }

  @Test
  @DisplayName("힌트를 부르다 워커에 닿지 못하면 오류를 그대로 알린다")
  void hint_workerCallFails_throws() {
    given(aiGateway.hint(any(), any(), anyList(), anyString(), anyInt()))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));
    UUID requestId = UUID.randomUUID();

    assertErrorCode(
        () -> turnHelpService.hint(userId, npcTurn.getId(), requestId),
        ErrorCode.AI_TEMPORARILY_UNAVAILABLE);
  }

  @Test
  @DisplayName("추천 단어를 부르다 워커에 닿지 못하면 오류를 그대로 알린다")
  void suggestWords_workerCallFails_throws() {
    given(aiGateway.suggest(any(), any(), anyList(), anyString(), anyInt()))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));
    UUID requestId = UUID.randomUUID();

    assertErrorCode(
        () -> turnHelpService.suggestWords(userId, npcTurn.getId(), requestId),
        ErrorCode.AI_TEMPORARILY_UNAVAILABLE);
  }

  @Test
  @DisplayName("워커가 실패하면 오류를 그대로 알리고 뜻을 저장하지 않아 다음 요청에서 다시 만든다")
  void meaning_workerFails_savesNothing() {
    given(aiGateway.meaning(any(), any(), anyString(), anyInt()))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE))
        .willReturn(MEANING);

    assertErrorCode(
        () -> turnHelpService.meaning(userId, npcTurn.getId(), UUID.randomUUID()),
        ErrorCode.AI_TEMPORARILY_UNAVAILABLE);
    MeaningResponse retried = turnHelpService.meaning(userId, npcTurn.getId(), UUID.randomUUID());

    assertThat(retried.meaningKo()).isEqualTo(MEANING.meaningKo());
    then(aiGateway).should(times(2)).meaning(any(), any(), anyString(), anyInt());
  }

  @Test
  @DisplayName("사용자 턴, 다른 사용자의 턴, 없는 턴의 뜻 보기는 워커를 부르지 않고 NOT_FOUND다")
  void meaning_unavailableTurns_throwNotFound() {
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Other");

    assertErrorCode(
        () -> turnHelpService.meaning(userId, userTurn.getId(), UUID.randomUUID()),
        ErrorCode.NOT_FOUND);
    assertErrorCode(
        () -> turnHelpService.meaning(otherUserId, npcTurn.getId(), UUID.randomUUID()),
        ErrorCode.NOT_FOUND);
    assertErrorCode(
        () -> turnHelpService.meaning(userId, Long.MAX_VALUE, UUID.randomUUID()),
        ErrorCode.NOT_FOUND);
    then(aiGateway).should(never()).meaning(any(), any(), anyString(), anyInt());
  }

  @Test
  @DisplayName("처음 힌트를 열면 그 NPC 대사까지의 대화로 워커를 불러 첫 추천 문장과 연 시각을 저장한다. 워커 요청에는 요청 ID와 대화를 붙인다")
  @SuppressWarnings("unchecked")
  void hint_firstRequest_usesHistoryUntilTurnAndSaves() {
    textTurn(entityManager, conversation, 4, "For here.");
    entityManager.flush();
    given(aiGateway.hint(any(), any(), anyList(), eq(""), eq(1))).willReturn(HINT);
    UUID requestId = UUID.randomUUID();

    HintResponse response = turnHelpService.hint(userId, npcTurn.getId(), requestId);

    assertThat(response)
        .isEqualTo(new HintResponse(npcTurn.getId(), "For here, please.", "여기서 먹을게요."));
    ArgumentCaptor<List<WorkerLine>> history = ArgumentCaptor.forClass(List.class);
    then(aiGateway)
        .should()
        .hint(
            eq(new WorkerTrace(requestId, userId, conversation.getId())),
            any(),
            history.capture(),
            eq(""),
            eq(1));
    assertThat(history.getValue())
        .extracting(WorkerLine::who, WorkerLine::text)
        .containsExactly(
            tuple("npc", "Hi! What can I get for you?"),
            tuple("me", "A latte, please."),
            tuple("npc", NPC_LINE));
    assertThat(savedNpcTurn().getHintOpenedAt()).isNotNull();
  }

  @Test
  @DisplayName("한 번 만든 힌트는 다시 요청해도 워커를 부르지 않고, 처음 연 시각을 바꾸지 않는다")
  void hint_savedTurn_returnsSavedHintWithoutAi() {
    given(aiGateway.hint(any(), any(), anyList(), anyString(), anyInt())).willReturn(HINT);
    turnHelpService.hint(userId, npcTurn.getId(), UUID.randomUUID());
    Instant firstOpenedAt = savedNpcTurn().getHintOpenedAt();

    HintResponse again = turnHelpService.hint(userId, npcTurn.getId(), UUID.randomUUID());

    assertThat(again.hintText()).isEqualTo("For here, please.");
    assertThat(savedNpcTurn().getHintOpenedAt()).isEqualTo(firstOpenedAt);
    then(aiGateway).should(times(1)).hint(any(), any(), anyList(), anyString(), anyInt());
  }

  @Test
  @DisplayName("힌트의 영어와 한국어 뜻을 함께 저장하고 다시 열 때 모델을 추가 호출하지 않는다")
  void hint_preservesKoreanMeaningAcrossRepeatedReads() {
    given(aiGateway.hint(any(), any(), anyList(), anyString(), anyInt())).willReturn(HINT);
    HintResponse first = turnHelpService.hint(userId, npcTurn.getId(), UUID.randomUUID());
    Long turnId = npcTurn.getId();
    entityManager.flush();
    entityManager.clear();
    HintResponse cached = turnHelpService.hint(userId, turnId, UUID.randomUUID());
    assertThat(first.hintMeaningKo()).isEqualTo("여기서 먹을게요.");
    assertThat(cached.hintText()).isEqualTo(first.hintText());
    assertThat(cached.hintMeaningKo()).isEqualTo(first.hintMeaningKo());
    then(aiGateway).should(times(1)).hint(any(), any(), anyList(), anyString(), anyInt());
  }

  @Test
  @DisplayName("쓸 수 없는 힌트(AI_RESPONSE_INVALID)면 오류를 그대로 알리고 힌트를 저장하지 않는다")
  void hint_withoutSentence_throwsInvalidAndSavesNothing() {
    given(aiGateway.hint(any(), any(), anyList(), anyString(), anyInt()))
        .willThrow(new BusinessException(ErrorCode.AI_RESPONSE_INVALID));

    assertErrorCode(
        () -> turnHelpService.hint(userId, npcTurn.getId(), UUID.randomUUID()),
        ErrorCode.AI_RESPONSE_INVALID);
    Turn saved = savedNpcTurn();
    assertThat(saved.getHintText()).isNull();
    assertThat(saved.getHintOpenedAt()).isNull();
  }

  @Test
  @DisplayName("사용자 턴, 다른 사용자의 턴, 없는 턴의 힌트는 워커를 부르지 않고 NOT_FOUND다")
  void hint_unavailableTurns_throwNotFound() {
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Other");

    assertErrorCode(
        () -> turnHelpService.hint(userId, userTurn.getId(), UUID.randomUUID()),
        ErrorCode.NOT_FOUND);
    assertErrorCode(
        () -> turnHelpService.hint(otherUserId, npcTurn.getId(), UUID.randomUUID()),
        ErrorCode.NOT_FOUND);
    assertErrorCode(
        () -> turnHelpService.hint(userId, Long.MAX_VALUE, UUID.randomUUID()), ErrorCode.NOT_FOUND);
    then(aiGateway).should(never()).hint(any(), any(), anyList(), anyString(), anyInt());
  }

  @Test
  @DisplayName("처음 추천 단어를 요청하면 그 NPC 대사까지의 대화로 워커를 불러 단어를 저장한다. 워커 요청에는 요청 ID와 대화를 붙인다")
  @SuppressWarnings("unchecked")
  void suggestWords_firstRequest_usesHistoryUntilTurnAndSaves() {
    textTurn(entityManager, conversation, 4, "For here.");
    entityManager.flush();
    given(aiGateway.suggest(any(), any(), anyList(), eq(""), eq(1))).willReturn(SUGGEST);
    UUID requestId = UUID.randomUUID();

    SuggestWordsResponse response =
        turnHelpService.suggestWords(userId, npcTurn.getId(), requestId);

    assertThat(response)
        .isEqualTo(new SuggestWordsResponse(npcTurn.getId(), List.of("for here", "to go")));
    ArgumentCaptor<List<WorkerLine>> history = ArgumentCaptor.forClass(List.class);
    then(aiGateway)
        .should()
        .suggest(
            eq(new WorkerTrace(requestId, userId, conversation.getId())),
            any(),
            history.capture(),
            eq(""),
            eq(1));
    assertThat(history.getValue())
        .extracting(WorkerLine::who, WorkerLine::text)
        .containsExactly(
            tuple("npc", "Hi! What can I get for you?"),
            tuple("me", "A latte, please."),
            tuple("npc", NPC_LINE));
    assertThat(savedNpcTurn().getSuggestWords()).isEqualTo("[\"for here\", \"to go\"]");
  }

  @Test
  @DisplayName("한 번 만든 추천 단어는 다시 요청해도 워커를 부르지 않는다")
  void suggestWords_savedTurn_returnsSavedWordsWithoutAi() {
    given(aiGateway.suggest(any(), any(), anyList(), anyString(), anyInt())).willReturn(SUGGEST);
    turnHelpService.suggestWords(userId, npcTurn.getId(), UUID.randomUUID());

    SuggestWordsResponse again =
        turnHelpService.suggestWords(userId, npcTurn.getId(), UUID.randomUUID());

    assertThat(again.suggestWords()).containsExactly("for here", "to go");
    then(aiGateway).should(times(1)).suggest(any(), any(), anyList(), anyString(), anyInt());
  }

  @Test
  @DisplayName("쓸 수 없는 추천 단어(AI_RESPONSE_INVALID)면 오류를 그대로 알리고 추천 단어를 저장하지 않는다")
  void suggestWords_withoutWords_throwsInvalidAndSavesNothing() {
    given(aiGateway.suggest(any(), any(), anyList(), anyString(), anyInt()))
        .willThrow(new BusinessException(ErrorCode.AI_RESPONSE_INVALID));

    assertErrorCode(
        () -> turnHelpService.suggestWords(userId, npcTurn.getId(), UUID.randomUUID()),
        ErrorCode.AI_RESPONSE_INVALID);
    assertThat(savedNpcTurn().getSuggestWords()).isNull();
  }

  @Test
  @DisplayName("사용자 턴, 다른 사용자의 턴, 없는 턴의 추천 단어는 워커를 부르지 않고 NOT_FOUND다")
  void suggestWords_unavailableTurns_throwNotFound() {
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Other");

    assertErrorCode(
        () -> turnHelpService.suggestWords(userId, userTurn.getId(), UUID.randomUUID()),
        ErrorCode.NOT_FOUND);
    assertErrorCode(
        () -> turnHelpService.suggestWords(otherUserId, npcTurn.getId(), UUID.randomUUID()),
        ErrorCode.NOT_FOUND);
    assertErrorCode(
        () -> turnHelpService.suggestWords(userId, Long.MAX_VALUE, UUID.randomUUID()),
        ErrorCode.NOT_FOUND);
    then(aiGateway).should(never()).suggest(any(), any(), anyList(), anyString(), anyInt());
  }

  private Turn savedNpcTurn() {
    entityManager.flush();
    entityManager.clear();
    return entityManager.find(Turn.class, npcTurn.getId());
  }

  private static void assertErrorCode(ThrowingCallable call, ErrorCode expected) {
    assertThatThrownBy(call)
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(expected));
  }
}
