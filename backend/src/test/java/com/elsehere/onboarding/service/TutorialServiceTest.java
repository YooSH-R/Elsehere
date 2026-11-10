package com.elsehere.onboarding.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.times;
import static org.mockito.Mockito.verify;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.config.TestClockConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.mission.entity.FirstDayMission;
import com.elsehere.mission.repository.FirstDayMissionRepository;
import com.elsehere.mission.service.FirstDayMissionService;
import com.elsehere.onboarding.dto.TutorialResponse;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.OnboardingStep;
import com.elsehere.user.repository.UserRepository;
import jakarta.persistence.EntityManager;
import java.util.Optional;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;
import org.springframework.test.context.bean.override.mockito.MockitoBean;

@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  TestClockConfig.class,
  JpaAuditingConfig.class,
  TutorialService.class,
  FirstDayMissionService.class
})
class TutorialServiceTest {

  @Autowired private TutorialService tutorialService;

  @Autowired private FirstDayMissionService firstDayMissionService;

  @Autowired private UserRepository userRepository;

  @Autowired private FirstDayMissionRepository firstDayMissionRepository;

  @Autowired private EntityManager entityManager;

  @MockitoBean private TutorialRelationshipService tutorialRelationshipService;

  @Test
  @DisplayName("Ben 길 안내 완료는 튜토리얼 단계를 유지하면서 관계를 기록한다")
  void completeBenDialogue_keepsTutorialAndRecordsTheVisit() {
    Long userId = insertUserAt(OnboardingStep.TUTORIAL);

    assertThat(tutorialService.completeBenDialogue(userId).onboardingStep())
        .isEqualTo(OnboardingStep.TUTORIAL);
    verify(tutorialRelationshipService).recordBenDialogue(userId);
  }

  @Test
  @DisplayName("전체 완료 재전송은 관계를 다시 올리지 않는다")
  void finish_doesNotDuplicateBenDialogue() {
    Long userId = insertUserAt(OnboardingStep.TUTORIAL);
    tutorialService.complete(userId);
    tutorialService.complete(userId);
    verify(tutorialRelationshipService, times(1)).recordBenDialogue(userId);
  }

  @Test
  void skip_doesNotRecordBenDialogue() {
    Long userId = insertUserAt(OnboardingStep.TUTORIAL);
    tutorialService.skip(userId);
    tutorialService.completeBenDialogue(userId);
    verify(tutorialRelationshipService, never()).recordBenDialogue(userId);
  }

  @Test
  @DisplayName("튜토리얼 단계의 사용자가 튜토리얼을 마치면 초기 설정을 끝낸다(DONE)")
  void complete_inTutorial_movesToDone() {
    Long userId = insertUserAt(OnboardingStep.TUTORIAL);

    TutorialResponse response = tutorialService.complete(userId);

    assertThat(response).isEqualTo(new TutorialResponse(OnboardingStep.DONE));
    assertThat(onboardingStep(userId)).isEqualTo(OnboardingStep.DONE);
  }

