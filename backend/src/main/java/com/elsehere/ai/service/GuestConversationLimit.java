package com.elsehere.ai.service;

import com.elsehere.ai.dto.AiStatus;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.user.entity.User;
import java.util.List;
import java.util.Map;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

/**
 * 게스트의 NPC 대화 횟수 제한. AI 사용량을 아끼려고 게스트는 계정마다 정해진 횟수까지만 대화한다.
 *
 * <p>한 번이라도 말을 주고받은 대화만 센다. 말을 걸었다가 한 마디도 주고받지 않고 끝낸 대화는 체험으로 치지 않는다. 게스트는 로그아웃하면 같은 계정으로 다시 들어올 수
 * 없으므로 횟수를 다시 채우지 않는다. 가입 사용자는 대화 횟수 제한이 없다. 대화 하나의 길이는 발화 30턴({@code Conversation.TURN_LIMIT})으로
 * 따로 제한한다.
 */
@Slf4j
@Component
public class GuestConversationLimit {

  // 화면 19의 "계속 쓸 수 있어요" 목록(API 명세 1.5)
  private static final List<String> ALLOWED_FEATURES =
      List.of("WORLD_EXPLORE", "OBJECT_CARD", "VOCABULARY", "SAVED_RECORDS");

  private final ConversationRepository conversationRepository;
  private final int limit;

  public GuestConversationLimit(
      ConversationRepository conversationRepository,
      @Value("${elsehere.guest.conversation-limit:3}") int limit) {
    this.conversationRepository = conversationRepository;
    this.limit = limit;
  }

  /** 게스트 한 계정이 할 수 있는 NPC 대화 수(서버 설정 GUEST_CONVERSATION_LIMIT). */
  public int limit() {
    return limit;
  }

  /** 게스트가 이미 정해진 횟수만큼 대화했으면 거절한다. 오류 코드는 AI 사용량 소진과 같지만, 게스트 횟수 때문에 막혔다고 문구로 알린다. */
  public void check(User user) {
    if (isReached(user)) {
      log.info("guest_conversation_limit_reached user_id={} limit={}", user.getId(), limit);
      throw new BusinessException(
          ErrorCode.AI_QUOTA_EXHAUSTED,
          "게스트는 NPC와 " + limit + "번까지 대화할 수 있어요. 마을 탐험, 사물 카드, 단어장은 계속할 수 있어요.",
          Map.of("aiStatus", AiStatus.AI_EXHAUSTED.name(), "allowedFeatures", ALLOWED_FEATURES));
    }
  }

  /** 게스트가 더 시작할 수 있는 대화 수. 끝난 대화도 쓴 것으로 센다. 가입 사용자는 횟수 제한이 없어 null이다. */
  public Integer remaining(User user) {
    if (!user.isGuest()) {
      return null;
    }
    long used = conversationRepository.countExchangedByUserId(user.getId());
    return (int) Math.max(0, limit - used);
  }

  /** 게스트가 정해진 횟수만큼 대화했는지. 가입 사용자는 항상 아니다. */
  public boolean isReached(User user) {
    Integer remaining = remaining(user);
    return remaining != null && remaining == 0;
  }
}
