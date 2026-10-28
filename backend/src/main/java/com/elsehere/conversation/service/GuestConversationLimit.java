package com.elsehere.conversation.service;

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

  /** 게스트가 이미 정해진 횟수만큼 대화를 시작했으면 거절한다. 끝난 대화도 센다. */
  public void check(User user) {
    if (user.getProvider() != AuthProvider.GUEST) {
      return;
    }
    if (conversationRepository.countByUserId(user.getId()) >= limit) {
      throw new BusinessException(
          ErrorCode.AI_QUOTA_EXHAUSTED,
          Map.of("aiStatus", "AI_EXHAUSTED", "allowedFeatures", ALLOWED_FEATURES));
    }
  }
}
