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
import com.elsehere.ai.client.RelationshipChange;
import com.elsehere.ai.client.WorkerLine;
import com.elsehere.ai.client.WorkerMemory;
import com.elsehere.ai.client.WorkerRelationship;
import com.elsehere.ai.entity.AiRequestKind;
import com.elsehere.ai.entity.AiRequestStatus;
import com.elsehere.ai.repository.AiRequestRepository;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.dialogue.service.DialogueServiceTest;
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
  @DisplayName("끝낸 대화가 있는 NPC에는 저장된 알아봄·호감을 지난 날 0과 함께 관계로 넘긴다")
  void memoryFor_afterConversation_includesRelationship() {
    relation(23, 3, 1);

    assertThat(memoryService.memoryFor(userId, npcId).relationship())
        .isEqualTo(new WorkerRelationship(23, 3, 0));
  }

  @Test
  @DisplayName("대화가 끝나면 그때의 게임 날짜를 마지막 대화 날로 저장한다")
  void updateAfterConversation_savesLastTalkGameDay() {
    gameDay(2);
    givenCard();

    memoryService.updateAfterConversation(
        userId, endedConversation("I want a latte").getId(), UUID.randomUUID());

    entityManager.flush();
    entityManager.clear();
    assertThat(
            userNpcRelationRepository
                .findById(new UserNpcRelationId(userId, npcId))
                .orElseThrow()
                .getLastTalkGameDay())
        .isEqualTo(2);
  }

  @Test
  @DisplayName("다음 대답에는 마지막 대화 뒤 지난 게임 날 수를 관계에 담아 넘긴다")
  void memoryFor_includesDaysSinceLastTalk() {
    relation(23, 3, 1, 1);
    gameDay(4);

    assertThat(memoryService.memoryFor(userId, npcId).relationship())
        .isEqualTo(new WorkerRelationship(23, 3, 3));
  }

  @Test
  @DisplayName("관계 갱신에는 이번 대화 전 마지막 대화 뒤 지난 게임 날 수를 보낸다")
  void updateAfterConversation_sendsDaysSinceLastTalkBeforeThisConversation() {
    relation(15, 2, 1, 1);
    gameDay(4);
    givenCard();
    given(aiWorkerClient.relationship(any(), any(), any(), any()))
        .willReturn(new RelationshipChange(8));

    memoryService.updateAfterConversation(
        userId, endedConversation("I want a latte").getId(), UUID.randomUUID());

    then(aiWorkerClient)
        .should()
        .relationship(any(), any(), eq(new WorkerRelationship(15, 2, 3)), any());
  }

  @Test
  @DisplayName("대화가 끝나면 저장된 관계와 사건이 담긴 대화로 관계 갱신을 불러 알아봄 변화량만 더한다(호감은 발화마다 이미 바뀜)")
  void updateAfterConversation_appliesRecognitionDeltaOnly() {
    relation(15, 2, 1);
    givenCard();
    given(aiWorkerClient.relationship(any(), any(), any(), any()))
        .willReturn(new RelationshipChange(8));
    Conversation conversation = endedConversation("Hi! One latte, please.");
    entityManager
        .createNativeQuery(
            "update turns set events = '[\"GREETING\"]' where conversation_id = ? and seq = 2")
        .setParameter(1, conversation.getId())
        .executeUpdate();
    entityManager.clear();

    memoryService.updateAfterConversation(userId, conversation.getId(), UUID.randomUUID());

    @SuppressWarnings("unchecked")
    ArgumentCaptor<List<WorkerLine>> history = ArgumentCaptor.forClass(List.class);
    then(aiWorkerClient)
        .should()
        .relationship(any(), any(), eq(new WorkerRelationship(15, 2, 0)), history.capture());
    assertThat(history.getValue())
        .contains(WorkerLine.learner("Hi! One latte, please.", null, false, List.of("GREETING")));
    assertThat(relationValues()).containsExactly(23, 2);
  }

  @Test
  @DisplayName("변화량을 더해도 알아봄은 0~100을 넘지 않는다")
  void updateAfterConversation_recognitionDelta_staysInRange() {
    relation(98, -99, 1);
    givenCard();
    given(aiWorkerClient.relationship(any(), any(), any(), any()))
        .willReturn(new RelationshipChange(8));

    memoryService.updateAfterConversation(
        userId, endedConversation("I want a latte").getId(), UUID.randomUUID());

    assertThat(relationValues()).containsExactly(100, -99);
  }

  @Test
  @DisplayName("관계 갱신에 실패하면 관계는 그대로 두고 기억 카드는 저장한다")
  void updateAfterConversation_relationshipFails_keepsRelationshipAndSavesCard() {
    relation(15, 2, 1);
    givenCard();
    given(aiWorkerClient.relationship(any(), any(), any(), any()))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));

    memoryService.updateAfterConversation(
        userId, endedConversation("I want a latte").getId(), UUID.randomUUID());

    assertThat(relationValues()).containsExactly(15, 2);
    assertThat(memoryService.memoryFor(userId, npcId).card()).isEqualTo(card("", List.of()));
  }

  @Test
  @DisplayName("기억 카드를 만들지 못해도 관계 갱신은 한다")
  void updateAfterConversation_cardFails_stillAppliesRelationship() {
    relation(15, 2, 1);
    given(aiWorkerClient.memoryCard(any(), any(), anyString(), any(), any()))
        .willThrow(new BusinessException(ErrorCode.AI_RATE_LIMITED));
    given(aiWorkerClient.relationship(any(), any(), any(), any()))
        .willReturn(new RelationshipChange(8));

    memoryService.updateAfterConversation(
        userId, endedConversation("I want a latte").getId(), UUID.randomUUID());

    assertThat(relationValues()).containsExactly(23, 2);
  }

  @Test
  @DisplayName("같은 대화의 종료를 두 번 반영해도 관계 갱신은 한 번만 한다")
  void updateAfterConversation_sameConversationTwice_appliesRelationshipOnce() {
    givenCard();
    given(aiWorkerClient.relationship(any(), any(), any(), any()))
        .willReturn(new RelationshipChange(8));
    Conversation conversation = endedConversation("I want a latte");

    memoryService.updateAfterConversation(userId, conversation.getId(), UUID.randomUUID());
    memoryService.updateAfterConversation(userId, conversation.getId(), UUID.randomUUID());

    then(aiWorkerClient).should().relationship(any(), any(), any(), any());
    assertThat(relationValues()).containsExactly(8, 0);
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

  @Test
  @DisplayName("카드를 만드는 사이 무례로 깎인 호감도는 카드를 저장할 때 덮어쓰지 않는다")
  void updateAfterConversation_affinityChangedMeanwhile_keepsAffinity() {
    Conversation conversation = endedConversation("I want a latte");
    given(aiWorkerClient.memoryCard(any(), any(), anyString(), any(), any()))
        .willAnswer(
            invocation -> {
              // 워커를 기다리는 사이 다음 대화의 무례한 말로 호감도가 깎인다.
              entityManager
                  .createNativeQuery(
                      "update user_npc_relations set affinity = -5 where user_id = ? and npc_id = ?")
                  .setParameter(1, userId)
                  .setParameter(2, npcId)
                  .executeUpdate();
              return new MemoryCardResult(FIRST_CARD, List.of());
            });

    memoryService.updateAfterConversation(userId, conversation.getId(), UUID.randomUUID());

    entityManager.flush();
    assertThat(
            entityManager
                .createNativeQuery(
                    "select affinity from user_npc_relations where user_id = ? and npc_id = ?")
                .setParameter(1, userId)
                .setParameter(2, npcId)
                .getSingleResult())
        .isEqualTo((short) -5);
  }

  private void givenCard() {
    given(aiWorkerClient.memoryCard(any(), any(), anyString(), any(), any()))
        .willReturn(new MemoryCardResult(FIRST_CARD, List.of()));
  }

  // 이미 끝낸 대화가 있는 관계. 다른 칸은 기본값이다.
  private void relation(int recognition, int affinity, int talkCount) {
    relation(recognition, affinity, talkCount, null);
  }

  private void relation(int recognition, int affinity, int talkCount, Integer lastTalkGameDay) {
    entityManager
        .createNativeQuery(
            "insert into user_npc_relations"
                + " (user_id, npc_id, recognition, affinity, talk_count, last_talk_game_day)"
                + " values (?, ?, ?, ?, ?, ?)")
        .setParameter(1, userId)
        .setParameter(2, npcId)
        .setParameter(3, recognition)
        .setParameter(4, affinity)
        .setParameter(5, talkCount)
        .setParameter(6, lastTalkGameDay)
        .executeUpdate();
  }

  // 사용자의 게임 시계를 그 날짜의 아침으로 맞춘다. 실제 10분(600초)이 게임 하루다.
  private void gameDay(int day) {
    entityManager
        .createNativeQuery("insert into user_game_clocks (user_id, play_seconds) values (?, ?)")
        .setParameter(1, userId)
        .setParameter(2, (day - 1) * 600L)
        .executeUpdate();
  }

  // 지금 저장된 알아봄·호감.
  private List<Integer> relationValues() {
    entityManager.flush();
    Object[] row =
        (Object[])
            entityManager
                .createNativeQuery(
                    "select recognition, affinity from user_npc_relations"
                        + " where user_id = ? and npc_id = ?")
                .setParameter(1, userId)
                .setParameter(2, npcId)
                .getSingleResult();
    return List.of(((Number) row[0]).intValue(), ((Number) row[1]).intValue());
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
