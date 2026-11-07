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

/** 모든 계정의 한국 시간 기준 일일 NPC 대화 시작 횟수 제한. 기존 빈 이름은 API 호환을 위해 유지한다. */
@Slf4j
@Component
public class GuestConversationLimit {

  // 화면 19의 "계속 쓸 수 있어요" 목록(API 명세 1.5)
  private static final List<String> ALLOWED_FEATURES =
      List.of("WORLD_EXPLORE", "OBJECT_CARD", "VOCABULARY", "SAVED_RECORDS");

  private final ConversationRepository conversationRepository;
  private final int limit;
  private final Clock clock;

  public GuestConversationLimit(
      ConversationRepository conversationRepository,
      @Value("${elsehere.conversation.daily-limit:30}") int limit,
      Clock clock) {
    this.conversationRepository = conversationRepository;
    this.limit = limit;
    this.clock = clock;
  }

  /** 한 계정이 하루에 시작할 수 있는 NPC 대화 수. */
  public int limit() {
    return limit;
  }

  /** 오늘 허용된 횟수만큼 대화를 시작했으면 거절한다. */
  public void check(User user) {
    if (isReached(user)) {
      log.info("daily_conversation_limit_reached user_id={} limit={}", user.getId(), limit);
      throw new BusinessException(
          ErrorCode.AI_QUOTA_EXHAUSTED,
          "오늘의 NPC 대화 " + limit + "회를 모두 사용했어요. 내일 다시 대화할 수 있어요.",
          Map.of("aiStatus", AiStatus.AI_EXHAUSTED.name(), "allowedFeatures", ALLOWED_FEATURES));
    }
  }

  /** 오늘 더 시작할 수 있는 대화 수. 끝난 대화도 쓴 것으로 센다. */
  public Integer remaining(User user) {
    LocalDate today = LocalDate.now(clock.withZone(ClockConfig.SERVICE_ZONE));
    long started =
        conversationRepository.countByUserIdAndStartedAtGreaterThanEqualAndStartedAtLessThan(
            user.getId(),
            today.atStartOfDay(ClockConfig.SERVICE_ZONE).toInstant(),
            today.plusDays(1).atStartOfDay(ClockConfig.SERVICE_ZONE).toInstant());
    return (int) Math.max(0, limit - started);
  }

  /** 오늘 정해진 횟수만큼 대화를 시작했는지. */
  public boolean isReached(User user) {
    Integer remaining = remaining(user);
    return remaining != null && remaining == 0;
  }
}
