package com.elsehere.conversation.repository;

import static com.elsehere.conversation.DialogueTestData.STARTED_AT;
import static com.elsehere.conversation.DialogueTestData.conversation;
import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.config.TestClockConfig;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.world.service.PronunciationAudioStore;
import jakarta.persistence.EntityManager;
import java.util.List;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;
import org.springframework.data.domain.Limit;

// NPC 근무지 묶음은 시드(world.sql)를 쓴다. CAFE_STAFF·CAFE_BARISTA는 카페(음식), TOWN_PASSERBY는 거리(일상)다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  TestClockConfig.class,
  JpaAuditingConfig.class,
  WorldSeedLoader.class,
  PronunciationAudioStore.class
})
class ConversationRepositoryTest {

  @Autowired private ConversationRepository conversationRepository;

  @Autowired private EntityManager entityManager;

  private Long userId;
  private Conversation cafeAtWork;
  private Conversation cafeOffDuty;
  private Conversation street;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    cafeAtWork = conversation(entityManager, userId, "CAFE_STAFF");
    cafeOffDuty = conversation(entityManager, userId, "CAFE_BARISTA");
    cafeOffDuty.markNpcOffDuty();
    street = conversation(entityManager, userId, "TOWN_PASSERBY");
    entityManager.flush();
  }

  @Test
  @DisplayName("장소 묶음으로 거르면 근무 중 대화는 NPC 근무지 묶음, 근무 밖 대화는 일상으로 센다")
  void findHistory_placeGroup_usesWorkplaceAndOffDutyIsDaily() {
    assertThat(firstPage("FOOD")).containsExactly(cafeAtWork);
    assertThat(firstPage("DAILY")).containsExactly(street, cafeOffDuty);
    assertThat(firstPage(null)).containsExactly(street, cafeOffDuty, cafeAtWork);
  }

  @Test
  @DisplayName("다음 페이지도 같은 장소 묶음으로 거른다")
  void findHistoryPageAfter_placeGroup_keepsTheFilter() {
    List<Conversation> page =
        conversationRepository.findHistoryPageAfter(
            userId, null, null, "DAILY", STARTED_AT, street.getId(), Limit.of(10));

    assertThat(page).containsExactly(cafeOffDuty);
  }

  private List<Conversation> firstPage(String placeGroup) {
    return conversationRepository.findHistoryFirstPage(
        userId, null, null, placeGroup, Limit.of(10));
  }
}
