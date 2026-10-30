package com.elsehere.bootstrap.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.ai.dto.AiStatus;
import com.elsehere.ai.service.AiStatusService;
import com.elsehere.auth.service.ConsentCatalog;
import com.elsehere.bootstrap.dto.AuthState;
import com.elsehere.bootstrap.dto.BootstrapResponse;
import com.elsehere.common.config.ClockConfig;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.service.GuestConversationLimit;
import com.elsehere.conversation.service.TrainingRecordingService;
import com.elsehere.conversation.service.TrainingRecordingStore;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.AvatarType;
import com.elsehere.user.entity.OnboardingStep;
import com.elsehere.user.entity.UiTheme;
import com.elsehere.user.service.OptionalConsentService;
import jakarta.persistence.EntityManager;
import java.time.Instant;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

@DataJpaTest(
    properties = {
      "spring.jpa.hibernate.ddl-auto=create-drop",
      "elsehere.guest.conversation-limit=1"
    })
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  ClockConfig.class,
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
  @DisplayName("AI 이용 상태를 AI-01과 같은 형식으로 함께 돌려준다")
  void signedInUser_returnsAiStatus() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid-1");
    entityManager.persist(
        Conversation.start(userId, 1L, InitiatedBy.NPC, 1, Instant.parse("2026-10-28T01:00:00Z")));

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
