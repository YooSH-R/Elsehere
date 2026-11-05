package com.elsehere.world.service;

import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.user.entity.OnboardingStep;
import com.elsehere.user.repository.UserRepository;
import com.elsehere.world.dto.GameClockResponse;
import com.elsehere.world.entity.UserGameClock;
import com.elsehere.world.repository.UserGameClockRepository;
import java.time.Clock;
import java.time.Duration;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * 사용자별 게임 시계. 게임 시간은 사용자가 플레이한 동안만 흐른다. Unity가 플레이 중 30초마다 신호를 보내면 지난 신호 뒤 흐른 실제 시간을 한 번에 60초까지
 * 더하고, 진행 중인 대화가 있거나 튜토리얼을 마치기 전이면 멈춘다. 실제 10분이 게임 하루다({@link GameTime}).
 *
 * <p>클라이언트가 실제 진행 초를 보내면 학습 화면을 열기 전까지의 시간을 보존하고, 서버 신호 간격·상한·대화 시작 시각으로 제한한다. 진행 초가 없는 이전 클라이언트는
 * 현재 멈춤 상태로 구간 전체를 판정한다.
 */
@Service
@RequiredArgsConstructor
public class GameClockService {

  // Unity 신호 간격(30초)의 두 배. 창을 닫았거나 백그라운드로 두어 신호가 끊긴 시간은 세지 않는다.
  static final Duration MAX_BEAT_GAP = Duration.ofSeconds(60);

  private final UserGameClockRepository userGameClockRepository;
  private final ConversationRepository conversationRepository;
  private final UserRepository userRepository;
  private final Clock clock;

  @Transactional(readOnly = true)
  public GameClockResponse now(Long userId) {
    return timeOf(userId).response(paused(userId));
  }

  /** 플레이 중 신호. 지난 신호 뒤 흐른 시간을 더하고 지금 게임 시각을 돌려준다. */
  @Transactional
  public GameClockResponse beat(Long userId) {
    return beat(userId, false, false);
  }

  /** 학습 창을 닫는 첫 신호도 창을 보던 구간은 제외하고, 그다음 구간부터 다시 센다. */
  @Transactional
  public GameClockResponse beat(
      Long userId, boolean interactionPaused, boolean interactionWasPaused) {
    return beat(userId, interactionPaused, interactionWasPaused, null);
  }

  /** 열기 직전까지 진행한 초를 보존하며, 대화 시작 이후와 튜토리얼 구간은 서버에서도 제한한다. */
  @Transactional
  public GameClockResponse beat(
      Long userId, boolean interactionPaused, boolean interactionWasPaused, Long activeSeconds) {
    userGameClockRepository.insertIfAbsent(userId);
    UserGameClock gameClock = userGameClockRepository.findForUpdate(userId).orElseThrow();
    boolean tutorial = beforeTutorialDone(userId);
    var conversation =
        conversationRepository.findFirstByUserIdAndStatus(userId, ConversationStatus.ACTIVE);
    boolean paused = tutorial || conversation.isPresent() || interactionPaused;
    Duration allowed = MAX_BEAT_GAP;
    if (activeSeconds != null && conversation.isPresent() && gameClock.getLastBeatAt() != null) {
      long beforeDialogue =
          Duration.between(gameClock.getLastBeatAt(), conversation.get().getStartedAt())
              .getSeconds();
      allowed = Duration.ofSeconds(Math.clamp(beforeDialogue, 0, MAX_BEAT_GAP.getSeconds()));
    }
    boolean skipInterval = activeSeconds == null ? paused || interactionWasPaused : tutorial;
    gameClock.beat(clock.instant(), skipInterval, allowed, activeSeconds);
    return GameTime.ofPlaySeconds(gameClock.getPlaySeconds()).response(paused);
  }

  /** 지금 게임 날짜. 호출하는 쪽의 트랜잭션 안에서 부른다. */
  public int currentDay(Long userId) {
    return timeOf(userId).day();
  }

  private GameTime timeOf(Long userId) {
    return GameTime.ofPlaySeconds(
        userGameClockRepository.findById(userId).map(UserGameClock::getPlaySeconds).orElse(0L));
  }

  private boolean paused(Long userId) {
    return beforeTutorialDone(userId)
        || conversationRepository
            .findFirstByUserIdAndStatus(userId, ConversationStatus.ACTIVE)
            .isPresent();
  }

  // 튜토리얼은 정해진 대본으로 연습하는 시간이라, 마치거나 건너뛰어 온보딩이 끝나기 전에는 게임 시간을 세지 않는다.
  private boolean beforeTutorialDone(Long userId) {
    return userRepository
        .findById(userId)
        .map(user -> user.getOnboardingStep() != OnboardingStep.DONE)
        .orElse(false);
  }
}
