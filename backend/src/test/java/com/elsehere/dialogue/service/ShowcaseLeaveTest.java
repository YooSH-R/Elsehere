package com.elsehere.dialogue.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static com.elsehere.conversation.DialogueTestData.npcTurn;
import static com.elsehere.conversation.DialogueTestData.textTurn;
import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;

import com.elsehere.ai.service.AiGateway;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.dialogue.dto.EndConversationRequest;
import com.elsehere.mission.entity.ShowcaseJourney;
import com.elsehere.mission.entity.ShowcaseStage;
import com.elsehere.mission.repository.ShowcaseJourneyRepository;
import com.elsehere.mission.service.ShowcaseScene;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;

/** 공용 서버(시연 설정 꺼짐)에서 시연이 덮어쓴 NPC 관계와 게임 시각이 시연을 벗어나면 시작 전 값으로 돌아오는지 확인한다. */
@DialogueDbTest
class ShowcaseLeaveTest {
  @Autowired private DialogueEndService dialogueEndService;
  @Autowired private AiGateway gateway;
  @Autowired private EntityManager entityManager;
  @Autowired private ShowcaseJourneyRepository journeys;
  @Autowired private ShowcaseScene showcaseScene;
  private Long userId;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "showcase-leave-test");
    // 시연 전: Jake와는 이미 친하고, Owen·Rosa·택시 기사와는 만난 적이 없다. 게임 시각은 5일차 안이다.
    entityManager
        .createNativeQuery(
            "insert into user_npc_relations (user_id, npc_id, affinity, recognition, talk_count,"
                + " last_talk_game_day, memory_card)"
                + " select ?1, id, 30, 40, 2, 4, '{\"events\":[\"Lent an umbrella.\"]}'"
                + " from npcs where code = 'HOTEL_STAFF'")
        .setParameter(1, userId)
        .executeUpdate();
    entityManager
        .createNativeQuery("insert into user_game_clocks (user_id, play_seconds) values (?1, 2700)")
        .setParameter(1, userId)
        .executeUpdate();
  }

  @Test
  void leavingPutsBackTheRelationsAndClockFromBeforeTheStart() {
    showcaseScene.setUp(userId);
    setAffinity("DINER_STAFF", 93);

    boolean left = showcaseScene.leave(userId);
    entityManager.clear();

    assertThat(left).isTrue();
    assertThat(relation("HOTEL_STAFF")).containsExactly(30, 40, 2, 4, "Lent an umbrella.");
    assertThat(hasRelation("HOTEL_RECEPTIONIST")).isFalse();
    assertThat(hasRelation("DINER_STAFF")).isFalse();
    assertThat(playSeconds()).isEqualTo(2700L);
    assertThat(showcaseScene.leave(userId)).isFalse();
  }

  // 게임 시계가 아직 없던 계정은 시연이 만든 시계 행도 남기지 않는다.
  @Test
  void leavingRemovesAClockThatDidNotExistBeforeTheStart() {
    entityManager
        .createNativeQuery("delete from user_game_clocks where user_id = ?1")
        .setParameter(1, userId)
        .executeUpdate();
    showcaseScene.setUp(userId);

    showcaseScene.leave(userId);

    assertThat(
            entityManager
                .createNativeQuery("select count(*) from user_game_clocks where user_id = ?1")
                .setParameter(1, userId)
                .getSingleResult())
        .isEqualTo(0L);
  }

  // 같은 계정으로 다시 처음부터 시작해도 보관해 둔 값은 맨 처음 시작 전 값이다.
  @Test
  void startingAgainBeforeLeavingKeepsTheFirstSavedState() {
    showcaseScene.setUp(userId);
    showcaseScene.setUp(userId);

    showcaseScene.leave(userId);
    entityManager.clear();

    assertThat(relation("HOTEL_STAFF")).containsExactly(30, 40, 2, 4, "Lent an umbrella.");
    assertThat(playSeconds()).isEqualTo(2700L);
  }

  // 시연을 멈추면 시작 전 상태로 돌아가고, 이어 하면 멈춘 때의 시연 상태로 돌아온다.
  @Test
  void pausingPutsTheOriginalBackAndResumingBringsTheShowcaseBack() {
    showcaseScene.setUp(userId);
    setAffinity("DINER_STAFF", 93);

    showcaseScene.pause(userId);
    entityManager.clear();

    assertThat(relation("HOTEL_STAFF")).containsExactly(30, 40, 2, 4, "Lent an umbrella.");
    assertThat(hasRelation("DINER_STAFF")).isFalse();
    assertThat(playSeconds()).isEqualTo(2700L);

    setAffinity("HOTEL_STAFF", 35);
    showcaseScene.resume(userId);
    entityManager.clear();

    assertThat(affinity("DINER_STAFF")).isEqualTo(93);
    assertThat(affinity("HOTEL_STAFF")).isZero();
    assertThat(playSeconds()).isEqualTo(237L);

    showcaseScene.leave(userId);
    entityManager.clear();

    // 멈춘 동안 일반 플레이로 바뀐 값이 시작 전 값이 된다.
    assertThat(affinity("HOTEL_STAFF")).isEqualTo(35);
    assertThat(hasRelation("DINER_STAFF")).isFalse();
  }

  // 주문을 마쳐 시연을 끝낸 대화를 닫으면 시작 전 상태로 돌아간다. 그 대화는 되돌린 기억 카드에 반영하지 않는다.
  @Test
  void endingTheConversationThatCompletedTheShowcaseRestoresTheAccount() {
    showcaseScene.setUp(userId);
    ShowcaseJourney journey = ShowcaseJourney.start(userId);
    journey.accept(0, "HOTEL_STAFF", ShowcaseStage.HOTEL.goalIds());
    journey.chooseTaxiDestination("TAXI_DRIVER", "DINER");
    journey.arriveByTaxi("DINER");
    journey.accept(2, "DINER_STAFF", ShowcaseStage.ORDER.goalIds());
    journeys.saveAndFlush(journey);
    Conversation conversation = conversation(entityManager, userId, "DINER_STAFF");
    conversation.attachShowcaseStage(ShowcaseStage.ORDER.ordinal());
    npcTurn(entityManager, conversation, 1, "Welcome to Maple Burger.");
    textTurn(entityManager, conversation, 2, "A cheeseburger, fries and Coke, to go please.");
    entityManager.flush();

    dialogueEndService.end(
        userId, conversation.getId(), new EndConversationRequest(UUID.randomUUID(), true));
    entityManager.clear();

    assertThat(journey.isCompleted()).isTrue();
    assertThat(relation("HOTEL_STAFF")).containsExactly(30, 40, 2, 4, "Lent an umbrella.");
    assertThat(hasRelation("DINER_STAFF")).isFalse();
    assertThat(playSeconds()).isEqualTo(2700L);
    then(gateway).should(never()).memoryCard(any(), any(), any(), any(), any());
  }

  private void setAffinity(String npcCode, int affinity) {
    entityManager
        .createNativeQuery(
            "update user_npc_relations set affinity = ?3 where user_id = ?1"
                + " and npc_id = (select id from npcs where code = ?2)")
        .setParameter(1, userId)
        .setParameter(2, npcCode)
        .setParameter(3, affinity)
        .executeUpdate();
  }

  private int affinity(String npcCode) {
    return ((Number) relation(npcCode).get(0)).intValue();
  }

  private boolean hasRelation(String npcCode) {
    return !rows(npcCode).isEmpty();
  }

  private List<Object> relation(String npcCode) {
    Object[] row = (Object[]) rows(npcCode).get(0);
    return java.util.Arrays.asList(
        ((Number) row[0]).intValue(), ((Number) row[1]).intValue(), row[2], row[3], row[4]);
  }

  private List<?> rows(String npcCode) {
    return entityManager
        .createNativeQuery(
            "select r.affinity, r.recognition, r.talk_count, r.last_talk_game_day,"
                + " r.memory_card -> 'events' ->> 0"
                + " from user_npc_relations r join npcs n on n.id = r.npc_id"
                + " where r.user_id = ?1 and n.code = ?2")
        .setParameter(1, userId)
        .setParameter(2, npcCode)
        .getResultList();
  }

  private Object playSeconds() {
    return entityManager
        .createNativeQuery("select play_seconds from user_game_clocks where user_id = ?1")
        .setParameter(1, userId)
        .getSingleResult();
  }
}
