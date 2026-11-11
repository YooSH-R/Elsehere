package com.elsehere.conversation.repository;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import jakarta.persistence.LockModeType;
import java.time.Instant;
import java.util.Collection;
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

  /** 사용자가 목표를 모두 이룬 적이 있는 장소 연습 상황 ID. */
  @Query(
      "select distinct c.practiceScenario from Conversation c"
          + " where c.userId = :userId and c.practiceDoneAt is not null")
  List<String> findFinishedPracticeScenarios(@Param("userId") Long userId);

  /** 사용자가 한 번이라도 말을 주고받은 대화 수. 사용자 턴은 NPC 대답과 함께 저장되므로 사용자 턴이 있는 대화를 센다. */
  @Query(
      "select count(c) from Conversation c where c.userId = :userId and exists"
          + " (select t.id from Turn t where t.conversation = c"
          + " and t.speaker = com.elsehere.conversation.entity.Speaker.USER)")
  long countExchangedByUserId(@Param("userId") Long userId);

  /** 사용자가 한 마디 이상 하고 endReasons 중 하나로 끝낸 대화가 있는 NPC들. */
  @Query(
      "select distinct c.npcId from Conversation c where c.userId = :userId"
          + " and c.endReason in :endReasons and exists"
          + " (select t.id from Turn t where t.conversation = c"
          + " and t.speaker = com.elsehere.conversation.entity.Speaker.USER)")
  List<Long> findNpcIdsEndedWith(
      @Param("userId") Long userId,
      @Param("endReasons") Collection<ConversationEndReason> endReasons);

  /**
   * 지난 대화 첫 페이지(최근 시작순). 시작 시각이 같으면 나중에 만든 대화(ID가 큰 쪽)가 먼저다. {@code npcId}가 null이면 모든 NPC와의 대화,
   * {@code sceneCode}가 null이면 모든 장소의 대화다.
   */
  @Query(
      "select c from Conversation c"
          + " where c.userId = :userId and c.reviewCardId is null"
          + " and (:npcId is null or c.npcId = :npcId)"
          + " and (:sceneCode is null or c.npcId in (select n.id from Npc n where n.scene.code = :sceneCode))"
          + " order by c.startedAt desc, c.id desc")
  List<Conversation> findHistoryFirstPage(
      @Param("userId") Long userId,
      @Param("npcId") Long npcId,
      @Param("sceneCode") String sceneCode,
      Limit limit);

  /** 커서 대화({@code startedAt}, {@code id}) 다음부터의 지난 대화 한 페이지. 정렬은 첫 페이지와 같다. */
  @Query(
      "select c from Conversation c"
          + " where c.userId = :userId and c.reviewCardId is null"
          + " and (:npcId is null or c.npcId = :npcId)"
          + " and (:sceneCode is null or c.npcId in (select n.id from Npc n where n.scene.code = :sceneCode))"
          + " and (c.startedAt < :startedAt or (c.startedAt = :startedAt and c.id < :id))"
          + " order by c.startedAt desc, c.id desc")
  List<Conversation> findHistoryPageAfter(
      @Param("userId") Long userId,
      @Param("npcId") Long npcId,
      @Param("sceneCode") String sceneCode,
      @Param("startedAt") Instant startedAt,
      @Param("id") Long id,
      Limit limit);
}
