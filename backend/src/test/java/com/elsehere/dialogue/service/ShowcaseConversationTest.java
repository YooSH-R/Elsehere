package com.elsehere.dialogue.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static com.elsehere.conversation.DialogueTestData.npcTurn;
import static com.elsehere.conversation.DialogueTestData.reply;
import static com.elsehere.conversation.DialogueTestData.textTurn;
import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;

import com.elsehere.ai.client.FollowUpResult;
import com.elsehere.ai.client.WorkerFollowUp;
import com.elsehere.ai.service.AiGateway;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.dialogue.dto.EndConversationRequest;
import com.elsehere.dialogue.dto.EndConversationResponse;
import com.elsehere.dialogue.dto.StartConversationRequest;
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
import org.mockito.ArgumentCaptor;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.test.context.TestPropertySource;

@DialogueDbTest
@TestPropertySource(properties = "elsehere.showcase.enabled=true")
class ShowcaseConversationTest {
  @Autowired private DialogueStartService dialogueStartService;
  @Autowired private DialogueEndService dialogueEndService;
  @Autowired private AiGateway gateway;
  @Autowired private EntityManager entityManager;
  @Autowired private ShowcaseJourneyRepository journeys;
  @Autowired private ShowcaseScene showcaseScene;
  private Long userId;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "showcase-test");
  }

  // 시연을 처음부터 시작하면 대본대로 Owen은 사이가 나쁘고 Rosa는 단골을 알아보며, Jake·택시 기사는 처음 만나는 상태다. 시각은 해 지기 전 저녁이다.
  @Test
  void sceneSetUpPutsTheScriptedRelationsAndEveningClock() {
    Object jakeId =
        entityManager
            .createNativeQuery("select id from npcs where code = 'HOTEL_STAFF'")
            .getSingleResult();
    entityManager
        .createNativeQuery(
            "insert into user_npc_relations (user_id, npc_id, affinity, recognition, talk_count,"
                + " memory_card) values (?1, ?2, 30, 40, 2, '{\"events\":[\"Lent an umbrella.\"]}')")
        .setParameter(1, userId)
        .setParameter(2, jakeId)
        .executeUpdate();

    showcaseScene.setUp(userId);
    entityManager.clear();

    assertThat(relation("HOTEL_RECEPTIONIST")).containsExactly(-40, 20, 1, "wary");
    assertThat(relation("DINER_STAFF")).containsExactly(100, 60, 6, "friendly");
    assertThat(relation("HOTEL_STAFF")).containsExactly(0, 0, 0, null);
    assertThat(
            entityManager
                .createNativeQuery(
                    "select memory_card ->> 'name' || ' / ' || (memory_card -> 'events' ->> 0)"
                        + " from user_npc_relations r join npcs n on n.id = r.npc_id"
                        + " where r.user_id = ?1 and n.code = 'DINER_STAFF'")
                .setParameter(1, userId)
                .getSingleResult())
        .isEqualTo(
            entityManager
                    .createNativeQuery("select nickname from users where id = ?1")
                    .setParameter(1, userId)
                    .getSingleResult()
                + " / "
                + ShowcaseScene.ROSA_USUAL_MEMORY);
    assertThat(
            entityManager
                .createNativeQuery("select play_seconds from user_game_clocks where user_id = ?1")
                .setParameter(1, userId)
                .getSingleResult())
        .isEqualTo(237L);
  }

  // 게스트 닉네임(guest-000001)은 사람 이름이 아니라서, 기억 카드에는 NPC가 부를 이름 kimssafy를 남긴다.
  @Test
  void sceneSetUpRemembersAGuestByTheGuestName() {
    Long guestId =
        UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid-1", "guest-000001");

    showcaseScene.setUp(guestId);
    entityManager.clear();

    assertThat(
            entityManager
                .createNativeQuery(
                    "select memory_card ->> 'name' from user_npc_relations r"
                        + " join npcs n on n.id = r.npc_id"
                        + " where r.user_id = ?1 and n.code = 'DINER_STAFF'")
                .setParameter(1, guestId)
                .getSingleResult())
        .isEqualTo("kimssafy");
  }

  private List<Object> relation(String npcCode) {
    Object[] row =
        (Object[])
            entityManager
                .createNativeQuery(
                    "select r.affinity, r.recognition, r.talk_count, r.memory_card ->> 'attitude'"
                        + " from user_npc_relations r join npcs n on n.id = r.npc_id"
                        + " where r.user_id = ?1 and n.code = ?2")
                .setParameter(1, userId)
                .setParameter(2, npcCode)
                .getSingleResult();
    return java.util.Arrays.asList(
        ((Number) row[0]).intValue(), ((Number) row[1]).intValue(), row[2], row[3]);
  }

  // 버거 가게 첫 인사는 단골이면 늘 시키던 메뉴를 묻게 한다. 다른 퀘스트의 "제안을 먼저 말하지 말라"는 문구가 이를 막지 않게 한다.
  @Test
  void burgerShopGreetingAsksARegularAboutTheirUsual() {
    ShowcaseJourney journey = ShowcaseJourney.start(userId);
    journey.accept(0, "HOTEL_STAFF", ShowcaseStage.HOTEL.goalIds());
    journey.chooseTaxiDestination("TAXI_DRIVER", "DINER");
    journey.arriveByTaxi("DINER");
    journeys.saveAndFlush(journey);
    given(gateway.reply(any(), any(), any(), any(), anyString(), anyString(), anyInt(), any()))
        .willReturn(reply("Hi, hon! The usual?", null));

    dialogueStartService.start(
        userId, new StartConversationRequest(UUID.randomUUID(), "DINER_STAFF", InitiatedBy.USER));

    ArgumentCaptor<String> instruction = ArgumentCaptor.forClass(String.class);
    then(gateway)
        .should()
        .reply(any(), any(), any(), any(), anyString(), instruction.capture(), anyInt(), any());
    assertThat(instruction.getValue())
        .contains("their usual", "name it")
        .doesNotContain("your offers or directions yet");
  }

  @Test
  void burgerDemoKeepsFeedbackButNeverCreatesAWaitingItem() {
    Conversation conversation = conversation(entityManager, userId, "DINER_STAFF");
    conversation.attachShowcaseStage(ShowcaseStage.ORDER.ordinal());
    npcTurn(entityManager, conversation, 1, "Welcome to Maple Burger.");
    textTurn(entityManager, conversation, 2, "A cheeseburger, fries and Coke, to go please.");
    given(gateway.followUp(any(), any(), any()))
        .willReturn(new FollowUpResult(new WorkerFollowUp("burger", "햄버거", "QUICK"), List.of()));
    entityManager.flush();

    EndConversationResponse ended =
        dialogueEndService.end(
            userId, conversation.getId(), new EndConversationRequest(UUID.randomUUID(), true));

    assertThat(ended.followUp()).isNull();
    assertThat(ended.userTurnCount()).isEqualTo(1);
    then(gateway).should(never()).followUp(any(), any(), any());
  }
}
