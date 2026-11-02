package com.elsehere.conversation.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.ConversationListResponse;
import com.elsehere.conversation.dto.ConversationSummaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.repository.TurnRepository.TurnCount;
import com.elsehere.feedback.entity.ConversationFeedback;
import com.elsehere.feedback.entity.FeedbackStatus;
import com.elsehere.feedback.repository.ConversationFeedbackRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.function.Function;
import java.util.stream.Collectors;
import lombok.RequiredArgsConstructor;
import org.springframework.data.domain.Limit;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * 지난 대화 목록(CONV-09). 저장된 대화만 읽으므로 AI 사용량이 소진돼도 쓸 수 있다.
 *
 * <p>최근 시작순이며, 커서는 이전 페이지 마지막 대화의 ID다(단어장 VOCAB-01과 같은 방식).
 */
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class ConversationHistoryService {

  private static final int MAX_SIZE = 50;

  private final ConversationRepository conversationRepository;
  private final TurnRepository turnRepository;
  private final ConversationFeedbackRepository conversationFeedbackRepository;
  private final NpcRepository npcRepository;
  private final ConversationSupport support;

  /**
   * @param npcCode 이 NPC와 나눈 대화만 본다. null이면 모든 NPC다. 없는 코드면 빈 목록이다.
   * @param cursor 이전 페이지의 nextCursor. 첫 페이지면 null
   */
  public ConversationListResponse list(Long userId, String npcCode, String cursor, int size) {
    if (size < 1 || size > MAX_SIZE) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }
    Long npcId = null;
    if (npcCode != null) {
      Optional<Npc> npc = npcRepository.findByCode(npcCode);
      if (npc.isEmpty()) {
        return new ConversationListResponse(List.of(), null);
      }
      npcId = npc.get().getId();
    }

    // 다음 페이지가 있는지 알기 위해 한 개를 더 가져온다.
    Limit limit = Limit.of(size + 1);
    List<Conversation> fetched =
        cursor == null
            ? conversationRepository.findHistoryFirstPage(userId, npcId, limit)
            : pageAfterCursor(userId, npcId, cursor, limit);
    boolean hasNext = fetched.size() > size;
    List<Conversation> page = hasNext ? fetched.subList(0, size) : fetched;
    String nextCursor = hasNext ? String.valueOf(page.getLast().getId()) : null;
    return new ConversationListResponse(summaries(page), nextCursor);
  }

  private List<Conversation> pageAfterCursor(Long userId, Long npcId, String cursor, Limit limit) {
    Conversation last = findCursorConversation(userId, cursor);
    return conversationRepository.findHistoryPageAfter(
        userId, npcId, last.getStartedAt(), last.getId(), limit);
  }

  /** 숫자가 아니거나, 없거나, 다른 사용자의 대화를 가리키는 커서는 잘못된 요청이다. */
  private Conversation findCursorConversation(Long userId, String cursor) {
    long conversationId;
    try {
      conversationId = Long.parseLong(cursor);
    } catch (NumberFormatException exception) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED, exception);
    }
    return conversationRepository
        .findByIdAndUserId(conversationId, userId)
        .orElseThrow(() -> new BusinessException(ErrorCode.VALIDATION_FAILED));
  }

  private List<ConversationSummaryResponse> summaries(List<Conversation> page) {
    List<Long> ids = page.stream().map(Conversation::getId).toList();
    Map<Long, Long> userTurnCounts =
        turnRepository.countBySpeaker(ids, Speaker.USER).stream()
            .collect(Collectors.toMap(TurnCount::getConversationId, TurnCount::getTurnCount));
    Map<Long, FeedbackStatus> feedbackStatuses =
        conversationFeedbackRepository.findAllById(ids).stream()
            .collect(
                Collectors.toMap(
                    ConversationFeedback::getConversationId, ConversationFeedback::getStatus));
    // 한 페이지에 같은 NPC가 여러 번 나오므로 NPC는 한 번씩만 읽는다.
    Map<Long, NpcContext> npcs = new HashMap<>();
    Function<Long, NpcContext> npcOf = npcId -> npcs.computeIfAbsent(npcId, support::npcById);
    return page.stream()
        .map(
            conversation -> {
              NpcContext npc = npcOf.apply(conversation.getNpcId());
              return new ConversationSummaryResponse(
                  conversation.getId(),
                  npc.summary(),
                  npc.sceneCode(),
                  conversation.getStatus(),
                  conversation.getEndReason(),
                  ConversationSupport.toOffset(conversation.getStartedAt()),
                  ConversationSupport.toOffset(conversation.getEndedAt()),
                  userTurnCounts.getOrDefault(conversation.getId(), 0L).intValue(),
                  feedbackStatuses.get(conversation.getId()));
            })
        .toList();
  }
}
