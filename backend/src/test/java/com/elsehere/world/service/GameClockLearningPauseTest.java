package com.elsehere.world.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.user.repository.UserRepository;
import com.elsehere.world.entity.UserGameClock;
import com.elsehere.world.repository.UserGameClockRepository;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.Optional;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.BeanUtils;

class GameClockLearningPauseTest {
  private static final Instant START = Instant.parse("2026-11-04T00:00:00Z");
  private final UserGameClock gameClock = BeanUtils.instantiateClass(UserGameClock.class);
  private final UserGameClockRepository repository = mock(UserGameClockRepository.class);
  private final ConversationRepository conversations = mock(ConversationRepository.class);
  private final UserRepository users = mock(UserRepository.class);
  private final GameClockService service =
      new GameClockService(
          repository, conversations, users, Clock.fixed(START.plusSeconds(30), ZoneOffset.UTC));

  @BeforeEach
  void startClock() {
    gameClock.beat(START, false, GameClockService.MAX_BEAT_GAP);
    when(repository.findForUpdate(8L)).thenReturn(Optional.of(gameClock));
  }

  @Test
  void refreshWithAnUnfinishedConversationResumesWithoutEndingIt() {
    var conversation =
        com.elsehere.conversation.entity.Conversation.start(
            8L, 1L, com.elsehere.conversation.entity.InitiatedBy.USER, 1, START.minusSeconds(10));
    when(conversations.findFirstByUserIdAndStatus(
            8L, com.elsehere.conversation.entity.ConversationStatus.ACTIVE))
        .thenReturn(Optional.of(conversation));

    var refreshed = service.beat(8L, false, false, 0L);

    assertThat(refreshed.paused()).isFalse();
    assertThat(gameClock.getPlaySeconds()).isZero();
    assertThat(conversation.isActive()).isTrue();

    var later =
        new GameClockService(
            repository, conversations, users, Clock.fixed(START.plusSeconds(60), ZoneOffset.UTC));
    var response = later.beat(8L, false, false, 30L);

    assertThat(response.paused()).isFalse();
    assertThat(gameClock.getPlaySeconds()).isEqualTo(30);
    assertThat(conversation.isActive()).isTrue();
  }

  @Test
  void reopeningAnUnfinishedConversationPreservesTimeBeforeOpening() {
    var conversation =
        com.elsehere.conversation.entity.Conversation.start(
            8L, 1L, com.elsehere.conversation.entity.InitiatedBy.USER, 1, START.minusSeconds(10));
    when(conversations.findFirstByUserIdAndStatus(
            8L, com.elsehere.conversation.entity.ConversationStatus.ACTIVE))
        .thenReturn(Optional.of(conversation));

    var response = service.beat(8L, true, false, 25L);

    assertThat(response.paused()).isTrue();
    assertThat(gameClock.getPlaySeconds()).isEqualTo(25);
    assertThat(conversation.isActive()).isTrue();
  }

  @Test
  void closingWithActiveSecondsCountsOnlyTimeAfterClosing() {
    var response = service.beat(8L, false, true, 5L);
    assertThat(gameClock.getPlaySeconds()).isEqualTo(5);
    assertThat(response.paused()).isFalse();
  }

  @Test
  void activeSecondsNeverIncludeTimeAfterTheServerConversationStarted() {
    var conversation = mock(com.elsehere.conversation.entity.Conversation.class);
    when(conversation.getStartedAt()).thenReturn(START.plusSeconds(10));
    when(conversations.findFirstByUserIdAndStatus(
            8L, com.elsehere.conversation.entity.ConversationStatus.ACTIVE))
        .thenReturn(Optional.of(conversation));
    var response = service.beat(8L, true, false, 25L);
    assertThat(gameClock.getPlaySeconds()).isEqualTo(10);
    assertThat(response.paused()).isTrue();
  }

  @Test
  void activeSecondsCannotAdvanceTheTutorialClock() {
    var user = mock(com.elsehere.user.entity.User.class);
    when(user.getOnboardingStep()).thenReturn(com.elsehere.user.entity.OnboardingStep.TUTORIAL);
    when(users.findById(8L)).thenReturn(Optional.of(user));
    service.beat(8L, false, false, 25L);
    assertThat(gameClock.getPlaySeconds()).isZero();
  }

  @Test
  void openingAWindowPreservesTheActiveSecondsBeforeItOpened() {
    var response = service.beat(8L, true, false, 25L);
    assertThat(gameClock.getPlaySeconds()).isEqualTo(25);
    assertThat(response.paused()).isTrue();
  }

  @Test
  void reportedActiveSecondsCannotExceedTheServerInterval() {
    service.beat(8L, false, false, 10000L);
    assertThat(gameClock.getPlaySeconds()).isEqualTo(30);
  }

  @Test
  void learningWindowOpenDoesNotAccumulateTimeAndReportsPaused() {
    var response = service.beat(8L, true, false);

    assertThat(gameClock.getPlaySeconds()).isZero();
    assertThat(response.paused()).isTrue();
    assertThat(gameClock.getLastBeatAt()).isEqualTo(START.plusSeconds(30));
  }

  @Test
  void closingLearningWindowDiscardsThePausedIntervalAndResumes() {
    var response = service.beat(8L, false, true);

    assertThat(gameClock.getPlaySeconds()).isZero();
    assertThat(response.paused()).isFalse();
  }
}
