package com.elsehere.user.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.ai.service.AiStatusService;
import com.elsehere.ai.service.GuestConversationLimit;
import com.elsehere.auth.service.ConsentCatalog;
import com.elsehere.bootstrap.service.BootstrapService;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.config.TestClockConfig;
import com.elsehere.conversation.service.TrainingRecordingService;
import com.elsehere.conversation.service.TrainingRecordingStore;
import com.elsehere.user.UserTestData;
import com.elsehere.user.dto.UiThemeResponse;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.UiTheme;
import jakarta.persistence.EntityManager;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

// 저장 결과는 다른 기기에서 접속할 때처럼 BOOT-01로 다시 읽어 확인한다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  TestClockConfig.class,
  JpaAuditingConfig.class,
  UiThemeService.class,
  BootstrapService.class,
  com.elsehere.mission.service.ShowcaseQuestService.class,
  com.elsehere.mission.service.ShowcaseSettings.class,
  AiStatusService.class,
  GuestConversationLimit.class,
  OptionalConsentService.class,
  ConsentCatalog.class,
  TrainingRecordingService.class,
  TrainingRecordingStore.class
})
class UiThemeServiceTest {

  @Autowired private UiThemeService uiThemeService;

  @Autowired private BootstrapService bootstrapService;

  @Autowired private EntityManager entityManager;

  @Test
  @DisplayName("화면 테마를 바꾸면 바꾼 테마를 돌려주고, 다시 접속할 때 그 테마가 내려간다")
  void changeUiTheme_bootstrapReturnsChosenTheme() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    UiThemeResponse response = uiThemeService.changeUiTheme(userId, UiTheme.STICKER);

    assertThat(response).isEqualTo(new UiThemeResponse(UiTheme.STICKER));
    assertThat(uiThemeOnNextVisit(userId)).isEqualTo(UiTheme.STICKER);
  }

  // 저장한 값이 DB에 반영됐는지 보려고 영속성 컨텍스트를 비운 뒤 BOOT-01로 다시 읽는다.
  private UiTheme uiThemeOnNextVisit(Long userId) {
    entityManager.flush();
    entityManager.clear();
    return bootstrapService.findSignedIn(userId).orElseThrow().user().uiTheme();
  }
}
