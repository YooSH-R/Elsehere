package com.elsehere.conversation.repository;

import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface TurnRepository extends JpaRepository<Turn, Long> {

  List<Turn> findAllByConversationIdOrderBySeqAsc(Long conversationId);

  Optional<Turn> findByRequestId(UUID requestId);

  Optional<Turn> findByConversationIdAndSeq(Long conversationId, int seq);

  long countByConversationIdAndSpeaker(Long conversationId, Speaker speaker);

  @Query("select coalesce(max(t.seq), 0) from Turn t where t.conversation.id = :conversationId")
  int findLastSeq(@Param("conversationId") Long conversationId);
}
