package com.elsehere.dialogue.service;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.world.repository.NpcRepository;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

@Service
@RequiredArgsConstructor
public class PerformanceIntentService {
  private final ConversationLookup conversations;
  private final NpcRepository npcs;
  private final AiGateway gateway;
  private final TransactionTemplate transaction;

  public String classify(Long userId, Long conversationId, UUID requestId, String text) {
    // 소유권·대화 상태만 읽는다. 이 경로에서는 턴 저장, 대사 생성, 음성 합성을 하지 않는다.
    transaction.execute(
        status -> {
          Conversation conversation = conversations.ownedConversation(userId, conversationId);
          if (!conversation.isActive()) {
            throw new BusinessException(ErrorCode.CONVERSATION_NOT_ACTIVE);
          }
          if (conversation.isReview()) {
            throw new BusinessException(ErrorCode.REVIEW_IN_PROGRESS);
          }
          var npc =
              npcs.findById(conversation.getNpcId())
                  .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
          if (!"TOWN_JOHN_PARK".equals(npc.getCode())) {
            throw new BusinessException(ErrorCode.VALIDATION_FAILED);
          }
          return null;
        });
    return gateway
        .performanceIntent(new WorkerTrace(requestId, userId, conversationId), text)
        .intent();
  }
}
