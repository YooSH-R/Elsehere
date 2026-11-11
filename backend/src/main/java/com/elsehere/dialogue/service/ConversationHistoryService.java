package com.elsehere.dialogue.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.repository.TurnRepository.TurnCount;
import com.elsehere.dialogue.dto.ConversationListResponse;
import com.elsehere.dialogue.dto.ConversationSummaryResponse;
import com.elsehere.feedback.entity.ConversationFeedback;
import com.elsehere.feedback.entity.FeedbackStatus;
import com.elsehere.feedback.repository.ConversationFeedbackRepository;
import com.elsehere.review.entity.ReviewCard;
import com.elsehere.review.repository.ReviewCardRepository;
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
  private static final int MAX_SCENE_CODE_LENGTH = 30;

  private final ConversationRepository conversationRepository;
  private final TurnRepository turnRepository;
  private final ConversationFeedbackRepository conversationFeedbackRepository;
  private final NpcRepository npcRepository;
  private final ReviewCardRepository reviewCardRepository;
  private final DialogueContexts dialogueContexts;

  /**
   * @param npcCode 이 NPC와 나눈 대화만 본다. null이면 모든 NPC다. 없는 코드면 빈 목록이다.
   * @param cursor 이전 페이지의 nextCursor. 첫 페이지면 null
   */
  public ConversationListResponse list(Long userId, String npcCode, String cursor, int size) {
    return list(userId, npcCode, null, cursor, size, false);
  }

  /**
   * 대화 노트는 완료된 피드백이 있는 대화만 조회하고, 일반 대화 기록은 기존 조건을 유지한다.
   *
   * @param sceneCode 이 장소의 NPC와 나눈 대화만 본다. null이면 모든 장소다. 없는 코드면 빈 목록이다.
   */
  public ConversationListResponse list(
      Long userId,
      String npcCode,
      String sceneCode,
      String cursor,
      int size,
      boolean feedbackOnly) {
    if (size < 1
        || size > MAX_SIZE
        || (sceneCode != null
            && (sceneCode.isBlank() || sceneCode.length() > MAX_SCENE_CODE_LENGTH))) {
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
            ? firstPage(userId, npcId, sceneCode, limit, feedbackOnly)
            : pageAfterCursor(userId, npcId, sceneCode, cursor, limit, feedbackOnly);
    boolean hasNext = fetched.size() > size;
    List<Conversation> page = hasNext ? fetched.subList(0, size) : fetched;
    String nextCursor = hasNext ? String.valueOf(page.getLast().getId()) : null;
    return new ConversationListResponse(summaries(userId, page), nextCursor);
  }

  private List<Conversation> firstPage(
      Long userId, Long npcId, String sceneCode, Limit limit, boolean feedbackOnly) {
    if (feedbackOnly) {
      return conversationFeedbackRepository.findCompletedFirstPage(userId, npcId, sceneCode, limit);
    }
    return conversationRepository.findHistoryFirstPage(userId, npcId, sceneCode, limit);
  }

  private List<Conversation> pageAfterCursor(
      Long userId, Long npcId, String sceneCode, String cursor, Limit limit, boolean feedbackOnly) {
    Conversation last = findCursorConversation(userId, cursor);
    if (feedbackOnly) {
      return conversationFeedbackRepository.findCompletedPageAfter(
          userId, npcId, sceneCode, last.getStartedAt(), last.getId(), limit);
    }
    return conversationRepository.findHistoryPageAfter(
        userId, npcId, sceneCode, last.getStartedAt(), last.getId(), limit);
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

  private List<ConversationSummaryResponse> summaries(Long userId, List<Conversation> page) {
    List<Long> ids = page.stream().map(Conversation::getId).toList();
    // 대화마다 [다시 연습]으로 만든 복습 카드는 하나뿐이다. 카드가 없으면 연습하지 않은 대화다.
    Map<Long, Integer> practiceCounts =
        reviewCardRepository.findByUserIdAndSourceConversationIdIn(userId, ids).stream()
            .collect(
                Collectors.toMap(
                    ReviewCard::getSourceConversationId, ReviewCard::getPracticeCount));
    Map<Long, Long> userTurnCounts =
        turnRepository.countBySpeaker(ids, Speaker.USER).stream()
            .collect(Collectors.toMap(TurnCount::getConversationId, TurnCount::getTurnCount));
    Map<Long, ConversationFeedback> feedbacks =
        conversationFeedbackRepository.findAllById(ids).stream()
            .collect(
                Collectors.toMap(ConversationFeedback::getConversationId, Function.identity()));
    // 한 페이지에 같은 NPC가 여러 번 나오므로 NPC는 한 번씩만 읽는다.
    Map<Long, NpcContext> npcs = new HashMap<>();
    Function<Long, NpcContext> npcOf =
        npcId -> npcs.computeIfAbsent(npcId, dialogueContexts::npcById);
    return page.stream()
        .map(
            conversation -> {
              NpcContext npc = npcOf.apply(conversation.getNpcId());
              ConversationFeedback feedback = feedbacks.get(conversation.getId());
              return new ConversationSummaryResponse(
                  conversation.getId(),
                  npc.summary(),
                  npc.sceneCode(),
                  conversation.getStatus(),
                  conversation.getEndReason(),
                  ConversationResponses.toOffset(conversation.getStartedAt()),
                  ConversationResponses.toOffset(conversation.getEndedAt()),
                  userTurnCounts.getOrDefault(conversation.getId(), 0L).intValue(),
                  feedback == null ? null : feedback.getStatus(),
                  feedback != null
                      && feedback.getStatus() == FeedbackStatus.DONE
                      && feedback.getReadAt() == null,
                  practiceCounts.getOrDefault(conversation.getId(), 0));
            })
        .toList();
  }
}
