package com.elsehere.memory.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static com.elsehere.conversation.DialogueTestData.npcId;
import static com.elsehere.conversation.DialogueTestData.npcTurn;
import static com.elsehere.conversation.DialogueTestData.textTurn;
import static com.elsehere.conversation.DialogueTestData.usage;
import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.ArgumentMatchers.isNull;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.times;

import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.client.MemoryCardResult;
import com.elsehere.ai.client.WorkerLine;
import com.elsehere.ai.client.WorkerMemory;
import com.elsehere.ai.entity.AiRequestKind;
import com.elsehere.ai.entity.AiRequestStatus;
import com.elsehere.ai.repository.AiRequestRepository;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.service.DialogueServiceTest;
import com.elsehere.memory.entity.UserNpcRelation;
import com.elsehere.memory.entity.UserNpcRelationId;
import com.elsehere.memory.repository.UserNpcRelationRepository;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.time.Instant;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.beans.factory.annotation.Autowired;

// 카드 생성은 AI 워커 결과를 Mock으로 정하고, 저장 결과는 방문 수와 다음 대화에 넘길 기억(memoryFor)으로 확인한다.
@DialogueServiceTest
class MemoryServiceTest {

  private static final String NORMAL_OUTCOME = "The learner ended the conversation normally.";
  private static final String FIRST_CARD =
      "{\"name\": \"\", \"claims\": [\"likes lattes\"], \"events\": [], \"attitude\": \"friendly\","
          + " \"struggled\": []}";
  private static final String SECOND_CARD =
      "{\"name\": \"Sun\", \"claims\": [\"likes lattes\"], \"events\": [\"ordered twice\"],"
          + " \"attitude\": \"friendly\", \"struggled\": []}";

  @Autowired private MemoryService memoryService;

  @Autowired private AiWorkerClient aiWorkerClient;

  @Autowired private UserNpcRelationRepository userNpcRelationRepository;

  @Autowired private AiRequestRepository aiRequestRepository;

  @Autowired private EntityManager entityManager;

