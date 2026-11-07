package com.elsehere.onboarding.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.Mockito.verify;

import com.elsehere.memory.entity.UserNpcRelation;
import com.elsehere.memory.repository.UserNpcRelationRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import com.elsehere.world.service.GameClockService;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.Optional;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

@ExtendWith(MockitoExtension.class)
class TutorialRelationshipServiceTest {

  @Mock private NpcRepository npcRepository;
  @Mock private UserNpcRelationRepository relationRepository;
  @Mock private GameClockService gameClockService;
  @Mock private Npc ben;
  private TutorialRelationshipService service;
  private static final Instant NOW = Instant.parse("2026-11-06T03:00:00Z");

  @BeforeEach
  void setUp() {
    service =
        new TutorialRelationshipService(
            npcRepository,
            relationRepository,
            gameClockService,
            Clock.fixed(NOW, ZoneOffset.UTC),
            20,
            6);
    given(npcRepository.findByCode("TOWN_PASSERBY")).willReturn(Optional.of(ben));
    given(ben.getId()).willReturn(7L);
    given(gameClockService.currentDay(1L)).willReturn(1);
  }

  @Test
  void firstDialogue_recordsRecognitionAffinityAndTheHelpMemory() {
    given(relationRepository.findById(any())).willReturn(Optional.empty());

    service.recordBenDialogue(1L);

    var saved = ArgumentCaptor.forClass(UserNpcRelation.class);
    verify(relationRepository).save(saved.capture());
    UserNpcRelation relation = saved.getValue();
    assertThat(relation.getRecognition()).isEqualTo(20);
    assertThat(relation.getAffinity()).isEqualTo(6);
    assertThat(relation.getTalkCount()).isEqualTo(1);
    assertThat(relation.getLastTalkedAt()).isEqualTo(NOW);
    assertThat(relation.getLastTalkGameDay()).isEqualTo(1);
    assertThat(relation.getMemoryCard()).contains("cafe");
    assertThat(relation.getMemoryCard()).contains("Introduced themselves");
    assertThat(relation.getMemoryConversationId()).isNull();
  }

  @Test
  void repeatedRequest_keepsOnlyOneVisitAndTheNextNormalConversationStillCounts() {
    UserNpcRelation relation = UserNpcRelation.start(1L, 7L);
    given(relationRepository.findById(any())).willReturn(Optional.of(relation));

    service.recordBenDialogue(1L);
    service.recordBenDialogue(1L);

    verify(relationRepository).save(relation);
    assertThat(relation.getTalkCount()).isEqualTo(1);
    assertThat(relation.getRecognition()).isEqualTo(20);
    assertThat(relation.getAffinity()).isEqualTo(6);
    assertThat(relation.recordVisit(100L, NOW, 1)).isTrue();
    assertThat(relation.getTalkCount()).isEqualTo(2);
  }
}
