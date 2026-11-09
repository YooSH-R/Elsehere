package com.elsehere.conversation.repository;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationStatus;
import jakarta.persistence.LockModeType;
import java.time.Instant;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.domain.Limit;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface ConversationRepository extends JpaRepository<Conversation, Long> {

  Optional<Conversation> findFirstByUserIdAndStatus(Long userId, ConversationStatus status);

  Optional<Conversation> findByIdAndUserId(Long id, Long userId);

  @Lock(LockModeType.PESSIMISTIC_WRITE)
  @Query("select c from Conversation c where c.id = :id and c.userId = :userId")
  Optional<Conversation> findByIdAndUserIdForUpdate(
      @Param("id") Long id, @Param("userId") Long userId);

  Optional<Conversation> findByRequestId(UUID requestId);

  boolean existsByReviewCardIdAndStatus(Long reviewCardId, ConversationStatus status);

  @Query(
      "select c from Conversation c where c.userId = :userId and c.reviewCardId = :cardId"
          + " and c.reviewResult is not null order by c.endedAt desc, c.id desc")
  List<Conversation> findReviewResults(
      @Param("userId") Long userId, @Param("cardId") Long cardId, Limit limit);

  /** 사용자가 한 번이라도 말을 주고받은 대화 수. 사용자 턴은 NPC 대답과 함께 저장되므로 사용자 턴이 있는 대화를 센다. */
  @Query(
      "select count(c) from Conversation c where c.userId = :userId and exists"
          + " (select t.id from Turn t where t.conversation = c"
          + " and t.speaker = com.elsehere.conversation.entity.Speaker.USER)")
  long countExchangedByUserId(@Param("userId") Long userId);

  long countByUserIdAndStartedAtGreaterThanEqualAndStartedAtLessThan(
      Long userId, Instant start, Instant end);

  /**
   * 지난 대화 첫 페이지(최근 시작순). 시작 시각이 같으면 나중에 만든 대화(ID가 큰 쪽)가 먼저다. {@code npcId}가 null이면 모든 NPC와의 대화다.
   */
  @Query(
      "select c from Conversation c"
          + " where c.userId = :userId and c.reviewCardId is null"
          + " and (:npcId is null or c.npcId = :npcId)"
          + " order by c.startedAt desc, c.id desc")
  List<Conversation> findHistoryFirstPage(
      @Param("userId") Long userId, @Param("npcId") Long npcId, Limit limit);

  /** 커서 대화({@code startedAt}, {@code id}) 다음부터의 지난 대화 한 페이지. 정렬은 첫 페이지와 같다. */
  @Query(
      "select c from Conversation c"
          + " where c.userId = :userId and c.reviewCardId is null"
          + " and (:npcId is null or c.npcId = :npcId)"
          + " and (c.startedAt < :startedAt or (c.startedAt = :startedAt and c.id < :id))"
          + " order by c.startedAt desc, c.id desc")
  List<Conversation> findHistoryPageAfter(
      @Param("userId") Long userId,
      @Param("npcId") Long npcId,
      @Param("startedAt") Instant startedAt,
      @Param("id") Long id,
      Limit limit);
}
