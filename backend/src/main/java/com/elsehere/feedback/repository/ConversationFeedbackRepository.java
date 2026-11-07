package com.elsehere.feedback.repository;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.feedback.entity.ConversationFeedback;
import java.time.Instant;
import java.util.List;
import org.springframework.data.domain.Limit;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface ConversationFeedbackRepository extends JpaRepository<ConversationFeedback, Long> {

  @Query(
      "select count(f) from ConversationFeedback f join Conversation c on c.id = f.conversationId"
          + " where c.userId = :userId and f.status = com.elsehere.feedback.entity.FeedbackStatus.DONE"
          + " and f.readAt is null")
  long countUnread(@Param("userId") Long userId);

  @Modifying
  @Query(
      "update ConversationFeedback f set f.readAt = :readAt where f.conversationId = :conversationId"
          + " and f.status = com.elsehere.feedback.entity.FeedbackStatus.DONE and f.readAt is null")
  int markRead(@Param("conversationId") Long conversationId, @Param("readAt") Instant readAt);

  @Query(
      "select c from Conversation c join ConversationFeedback f on f.conversationId = c.id"
          + " where c.userId = :userId and f.status = com.elsehere.feedback.entity.FeedbackStatus.DONE"
          + " and (:npcId is null or c.npcId = :npcId) order by c.startedAt desc, c.id desc")
  List<Conversation> findCompletedFirstPage(
      @Param("userId") Long userId, @Param("npcId") Long npcId, Limit limit);

  @Query(
      "select c from Conversation c join ConversationFeedback f on f.conversationId = c.id"
          + " where c.userId = :userId and f.status = com.elsehere.feedback.entity.FeedbackStatus.DONE"
          + " and (:npcId is null or c.npcId = :npcId)"
          + " and (c.startedAt < :startedAt or (c.startedAt = :startedAt and c.id < :id))"
          + " order by c.startedAt desc, c.id desc")
  List<Conversation> findCompletedPageAfter(
      @Param("userId") Long userId,
      @Param("npcId") Long npcId,
      @Param("startedAt") Instant startedAt,
      @Param("id") Long id,
      Limit limit);
}
