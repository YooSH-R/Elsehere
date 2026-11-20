package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.config.TestClockConfig;
import com.elsehere.mission.dto.QuestBoardResponse;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.entity.FirstDayStage;
import com.elsehere.mission.entity.ShowcaseStage;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.onboarding.service.TutorialRelationshipService;
import com.elsehere.onboarding.service.TutorialService;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.OnboardingStep;
import jakarta.persistence.EntityManager;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;
import org.springframework.test.context.bean.override.mockito.MockitoBean;

/**
 * 처음 들어온 사용자의 퀘스트 흐름을 실제 DB로 따라간다. 튜토리얼을 마치면 서버가 호텔 퀘스트를 바로 시작하는데, 그 퀘스트도 그만둘 수 있고, 하는 중에 시연을 시작해도
 * 호텔 퀘스트가 완료되거나 보상이 생기지 않는다. 시연이 덮어쓰는 NPC 관계·게임 시각(ShowcaseScene)은 이 흐름과 따로라 대역으로 둔다.
 */
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  TestClockConfig.class,
  JpaAuditingConfig.class,
  ShowcaseSettings.class,
  TutorialService.class,
  FirstDayMissionService.class,
  ShowcaseQuestService.class,
  QuestBoardService.class,
  QuestCancelService.class
})
class QuestFlowTest {
  @Autowired private TutorialService tutorial;
  @Autowired private QuestBoardService board;
  @Autowired private QuestCancelService cancels;
  @Autowired private ShowcaseQuestService showcase;
  @Autowired private FirstDayMissionService firstDay;
  @Autowired private FirstDayMissionRepository missions;
  @Autowired private EntityManager entityManager;

  @MockitoBean private TutorialRelationshipService tutorialRelationships;
  @MockitoBean private ShowcaseScene scene;

  @Test
  @DisplayName("튜토리얼 뒤 저절로 시작한 호텔 퀘스트를 하다가 그만두면 시작 전으로 돌아가고, 장소에 들어가도 다시 시작되지 않는다")
  void theHotelQuestStartedAfterTheTutorialCanBeCancelled() {
    Long userId = newUserAfterTutorial();
    assertThat(status(userId, "CHECK_IN")).isEqualTo("IN_PROGRESS");
    makeProgressInTheHotelQuest(userId);

    cancels.cancel(userId, "CHECK_IN");
    flushAndClear();

    QuestBoardResponse.Quest hotel = quest(userId, "CHECK_IN");
    assertThat(hotel.status()).isEqualTo("AVAILABLE");
    assertThat(hotel.stops()).noneMatch(QuestBoardResponse.Stop::done);
    assertThat(firstDay.visit(userId, "HOTEL").started()).isFalse();
    assertThat(missions.findById(userId)).isEmpty();
    assertThat(board.board(userId).balance()).isEqualTo(100);
  }

  @Test
  @DisplayName("호텔 퀘스트를 하는 중에 시연을 시작하면 호텔 퀘스트는 완료되지 않고 처음으로 돌아가며 보상도 생기지 않는다")
  void startingTheShowcaseDuringTheHotelQuestDoesNotCompleteIt() {
    Long userId = newUserAfterTutorial();
    makeProgressInTheHotelQuest(userId);

    showcase.start(userId);
    flushAndClear();

    assertThat(status(userId, ShowcaseStage.QUEST_ID)).isEqualTo("IN_PROGRESS");
    QuestBoardResponse.Quest hotel = quest(userId, "CHECK_IN");
    assertThat(hotel.status()).isEqualTo("LOCKED").isNotEqualTo("DONE");
    assertThat(hotel.lockReason()).contains("비 오는 저녁, 햄버거 한 끼");
    assertThat(missions.findById(userId)).isEmpty();

    cancels.cancel(userId, ShowcaseStage.QUEST_ID);
    flushAndClear();

    assertThat(status(userId, "CHECK_IN")).isEqualTo("AVAILABLE");
    assertThat(status(userId, ShowcaseStage.QUEST_ID)).isEqualTo("AVAILABLE");
    // 호텔 퀘스트 보상(10코인)이 더해지지 않았다.
    assertThat(board.board(userId).balance()).isEqualTo(100);
  }

  private Long newUserAfterTutorial() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-flow");
    entityManager
        .createNativeQuery("update users set onboarding_step = ? where id = ?")
        .setParameter(1, OnboardingStep.TUTORIAL.name())
        .setParameter(2, userId)
        .executeUpdate();
    tutorial.skip(userId);
    flushAndClear();
    return userId;
  }

  // 호텔 퀘스트의 첫 단계를 마쳐 진행이 생긴 상태로 만든다.
  private void makeProgressInTheHotelQuest(Long userId) {
    FirstDayMission mission = missions.findById(userId).orElseThrow();
    FirstDayStage first = FirstDayStage.values()[0];
    mission.accept(0, first.npc(), first.intentIds());
    flushAndClear();
    assertThat(missions.findById(userId).orElseThrow().getStage()).isEqualTo(1);
  }

  private String status(Long userId, String questId) {
    return quest(userId, questId).status();
  }

  private QuestBoardResponse.Quest quest(Long userId, String questId) {
    return board.board(userId).quests().stream()
        .filter(quest -> quest.id().equals(questId))
        .findFirst()
        .orElseThrow();
  }

  private void flushAndClear() {
    entityManager.flush();
    entityManager.clear();
  }
}
