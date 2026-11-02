package com.elsehere.conversation.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;

/**
 * 사용자의 대화·턴을 읽는다. 대화 흐름(dialogue)과 피드백·단어장처럼 대화 기록을 읽는 도메인이 함께 쓴다.
 *
 * <p>다른 사용자의 대화·턴은 존재 여부를 드러내지 않도록 없는 것과 같이 NOT_FOUND로 응답한다. 엔티티를 돌려주므로 호출하는 쪽의 트랜잭션 안에서 부른다.
 */
@Component
@RequiredArgsConstructor
public class ConversationLookup {

  private final ConversationRepository conversationRepository;
  private final TurnRepository turnRepository;

  public Conversation ownedConversation(Long userId, Long conversationId) {
    return conversationRepository
        .findByIdAndUserId(conversationId, userId)
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  public Turn ownedTurn(Long userId, Long turnId) {
    return turnRepository
        .findById(turnId)
        .filter(turn -> turn.getConversation().getUserId().equals(userId))
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  /** NPC가 말한 턴만 돌려준다. 사용자 턴도 NOT_FOUND다. */
  public Turn ownedNpcTurn(Long userId, Long turnId) {
    Turn turn = ownedTurn(userId, turnId);
    if (turn.getSpeaker() != Speaker.NPC) {
      throw new BusinessException(ErrorCode.NOT_FOUND);
    }
    return turn;
  }
}
