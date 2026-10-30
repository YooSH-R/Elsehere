package com.elsehere.world.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.ai.service.AiStatusService;
import com.elsehere.auth.service.ConsentCatalog;
import com.elsehere.bootstrap.service.BootstrapService;
import com.elsehere.common.config.ClockConfig;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.conversation.service.GuestConversationLimit;
import com.elsehere.conversation.service.TrainingRecordingService;
import com.elsehere.conversation.service.TrainingRecordingStore;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.service.OptionalConsentService;
import com.elsehere.world.dto.LastSceneResponse;
import jakarta.persistence.EntityManager;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

// 장소는 시드(WorldSeedLoader가 시작 때 넣음)를 쓰고, 저장 결과는 BOOT-01의 재접속 장소로 확인한다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  ClockConfig.class,
  JpaAuditingConfig.class,
  WorldSeedLoader.class,
  PronunciationAudioStore.class,
  LastSceneService.class,
  BootstrapService.class,
  AiStatusService.class,
  GuestConversationLimit.class,
  OptionalConsentService.class,
  ConsentCatalog.class,
  TrainingRecordingService.class,
  TrainingRecordingStore.class
})
class LastSceneServiceTest {

  @Autowired private LastSceneService lastSceneService;

  @Autowired private BootstrapService bootstrapService;

  @Autowired private EntityManager entityManager;

  @Test
  @DisplayName("현재 장소를 저장하면 저장한 장소 코드를 돌려주고, 재접속할 때 그 장소에서 시작한다")
  void saveLastScene_bootstrapStartsThere() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    LastSceneResponse response = lastSceneService.saveLastScene(userId, "CAFE");

    assertThat(response).isEqualTo(new LastSceneResponse("CAFE"));
    assertThat(lastSceneCode(userId)).isEqualTo("CAFE");
  }

  @Test
  @DisplayName("없는 장소 코드면 NOT_FOUND 오류를 내고 마지막 장소를 바꾸지 않는다")
  void saveLastScene_unknownScene_throwsNotFound() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    lastSceneService.saveLastScene(userId, "CAFE");

    assertThatThrownBy(() -> lastSceneService.saveLastScene(userId, "MOON"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.NOT_FOUND));
    assertThat(lastSceneCode(userId)).isEqualTo("CAFE");
  }

  @Test
  @DisplayName("열리지 않은 장소면 SCENE_CLOSED 오류를 내고 마지막 장소를 바꾸지 않는다")
  void saveLastScene_closedScene_throwsSceneClosed() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    lastSceneService.saveLastScene(userId, "CAFE");
    // MVP 시드에는 닫힌 장소가 없어 테스트에서 만든다.
    entityManager
        .createNativeQuery(
            "insert into scenes (code, name, unity_key, is_open)"
                + " values ('LIBRARY', '도서관', 'LibraryScene', false)")
        .executeUpdate();

    assertThatThrownBy(() -> lastSceneService.saveLastScene(userId, "LIBRARY"))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.SCENE_CLOSED));
    assertThat(lastSceneCode(userId)).isEqualTo("CAFE");
  }

  // 저장한 값이 DB에 반영됐는지 보려고 영속성 컨텍스트를 비운 뒤 BOOT-01로 다시 읽는다.
  private String lastSceneCode(Long userId) {
    entityManager.flush();
    entityManager.clear();
    return bootstrapService.findSignedIn(userId).orElseThrow().lastSceneCode();
  }
}
