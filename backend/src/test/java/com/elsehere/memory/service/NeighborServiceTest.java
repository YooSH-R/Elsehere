package com.elsehere.memory.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static com.elsehere.conversation.DialogueTestData.npcId;
import static com.elsehere.conversation.DialogueTestData.npcTurn;
import static com.elsehere.conversation.DialogueTestData.textTurn;
import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.config.TestClockConfig;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.memory.dto.NeighborListResponse;
import com.elsehere.memory.dto.NeighborResponse;
import com.elsehere.memory.entity.UserNpcRelation;
import com.elsehere.memory.entity.UserNpcRelationId;
import com.elsehere.memory.repository.UserNpcRelationRepository;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.world.service.PronunciationAudioStore;
import jakarta.persistence.EntityManager;
import java.time.Instant;
import java.time.OffsetDateTime;
import java.util.List;
import java.util.function.BiConsumer;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

// NPC와 장소·인격 카드는 시드(WorldSeedLoader가 시작 때 넣음)를 쓰고, 대화와 관계는 대화 처리와 같은 엔티티 메서드로 만든다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  TestClockConfig.class,
  JpaAuditingConfig.class,
  WorldSeedLoader.class,
  PronunciationAudioStore.class,
  NeighborService.class
})
class NeighborServiceTest {

  private static final Instant ENDED_AT = Instant.parse("2026-11-09T05:12:00Z");

  @Autowired private NeighborService neighborService;

  @Autowired private UserNpcRelationRepository userNpcRelationRepository;

  @Autowired private EntityManager entityManager;

