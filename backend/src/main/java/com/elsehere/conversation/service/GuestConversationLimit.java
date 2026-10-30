package com.elsehere.conversation.service;

import com.elsehere.ai.dto.AiStatus;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.User;
import java.util.List;
import java.util.Map;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

/**
 * 게스트의 NPC 대화 횟수 제한. AI 사용량을 아끼려고 게스트는 계정마다 정해진 횟수까지만 대화를 시작한다.
 *
 * <p>게스트는 로그아웃하면 같은 계정으로 다시 들어올 수 없으므로 횟수를 다시 채우지 않는다. 가입 사용자의 AI 사용량 한도(API 명세 1.4)는 아직 없다.
 */
@Component
public class GuestConversationLimit {

  // 화면 19의 "계속 쓸 수 있어요" 목록(API 명세 1.5)
  private static final List<String> ALLOWED_FEATURES =
      List.of("WORLD_EXPLORE", "OBJECT_CARD", "VOCABULARY", "SAVED_RECORDS");

  private final ConversationRepository conversationRepository;
  private final int limit;

  public GuestConversationLimit(
      ConversationRepository conversationRepository,
      @Value("${elsehere.guest.conversation-limit}") int limit) {
    this.conversationRepository = conversationRepository;
    this.limit = limit;
  }

  /** 게스트가 이미 정해진 횟수만큼 대화를 시작했으면 거절한다. 오류 코드는 AI 사용량 소진과 같지만, 사용량이 아니라 게스트 횟수 때문에 막혔다고 문구로 알린다. */
  public void check(User user) {
    if (isReached(user)) {
      throw new BusinessException(
          ErrorCode.AI_QUOTA_EXHAUSTED,
          "게스트는 NPC와 " + limit + "번까지 대화할 수 있어요. 마을 탐험, 사물 카드, 단어장은 계속할 수 있어요.",
          Map.of("aiStatus", AiStatus.AI_EXHAUSTED.name(), "allowedFeatures", ALLOWED_FEATURES));
    }
  }

  /** 게스트가 더 시작할 수 있는 대화 수. 끝난 대화도 쓴 것으로 센다. 가입 사용자는 횟수 제한이 없어 null이다. */
  public Integer remaining(User user) {
    if (user.getProvider() != AuthProvider.GUEST) {
      return null;
    }
    long started = conversationRepository.countByUserId(user.getId());
    return (int) Math.max(0, limit - started);
  }

  /** 게스트가 정해진 횟수만큼 대화를 시작했는지. 가입 사용자는 항상 아니다. */
  public boolean isReached(User user) {
    Integer remaining = remaining(user);
    return remaining != null && remaining == 0;
  }
}
