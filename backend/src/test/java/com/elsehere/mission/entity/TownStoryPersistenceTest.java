package com.elsehere.mission.entity;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.config.TestClockConfig;
import jakarta.persistence.EntityManager;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.context.annotation.Import;

@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
@Import({TestcontainersConfiguration.class, TestClockConfig.class, JpaAuditingConfig.class})
class TownStoryPersistenceTest {
  @Autowired private EntityManager entityManager;

  @Test
  void partiallyCompletedGoalsSurviveReconnect() {
    TownStory story = TownStory.start(8L);
    story.accept(0, "DINER_STAFF", List.of("order_breakfast"));
    entityManager.persist(story);
    entityManager.flush();
    entityManager.clear();

    TownStory restored = entityManager.find(TownStory.class, 8L);
    restored.accept(0, "DINER_STAFF", List.of("explain_spill"));
    assertThat(restored.getStage()).isEqualTo(1);
  }
}
