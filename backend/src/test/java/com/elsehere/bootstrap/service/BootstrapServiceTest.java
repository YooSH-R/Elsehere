package com.elsehere.bootstrap.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.when;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.ai.dto.AiStatus;
import com.elsehere.ai.service.AiStatusService;
import com.elsehere.ai.service.GuestConversationLimit;
import com.elsehere.auth.service.ConsentCatalog;
import com.elsehere.bootstrap.dto.AuthState;
import com.elsehere.bootstrap.dto.BootstrapResponse;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.config.TestClockConfig;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.InputType;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.service.TrainingRecordingService;
import com.elsehere.conversation.service.TrainingRecordingStore;
import com.elsehere.mission.dto.FirstDayMissionResponse;
import com.elsehere.mission.service.ShowcaseQuestService;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.AvatarType;
import com.elsehere.user.entity.OnboardingStep;
import com.elsehere.user.entity.UiTheme;
import com.elsehere.user.entity.User;
import com.elsehere.user.service.OptionalConsentService;
import jakarta.persistence.EntityManager;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;
import org.springframework.test.context.bean.override.mockito.MockitoBean;

@DataJpaTest(
    properties = {
      "spring.jpa.hibernate.ddl-auto=create-drop",
      "elsehere.guest.conversation-limit=1"
    })
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  TestClockConfig.class,
  JpaAuditingConfig.class,
  BootstrapService.class,
  AiStatusService.class,
  GuestConversationLimit.class,
  OptionalConsentService.class,
  ConsentCatalog.class,
  TrainingRecordingService.class,
  TrainingRecordingStore.class
})
class BootstrapServiceTest {

  @Autowired private EntityManager entityManager;

  @Autowired private BootstrapService bootstrapService;

  @MockitoBean private ShowcaseQuestService showcase;

  @Test
  @DisplayName("가입한 사용자면 로그인 상태와 사용자 정보, 초기 설정 단계를 돌려준다")
  void signedInUser_returnsUserAndOnboardingStep() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    BootstrapResponse response = bootstrapService.findSignedIn(userId).orElseThrow();