  private Long userId;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
  }

  @Test
  @DisplayName("[대화 종료]로 끝까지 마친 NPC는 이웃으로 열려 이름·역할·장소·직업·관심사·호감도·알아봄·대화 수·마지막 대화 시각이 온다")
  void neighbors_userEndedConversation_opensWithDetails() {
    finished(userId, "ARCADE_STAFF", ENDED_AT, Conversation::endByUser);
    userNpcRelationRepository.addAffinity(userId, npcId(entityManager, "ARCADE_STAFF"), 12);
    userNpcRelationRepository.addRecognition(userId, npcId(entityManager, "ARCADE_STAFF"), 30);

    NeighborResponse arcade = find(neighborService.neighbors(userId), "ARCADE_STAFF");

    assertThat(arcade)
        .isEqualTo(
            new NeighborResponse(
                "ARCADE_STAFF",
                true,
                "Max",
                "오락실 직원",
                "TOWN",
                "마을",
                "attendant at Little Arcade on the main street",
                List.of("video games"),
                12,
                30,
                1,
                OffsetDateTime.parse("2026-11-09T14:12:00+09:00")));
  }

  @Test
  @DisplayName("NPC 마무리와 30턴 도달로 끝난 대화도 끝까지 마친 대화로 본다")
  void neighbors_npcEndedOrTurnLimit_opens() {
    finished(userId, "CAFE_STAFF", ENDED_AT, Conversation::endByNpc);
    finished(userId, "HOTEL_STAFF", ENDED_AT, Conversation::endByTurnLimit);

    NeighborListResponse response = neighborService.neighbors(userId);

    assertThat(find(response, "CAFE_STAFF").unlocked()).isTrue();
    assertThat(find(response, "HOTEL_STAFF").unlocked()).isTrue();
  }

  @Test
  @DisplayName("무례 퇴장으로 끝났거나 끝내지 않은 대화만 있으면 잠긴 채로 둔다")
  void neighbors_rudeOrUnfinishedConversation_staysLocked() {
    finished(userId, "CAFE_STAFF", ENDED_AT, (talk, at) -> talk.countRude(1, at));
    finished(userId, "HOTEL_STAFF", ENDED_AT, (talk, at) -> {});

    NeighborListResponse response = neighborService.neighbors(userId);

    assertThat(find(response, "CAFE_STAFF").unlocked()).isFalse();
    assertThat(find(response, "HOTEL_STAFF").unlocked()).isFalse();
  }

  @Test
  @DisplayName("사용자가 한 마디도 하지 않고 끝낸 대화는 이웃으로 열지 않는다")
  void neighbors_conversationWithoutUserLine_staysLocked() {
    Conversation talk = conversation(entityManager, userId, "CAFE_STAFF");
    npcTurn(entityManager, talk, 1, "Hi! What can I get you?");
    talk.endByUser(ENDED_AT);
    entityManager.flush();

    assertThat(find(neighborService.neighbors(userId), "CAFE_STAFF").unlocked()).isFalse();
  }

  @Test
  @DisplayName("서버 대화 없이 기록되는 튜토리얼 길 안내로 만난 NPC도 이웃으로 연다")
  void neighbors_tutorialVisit_opens() {
    UserNpcRelation relation = UserNpcRelation.start(userId, npcId(entityManager, "TOWN_PASSERBY"));
    relation.recordTutorialVisit(ENDED_AT, 1, 10, 5, null);
    userNpcRelationRepository.saveAndFlush(relation);

    NeighborResponse ben = find(neighborService.neighbors(userId), "TOWN_PASSERBY");

    assertThat(ben.unlocked()).isTrue();
    assertThat(ben.name()).isEqualTo("Ben");
    assertThat(ben.recognition()).isEqualTo(10);
  }

  @Test
  @DisplayName("아직 열리지 않은 NPC도 모두 오되, 이름·직업·관심사·관계 없이 역할과 장소만 온다")
  void neighbors_lockedNpc_hasOnlyRoleAndPlace() {
    finished(userId, "ARCADE_STAFF", ENDED_AT, Conversation::endByUser);
    long npcCount =
        entityManager.createQuery("select count(npc) from Npc npc", Long.class).getSingleResult();

    NeighborListResponse response = neighborService.neighbors(userId);

    assertThat(response.totalCount()).isEqualTo(npcCount);
    assertThat(response.npcs()).hasSize((int) npcCount);
    assertThat(response.neighborCount()).isEqualTo(1);
    assertThat(find(response, "CAFE_STAFF"))
        .isEqualTo(
            new NeighborResponse(
                "CAFE_STAFF",
                false,
                null,
                "카페 직원",
                "CAFE",
                "모퉁이 카페",
                null,
                List.of(),
                null,
                null,
                null,
                null));
  }

  @Test
  @DisplayName("다른 사용자가 끝까지 마친 대화로는 열리지 않는다")
  void neighbors_otherUsersConversation_doesNotOpen() {
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid-2", "guest-123456");
    finished(otherUserId, "CAFE_STAFF", ENDED_AT, Conversation::endByUser);

    NeighborListResponse response = neighborService.neighbors(userId);

    assertThat(response.neighborCount()).isZero();
    assertThat(find(response, "CAFE_STAFF").unlocked()).isFalse();
  }

  @Test
  @DisplayName("이웃이 먼저 마지막 대화가 최근인 순서로 오고, 시각이 같으면 이름순, 잠긴 NPC는 그 뒤에 장소·역할 순이다")
  void neighbors_ordersNeighborsByLastTalkThenLockedByPlace() {
    finished(userId, "HOTEL_STAFF", ENDED_AT.minusSeconds(86_400), Conversation::endByUser);
    finished(userId, "BAKERY_STAFF", ENDED_AT, Conversation::endByUser);
    finished(userId, "ARCADE_STAFF", ENDED_AT, Conversation::endByUser);
    finished(userId, "CAFE_STAFF", ENDED_AT.plusSeconds(60), Conversation::endByUser);

    List<NeighborResponse> npcs = neighborService.neighbors(userId).npcs();

    assertThat(npcs.subList(0, 4))
        .extracting(NeighborResponse::name)
        .containsExactly("Mia", "Grace", "Max", "Jake");
    List<NeighborResponse> locked = npcs.subList(4, npcs.size());
    assertThat(locked).allMatch(npc -> !npc.unlocked());
    assertThat(locked)
        .extracting(npc -> npc.sceneName() + "/" + npc.roleLabel() + "/" + npc.npcCode())
        .isSorted();
  }

  /** 사용자가 한 마디 한 대화를 ending으로 끝내고, 대화 종료 처리처럼 방문으로 센 관계를 저장한다. 끝내지 않는 ending이면 대화는 진행 중으로 남는다. */
  private void finished(
      Long userId, String npcCode, Instant endedAt, BiConsumer<Conversation, Instant> ending) {
    Long npcId = npcId(entityManager, npcCode);
    Conversation talk = conversation(entityManager, userId, npcCode);
    textTurn(entityManager, talk, 1, "Hi there!");
    ending.accept(talk, endedAt);
    entityManager.flush();
    UserNpcRelation relation =
        userNpcRelationRepository
            .findById(new UserNpcRelationId(userId, npcId))
            .orElseGet(() -> UserNpcRelation.start(userId, npcId));
    if (!talk.isActive()) {
      relation.recordVisit(talk.getId(), endedAt, 1);
    }
    userNpcRelationRepository.saveAndFlush(relation);
  }

  private static NeighborResponse find(NeighborListResponse response, String npcCode) {
    return response.npcs().stream()
        .filter(npc -> npc.npcCode().equals(npcCode))
        .findFirst()
        .orElseThrow();
  }
}
