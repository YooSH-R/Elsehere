package com.elsehere.dialogue.service;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.dialogue.dto.EndConversationRequest;
import com.elsehere.dialogue.dto.EndConversationResponse;
import com.elsehere.feedback.dto.FeedbackResponse;
import com.elsehere.feedback.service.FeedbackService;
import com.elsehere.followup.dto.FollowUpResponse;
import com.elsehere.followup.service.FollowUpService;
import com.elsehere.memory.service.MemoryService;
import com.elsehere.review.service.ReviewPracticeResults;
import java.time.Clock;
import java.time.Instant;
import java.util.List;
import java.util.Optional;
import java.util.concurrent.CompletableFuture;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

/**
 * 대화 종료(CONV-08). 종료를 먼저 저장한 뒤 이 대화로 이어지는 일(기억 카드 갱신, 건넬 것 판정, 피드백)을 처리한다.
 *
 * <p>종료는 한 트랜잭션에서 확정하고, AI를 부르는 기억·건넬 것·피드백은 그 뒤 트랜잭션 밖에서 한다. 이 일들이 실패해도 대화는 끝난 상태로 남는다.
 */
@Slf4j
@Service
@RequiredArgsConstructor
public class DialogueEndService {

  private final TurnRepository turnRepository;
  private final ConversationLookup conversationLookup;
  private final DialogueContexts dialogueContexts;
  private final FeedbackService feedbackService;
  private final MemoryService memoryService;
  private final FollowUpService followUpService;
  private final ReviewPracticeResults reviewPracticeResults;
  private final TransactionTemplate transactionTemplate;
  private final Clock clock;

  /**
   * 대화를 끝내고 짧은 피드백을 만든다. NPC가 이미 끝낸 대화는 상태를 바꾸지 않고 피드백만 만든다. 피드백 생성은 대화 종료를 확정한 뒤 트랜잭션 밖에서 한다. NPC가
   * 곧 건네기로 한 것도 함께 판정해 담는다.
   *
   * <p>장소 연습 대화는 피드백만 만든다. 이야기·관계와 따로 하는 연습이라 기억 카드와 건넬 것을 만들지 않는다.
   */
  public EndConversationResponse end(
      Long userId, Long conversationId, EndConversationRequest request) {
    Ending ending = endConversation(userId, conversationId);
    EndConversationResponse ended = ending.response();
    if (ended.reviewCardId() != null) {
      return ended;
    }
    CompletableFuture<Optional<FollowUpResponse>> followUp =
        CompletableFuture.completedFuture(Optional.empty());
    if (!ending.practice()) {
      // 기억 카드 갱신은 따로 실행되어 피드백 생성과 함께 진행된다. 실패해도 종료·피드백에 영향이 없다.
      memoryService.updateAfterConversation(userId, conversationId, request.requestId());
      // 건넬 것 판정도 따로 실행해 피드백을 만드는 동안 끝낸다. 실패하면 없는 것으로 본다.
      followUp = followUpService.afterConversation(userId, conversationId, request.requestId());
    }
    FeedbackResponse feedback =
        feedbackService.feedbackOnEnd(
            userId, conversationId, request.requestId(), request.skipFeedback());
    return ended
        .withFeedback(feedback)
        .withDetailedFeedback(
            feedback.status() == com.elsehere.feedback.entity.FeedbackStatus.DONE
                ? feedbackService.detailed(userId, conversationId).items()
                : List.of())
        .withFollowUp(followUp.join().orElse(null));
  }

  private Ending endConversation(Long userId, Long conversationId) {
    return transactionTemplate.execute(
        status -> {
          Conversation conversation =
              conversationLookup.ownedConversationForUpdate(userId, conversationId);
          boolean endingNow = conversation.isActive();
          Instant now = clock.instant();
          conversation.endByUser(now);
          NpcContext npc = dialogueContexts.npcById(conversation.getNpcId());
          long userTurnCount =
              turnRepository.countByConversationIdAndSpeaker(conversationId, Speaker.USER);
          // NPC가 이미 끝낸 대화(TurnService가 남김)나 다시 보낸 종료 요청은 새로 남기지 않는다.
          if (endingNow) {
            log.info(
                "conversation_ended conversation_id={} status={} end_reason={} user_turns={}",
                conversationId,
                conversation.getStatus(),
                conversation.getEndReason(),
                userTurnCount);
          }
          EndConversationResponse response =
              new EndConversationResponse(
                      conversation.getId(),
                      conversation.getStatus(),
                      conversation.getEndReason(),
                      ConversationResponses.toOffset(conversation.getEndedAt()),
                      npc.summary(),
                      npc.sceneCode(),
                      userTurnCount,
                      null,
                      List.of(),
                      null,
                      conversation.getReviewCardId())
                  .withReviewResult(
                      conversation.isReview()
                          ? reviewPracticeResults.record(userId, conversation)
                          : null);
          return new Ending(response, conversation.isPractice());
        });
  }

  /**
   * @param practice 장소 연습 대화인지. 종료 뒤 기억 카드와 건넬 것을 만들지 않는다
   */
  private record Ending(EndConversationResponse response, boolean practice) {}
}