  private Long userId;
  private Long npcId;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    npcId = npcId(entityManager, "CAFE_STAFF");
  }

  @Test
  @DisplayName("처음 만난 NPC는 넘길 기억이 없다")
  void memoryFor_beforeAnyConversation_isNull() {
    assertThat(memoryService.memoryFor(userId, npcId)).isNull();
  }

  @Test
  @DisplayName("사용자가 말한 대화가 끝나면 방문 수를 1로 세고 워커가 만든 카드를 저장해 다음 대화에 넘긴다")
  void updateAfterConversation_firstVisit_savesCard() {
    given(aiWorkerClient.memoryCard(any(), any(), anyString(), any(), isNull()))
        .willReturn(new MemoryCardResult(FIRST_CARD, List.of(usage("memory"))));
    Conversation conversation = endedConversation("I want a latte");
    UUID requestId = UUID.randomUUID();

    memoryService.updateAfterConversation(userId, conversation.getId(), requestId);

    WorkerMemory memory = memoryService.memoryFor(userId, npcId);
    assertThat(memory.visits()).isEqualTo(1);
    assertThat(memory.lastOrder()).isNull();
    assertThat(memory.card()).isEqualTo(card("", List.of()));
    assertThat(
            aiRequestRepository
                .findByRequestIdAndKind(requestId, AiRequestKind.MEMORY)
                .orElseThrow()
                .getStatus())
        .isEqualTo(AiRequestStatus.OK);
  }

  @Test
  @DisplayName("워커에는 대화 결과 한 줄과 대화 전체를 보내며, 처음이면 이전 카드는 보내지 않는다")
  void updateAfterConversation_sendsOutcomeAndHistory() {
    given(aiWorkerClient.memoryCard(any(), any(), anyString(), any(), any()))
        .willReturn(new MemoryCardResult(FIRST_CARD, List.of()));
    Conversation conversation = endedConversation("I want a latte");

    memoryService.updateAfterConversation(userId, conversation.getId(), UUID.randomUUID());

    @SuppressWarnings("unchecked")
    ArgumentCaptor<List<WorkerLine>> history = ArgumentCaptor.forClass(List.class);
    then(aiWorkerClient)
        .should()
        .memoryCard(any(), any(), eq(NORMAL_OUTCOME), history.capture(), isNull());
    assertThat(history.getValue())
        .containsExactly(
            WorkerLine.npc("Hi! What can I get for you?"),
            new WorkerLine("me", "I want a latte", null, false),
            WorkerLine.npc("Coming right up."));
  }

  @Test
  @DisplayName("다음 대화가 끝나면 이전 카드를 넘겨 새 카드로 통째로 바꾸고 방문 수를 2로 센다")
  void updateAfterConversation_secondVisit_replacesCard() {
    given(aiWorkerClient.memoryCard(any(), any(), anyString(), any(), any()))
        .willReturn(new MemoryCardResult(FIRST_CARD, List.of()))
        .willReturn(new MemoryCardResult(SECOND_CARD, List.of()));
    Conversation first = endedConversation("I want a latte");
    memoryService.updateAfterConversation(userId, first.getId(), UUID.randomUUID());
    Conversation second = endedConversation("Another latte please");

    memoryService.updateAfterConversation(userId, second.getId(), UUID.randomUUID());

    ArgumentCaptor<Object> previousCard = ArgumentCaptor.forClass(Object.class);
    then(aiWorkerClient)
        .should(times(2))
        .memoryCard(any(), any(), anyString(), any(), previousCard.capture());
    assertThat(previousCard.getAllValues().get(1)).isEqualTo(card("", List.of()));
    WorkerMemory memory = memoryService.memoryFor(userId, npcId);
    assertThat(memory.visits()).isEqualTo(2);
    assertThat(memory.card()).isEqualTo(card("Sun", List.of("ordered twice")));
  }

  @Test
  @DisplayName("같은 대화의 종료를 두 번 반영해도 방문 수는 한 번만 세고 카드도 한 번만 만든다")
  void updateAfterConversation_sameConversationTwice_countsOnce() {
    given(aiWorkerClient.memoryCard(any(), any(), anyString(), any(), any()))
        .willReturn(new MemoryCardResult(FIRST_CARD, List.of()));
    Conversation conversation = endedConversation("I want a latte");

    memoryService.updateAfterConversation(userId, conversation.getId(), UUID.randomUUID());
    memoryService.updateAfterConversation(userId, conversation.getId(), UUID.randomUUID());

    assertThat(memoryService.memoryFor(userId, npcId).visits()).isEqualTo(1);
    then(aiWorkerClient).should().memoryCard(any(), any(), anyString(), any(), any());
  }

  @Test
  @DisplayName("사용자가 한마디도 하지 않은 대화는 방문으로 세지 않고 카드를 만들지 않는다")
  void updateAfterConversation_withoutUserTurns_isIgnored() {
    Conversation conversation = conversation(entityManager, userId, "CAFE_STAFF");
    npcTurn(entityManager, conversation, 1, "Hi! What can I get for you?");
    conversation.endByUser(Instant.parse("2026-10-27T01:05:00Z"));

    memoryService.updateAfterConversation(userId, conversation.getId(), UUID.randomUUID());

    assertThat(memoryService.memoryFor(userId, npcId)).isNull();
    then(aiWorkerClient).should(never()).memoryCard(any(), any(), any(), any(), any());
  }

  @Test
  @DisplayName("아직 진행 중인 대화는 반영하지 않는다")
  void updateAfterConversation_activeConversation_isIgnored() {
    Conversation conversation = conversation(entityManager, userId, "CAFE_STAFF");
    textTurn(entityManager, conversation, 1, "Hello");

    memoryService.updateAfterConversation(userId, conversation.getId(), UUID.randomUUID());

    assertThat(userNpcRelationRepository.findById(new UserNpcRelationId(userId, npcId))).isEmpty();
    then(aiWorkerClient).should(never()).memoryCard(any(), any(), any(), any(), any());
  }

  @Test
  @DisplayName("카드를 만들지 못하면 방문 수는 세되 이전 카드를 그대로 두고 호출 기록을 FAILED로 남긴다")
  void updateAfterConversation_workerFails_keepsPreviousCard() {
    given(aiWorkerClient.memoryCard(any(), any(), anyString(), any(), any()))
        .willReturn(new MemoryCardResult(FIRST_CARD, List.of()))
        .willThrow(new BusinessException(ErrorCode.AI_RATE_LIMITED));
    Conversation first = endedConversation("I want a latte");
    memoryService.updateAfterConversation(userId, first.getId(), UUID.randomUUID());
    Conversation second = endedConversation("Another latte please");
    UUID requestId = UUID.randomUUID();

    memoryService.updateAfterConversation(userId, second.getId(), requestId);

    WorkerMemory memory = memoryService.memoryFor(userId, npcId);
    assertThat(memory.visits()).isEqualTo(2);
    assertThat(memory.card()).isEqualTo(card("", List.of()));
    assertThat(
            aiRequestRepository
                .findByRequestIdAndKind(requestId, AiRequestKind.MEMORY)
                .orElseThrow()
                .getStatus())
        .isEqualTo(AiRequestStatus.FAILED);
    UserNpcRelation relation =
        userNpcRelationRepository.findById(new UserNpcRelationId(userId, npcId)).orElseThrow();
    assertThat(relation.getMemoryConversationId()).isEqualTo(second.getId());
  }

  private Conversation endedConversation(String userText) {
    Conversation conversation = conversation(entityManager, userId, "CAFE_STAFF");
    npcTurn(entityManager, conversation, 1, "Hi! What can I get for you?");
    textTurn(entityManager, conversation, 2, userText);
    npcTurn(entityManager, conversation, 3, "Coming right up.");
    conversation.endByUser(Instant.parse("2026-10-27T01:05:00Z"));
    entityManager.flush();
    return conversation;
  }

  // FIRST_CARD·SECOND_CARD를 읽은 결과. 둘은 이름과 사건만 다르다.
  private static Map<String, Object> card(String name, List<String> events) {
    return Map.of(
        "name",
        name,
        "claims",
        List.of("likes lattes"),
        "events",
        events,
        "attitude",
        "friendly",
        "struggled",
        List.of());
  }
}
