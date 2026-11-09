package com.elsehere.ai.service;

import com.elsehere.ai.dto.AiStatus;
import com.elsehere.common.config.ClockConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.user.entity.User;
import java.time.Clock;
import java.time.LocalDate;
import java.util.List;
import java.util.Map;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

/**
 * NPC 대화 시작 횟수 제한. 게스트는 계정마다 정해진 횟수까지, 가입 사용자는 한국 시간 하루에 정해진 횟수까지 대화를 시작한다.
 *
 * <p>게스트는 한 번이라도 말을 주고받은 대화만 센다. 말을 걸었다가 한 마디도 주고받지 않고 끝낸 대화는 체험으로 치지 않는다. 게스트는 로그아웃하면 같은 계정으로 다시
 * 들어올 수 없으므로 횟수를 다시 채우지 않는다.
 */
@Slf4j
@Component
public class GuestConversationLimit {

  // 화면 19의 "계속 쓸 수 있어요" 목록(API 명세 1.5)
  private static final List<String> ALLOWED_FEATURES =
      List.of("WORLD_EXPLORE", "OBJECT_CARD", "VOCABULARY", "SAVED_RECORDS");

  private final ConversationRepository conversationRepository;
  private final int guestLimit;
  private final int dailyLimit;
  private final Clock clock;

  public GuestConversationLimit(
      ConversationRepository conversationRepository,
      @Value("${elsehere.guest.conversation-limit:3}") int guestLimit,
      @Value("${elsehere.conversation.daily-limit:30}") int dailyLimit,
      Clock clock) {
    this.conversationRepository = conversationRepository;
    this.guestLimit = guestLimit;
    this.dailyLimit = dailyLimit;
    this.clock = clock;
  }

  /** 게스트 한 계정이 할 수 있는 NPC 대화 수(서버 설정 GUEST_CONVERSATION_LIMIT). */
  public int guestLimit() {
    return guestLimit;
  }

  /** 정해진 횟수를 다 썼으면 새 대화를 거절한다. 오류 코드는 AI 사용량 소진과 같고, 무엇 때문에 막혔는지는 문구로 알린다. */
  public void check(User user) {
    if (!isReached(user)) {
      return;
    }
    Map<String, Object> details =
        Map.of("aiStatus", AiStatus.AI_EXHAUSTED.name(), "allowedFeatures", ALLOWED_FEATURES);
    if (user.isGuest()) {
      log.info("guest_conversation_limit_reached user_id={} limit={}", user.getId(), guestLimit);
      throw new BusinessException(
          ErrorCode.AI_QUOTA_EXHAUSTED,
          "게스트는 NPC와 " + guestLimit + "번까지 대화할 수 있어요. 마을 탐험, 사물 카드, 단어장은 계속할 수 있어요.",
          details);
    }
    log.info("daily_conversation_limit_reached user_id={} limit={}", user.getId(), dailyLimit);
    throw new BusinessException(
        ErrorCode.AI_QUOTA_EXHAUSTED,
        "오늘의 NPC 대화 " + dailyLimit + "회를 모두 사용했어요. 내일 다시 대화할 수 있어요.",
        details);
  }

  /** 더 시작할 수 있는 대화 수. 게스트는 남은 전체 횟수, 가입 사용자는 오늘 남은 횟수다. 끝난 대화도 쓴 것으로 센다. */
  public int remaining(User user) {
    long used = user.isGuest() ? usedByGuest(user) : usedToday(user);
    int limit = user.isGuest() ? guestLimit : dailyLimit;
    return (int) Math.max(0, limit - used);
  }

  /** 정해진 횟수를 다 썼는지. */
  public boolean isReached(User user) {
    return remaining(user) == 0;
  }

  private long usedByGuest(User user) {
    return conversationRepository.countExchangedByUserId(user.getId());
  }

  private long usedToday(User user) {
    LocalDate today = LocalDate.now(clock.withZone(ClockConfig.SERVICE_ZONE));
    return conversationRepository.countByUserIdAndStartedAtGreaterThanEqualAndStartedAtLessThan(
        user.getId(),
        today.atStartOfDay(ClockConfig.SERVICE_ZONE).toInstant(),
        today.plusDays(1).atStartOfDay(ClockConfig.SERVICE_ZONE).toInstant());
  }
}