    assertThat(response.authState()).isEqualTo(AuthState.SIGNED_IN);
    assertThat(response.user().userId()).isEqualTo(userId);
    assertThat(response.user().nickname()).isEqualTo("Sun");
    assertThat(response.user().avatarType()).isEqualTo(AvatarType.F);
    assertThat(response.user().currentLanguage()).isEqualTo("en-US");
    assertThat(response.user().onboardingStep()).isEqualTo(OnboardingStep.DONE);
  }

  @Test
  @DisplayName("팀 계정으로 표시하면 그 사용자만 팀 계정이 된다. 없는 사용자면 아무것도 하지 않는다")
  void markTeamAccount_marksOnlyThatUser() {
    Long teamUser =
        UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    Long otherUser = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    bootstrapService.markTeamAccount(teamUser);
    bootstrapService.markTeamAccount(-1L);
    entityManager.flush();
    entityManager.clear();

    assertThat(entityManager.find(User.class, teamUser).isTeamAccount()).isTrue();
    assertThat(entityManager.find(User.class, otherUser).isTeamAccount()).isFalse();
  }

  @Test
  @DisplayName("지금 버전의 음성 처리에 동의한 사용자면 음성 입력을 쓸 수 있다고 알린다")
  void userWithVoiceConsent_returnsVoiceConsent() {
    Long agreedUserId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid-1", "guest-000001");
    entityManager
        .createNativeQuery(
            "insert into user_consents (user_id, consent_type, version, agreed_at)"
                + " values (?, 'VOICE', '2026-10', now())")
        .setParameter(1, agreedUserId)
        .executeUpdate();

    assertThat(bootstrapService.findSignedIn(agreedUserId).orElseThrow().user().voiceConsent())
        .isTrue();
    assertThat(bootstrapService.findSignedIn(otherUserId).orElseThrow().user().voiceConsent())
        .isFalse();
  }

  @Test
  @DisplayName("화면 테마를 고른 적 없는 사용자면 기본 테마 JELLY를 돌려준다")
  void userWithoutChosenTheme_returnsJellyTheme() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    BootstrapResponse response = bootstrapService.findSignedIn(userId).orElseThrow();

    assertThat(response.user().uiTheme()).isEqualTo(UiTheme.JELLY);
  }

  @Test
  @DisplayName("마지막으로 머문 장소가 있으면 재접속할 장소 코드를 돌려준다")
  void userWithLastScene_returnsLastSceneCode() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    setLastScene(userId, "CAFE");

    BootstrapResponse response = bootstrapService.findSignedIn(userId).orElseThrow();

    assertThat(response.lastSceneCode()).isEqualTo("CAFE");
  }

  @Test
  @DisplayName("시연 서버에서는 시연을 하는 중이 아니면 마지막 장소와 상관없이 호텔에서 시작한다")
  void onTheShowcaseServer_startsInTheHotelUnlessTheDemoIsRunning() {
    when(showcase.isShowcaseServer()).thenReturn(true);
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    setLastScene(userId, "CAFE");

    assertThat(bootstrapService.findSignedIn(userId).orElseThrow().lastSceneCode())
        .isEqualTo("HOTEL");

    when(showcase.activeStatus(userId))
        .thenReturn(
            Optional.of(
                new FirstDayMissionResponse(
                    true,
                    1,
                    "SHOWCASE_RAINY_EVENING",
                    false,
                    "TOWN",
                    false,
                    true,
                    "RAIN",
                    100,
                    null,
                    "",
                    "",
                    List.of())));
    assertThat(bootstrapService.findSignedIn(userId).orElseThrow().lastSceneCode())
        .isEqualTo("CAFE");
  }

  @Test
  @DisplayName("AI 이용 상태를 AI-01과 같은 형식으로 함께 돌려준다")
  void signedInUser_returnsAiStatus() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid-1");
    Conversation conversation =
        Conversation.start(userId, 1L, InitiatedBy.NPC, 1, TestClockConfig.NOW);
    entityManager.persist(conversation);
    // 게스트 횟수는 한 번이라도 주고받은 대화만 센다.
    entityManager.persist(
        Turn.user(
            conversation,
            1,
            UUID.randomUUID(),
            InputType.TEXT,
            "Hi",
            null,
            null,
            false,
            null,
            null));
    entityManager.persist(Turn.npc(conversation, 2, "Hello!", null, TestClockConfig.NOW));

    BootstrapResponse response = bootstrapService.findSignedIn(userId).orElseThrow();

    assertThat(response.aiStatus()).isNotNull();
    assertThat(response.aiStatus().status()).isEqualTo(AiStatus.AI_EXHAUSTED);
    assertThat(response.aiStatus().capabilities().canStartConversation()).isFalse();
  }

  @Test
  @DisplayName("세션의 사용자가 DB에 없으면(탈퇴, 없는 ID로 개발용 로그인) 빈 값을 돌려준다")
  void missingUser_returnsEmpty() {
    assertThat(bootstrapService.findSignedIn(999L)).isEmpty();
  }

  // 장소 이동 저장(WORLD-05) 전이라 SQL로 장소를 만들고 마지막 장소로 지정한다.
  private void setLastScene(Long userId, String sceneCode) {
    entityManager
        .createNativeQuery("insert into scenes (code, name, unity_key) values (?, ?, ?)")
        .setParameter(1, sceneCode)
        .setParameter(2, sceneCode)
        .setParameter(3, sceneCode.toLowerCase())
        .executeUpdate();
    entityManager
        .createNativeQuery(
            "update users set last_scene_id = (select id from scenes where code = ?) where id = ?")
        .setParameter(1, sceneCode)
        .setParameter(2, userId)
        .executeUpdate();
  }
}
