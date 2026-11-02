package com.elsehere.conversation.repository;

import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import java.time.Instant;
import java.util.Collection;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface TurnRepository extends JpaRepository<Turn, Long> {

  /**
   * 모든 사용자가 함께 쓰는 NPC 말걸기 음성 키. NPC 음성 보관 정리에서 지우지 않는다. 대화 기록 패키지가 world 패키지를 import하지 않도록 npcs
   * 테이블을 직접 읽는다.
   */
  @Query(
      value = "select approach_audio_key from npcs where approach_audio_key is not null",
      nativeQuery = true)
  List<String> findNpcApproachAudioKeys();

  List<Turn> findAllByConversationIdOrderBySeqAsc(Long conversationId);

  Optional<Turn> findByRequestId(UUID requestId);

  Optional<Turn> findByConversationIdAndSeq(Long conversationId, int seq);

  long countByConversationIdAndSpeaker(Long conversationId, Speaker speaker);

  @Query("select coalesce(max(t.seq), 0) from Turn t where t.conversation.id = :conversationId")
  int findLastSeq(@Param("conversationId") Long conversationId);

  List<Turn> findAllByNpcAudioKeyIn(Collection<String> npcAudioKeys);

  @Query(
      "select t from Turn t where t.npcAudioKey is not null"
          + " and (coalesce(t.npcAudioCreatedAt, t.createdAt) <= :createdAt or t.npcAudioDeletedAt is not null)")
  List<Turn> findExpiredNpcAudio(@Param("createdAt") Instant createdAt);

  @Query("select t.npcAudioKey from Turn t where t.npcAudioKey is not null")
  List<String> findAllNpcAudioKeys();

  /** 대화마다 한 화자가 보낸 턴 수. 턴이 없는 대화는 결과에 없다. */
  @Query(
      "select t.conversation.id as conversationId, count(t) as turnCount from Turn t"
          + " where t.conversation.id in :conversationIds and t.speaker = :speaker"
          + " group by t.conversation.id")
  List<TurnCount> countBySpeaker(
      @Param("conversationIds") Collection<Long> conversationIds,
      @Param("speaker") Speaker speaker);

  interface TurnCount {
    Long getConversationId();

    long getTurnCount();
  }
}