  @Test
  @DisplayName("튜토리얼 전 단계(LEVEL)의 사용자가 완료하면 TUTORIAL_NOT_IN_PROGRESS 오류를 내고 단계를 바꾸지 않는다")
  void complete_beforeTutorial_throwsTutorialNotInProgress() {
    Long userId = insertUserAt(OnboardingStep.LEVEL);

    assertThatThrownBy(() -> tutorialService.complete(userId))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.TUTORIAL_NOT_IN_PROGRESS));
    assertThat(onboardingStep(userId)).isEqualTo(OnboardingStep.LEVEL);
  }

  @Test
  @DisplayName("이미 초기 설정을 끝낸(DONE) 사용자가 다시 완료하면 오류 없이 같은 결과를 돌려준다")
  void complete_alreadyDone_returnsDone() {
    Long userId = insertUserAt(OnboardingStep.DONE);

    TutorialResponse response = tutorialService.complete(userId);

    assertThat(response).isEqualTo(new TutorialResponse(OnboardingStep.DONE));
    assertThat(onboardingStep(userId)).isEqualTo(OnboardingStep.DONE);
  }

  @Test
  @DisplayName("튜토리얼 단계의 사용자가 튜토리얼을 건너뛰면 초기 설정을 끝낸다(DONE)")
  void skip_inTutorial_movesToDone() {
    Long userId = insertUserAt(OnboardingStep.TUTORIAL);

    TutorialResponse response = tutorialService.skip(userId);

    assertThat(response).isEqualTo(new TutorialResponse(OnboardingStep.DONE));
    assertThat(onboardingStep(userId)).isEqualTo(OnboardingStep.DONE);
  }

  @Test
  @DisplayName("튜토리얼 전 단계(LEVEL)의 사용자가 건너뛰면 TUTORIAL_NOT_IN_PROGRESS 오류를 내고 단계를 바꾸지 않는다")
  void skip_beforeTutorial_throwsTutorialNotInProgress() {
    Long userId = insertUserAt(OnboardingStep.LEVEL);

    assertThatThrownBy(() -> tutorialService.skip(userId))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.TUTORIAL_NOT_IN_PROGRESS));
    assertThat(onboardingStep(userId)).isEqualTo(OnboardingStep.LEVEL);
  }

  @Test
  @DisplayName("이미 초기 설정을 끝낸(DONE) 사용자가 다시 건너뛰면 오류 없이 같은 결과를 돌려준다")
  void skip_alreadyDone_returnsDone() {
    Long userId = insertUserAt(OnboardingStep.DONE);

    TutorialResponse response = tutorialService.skip(userId);

    assertThat(response).isEqualTo(new TutorialResponse(OnboardingStep.DONE));
    assertThat(onboardingStep(userId)).isEqualTo(OnboardingStep.DONE);
  }

  @Test
  @DisplayName("튜토리얼을 건너뛰면 첫 퀘스트를 처음 단계부터 시작한다")
  void skip_inTutorial_startsFirstQuest() {
    Long userId = insertUserAt(OnboardingStep.TUTORIAL);

    tutorialService.skip(userId);

    assertThat(firstQuestStage(userId)).contains(0);
  }

  @Test
  @DisplayName("튜토리얼을 마치면 첫 퀘스트를 처음 단계부터 시작한다")
  void complete_inTutorial_startsFirstQuest() {
    Long userId = insertUserAt(OnboardingStep.TUTORIAL);

    tutorialService.complete(userId);

    assertThat(firstQuestStage(userId)).contains(0);
  }

  @Test
  @DisplayName("튜토리얼 중에 이미 시작한 첫 퀘스트는 튜토리얼을 마쳐도 처음으로 돌아가지 않는다")
  void complete_withQuestAlreadyStarted_keepsItsStage() {
    Long userId = insertUserAt(OnboardingStep.TUTORIAL);
    firstDayMissionService.start(userId);
    entityManager
        .createNativeQuery("update first_day_missions set stage = 3 where user_id = ?")
        .setParameter(1, userId)
        .executeUpdate();
    entityManager.clear();

    tutorialService.complete(userId);

    assertThat(firstQuestStage(userId)).contains(3);
  }

  @Test
  @DisplayName("이미 초기 설정을 끝낸(DONE) 사용자가 다시 보내도 첫 퀘스트를 새로 시작하지 않는다")
  void skip_alreadyDone_doesNotStartFirstQuest() {
    Long userId = insertUserAt(OnboardingStep.DONE);

    tutorialService.skip(userId);

    assertThat(firstQuestStage(userId)).isEmpty();
  }

  // 사용자 테스트 데이터는 초기 설정을 마친(DONE) 상태라, 원하는 단계로 바꿔 둔다.
  private Long insertUserAt(OnboardingStep step) {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    entityManager
        .createNativeQuery("update users set onboarding_step = ? where id = ?")
        .setParameter(1, step.name())
        .setParameter(2, userId)
        .executeUpdate();
    return userId;
  }

  // 저장한 값이 DB에 반영됐는지 보려고 영속성 컨텍스트를 비운 뒤 다시 읽는다.
  private OnboardingStep onboardingStep(Long userId) {
    entityManager.flush();
    entityManager.clear();
    return userRepository.findById(userId).orElseThrow().getOnboardingStep();
  }

  private Optional<Integer> firstQuestStage(Long userId) {
    entityManager.flush();
    entityManager.clear();
    return firstDayMissionRepository.findById(userId).map(FirstDayMission::getStage);
  }
}
