package com.elsehere.ai.service;

import com.elsehere.ai.dto.AiStatus;
import com.elsehere.ai.dto.AiStatusResponse;
import com.elsehere.ai.dto.AiStatusResponse.Capabilities;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.service.GuestConversationLimit;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * AI 이용 상태(AI-01, BOOT-01의 aiStatus).
 *
 * <p>가입 사용자의 AI 사용량 한도(API 명세 1.4)는 수치가 정해지지 않아 아직 없으므로, 게스트의 대화 횟수 제한만 반영한다.
 */
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class AiStatusService {

  private final UserRepository userRepository;
  private final GuestConversationLimit guestConversationLimit;

  public AiStatusResponse status(Long userId) {
    User user =
        userRepository
            .findById(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    return statusOf(user);
  }

  public AiStatusResponse statusOf(User user) {
    if (guestConversationLimit.isReached(user)) {
      // 게스트 한도는 새 대화 시작만 막는다. 진행 중인 마지막 대화의 음성 입력과 피드백은 그대로 쓸 수 있다.
      return new AiStatusResponse(AiStatus.AI_EXHAUSTED, new Capabilities(false, true, true));
    }
    return new AiStatusResponse(AiStatus.AI_AVAILABLE, new Capabilities(true, true, true));
  }
}
