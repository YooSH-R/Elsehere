package com.elsehere.mission.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.config.TestClockConfig;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.context.annotation.Import;

@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  TestClockConfig.class,
  JpaAuditingConfig.class,
  FirstDayMissionService.class
})
class FirstDayMissionPersistenceTest {
  @Autowired private FirstDayMissionService service;
  @Autowired private EntityManager entityManager;

  @Test
  void reconnectRestoresGoalsAndOtherUserDoesNotShareProgress() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "quest-user");
    Long other =
        UserTestData.insertUser(
            entityManager, AuthProvider.GOOGLE, "quest-other", "OtherQuestUser");
    service.start(userId);
    service.accept(
        userId,
        0,
        "HOTEL_STAFF",
        new JudgeResult.Intent("store_suitcase", List.of("store_suitcase"), null));
    service.visit(userId, "HOTEL");
    entityManager.flush();
    entityManager.clear();
    assertThat(service.start(userId).steps().getFirst().goals().get(1).completed()).isTrue();
    assertThat(service.status(userId).scene()).isEqualTo("HOTEL");
    assertThat(service.status(other).started()).isFalse();
  }
}
