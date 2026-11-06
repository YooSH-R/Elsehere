package com.elsehere.followup.repository;

import com.elsehere.followup.entity.NpcFollowUp;
import java.util.List;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;

public interface NpcFollowUpRepository extends JpaRepository<NpcFollowUp, Long> {

  Optional<NpcFollowUp> findByConversationId(Long conversationId);

  Optional<NpcFollowUp> findByDeliveredConversationId(Long conversationId);

  Optional<NpcFollowUp> findFirstByUserIdAndNpcIdAndDeliveredAtIsNull(Long userId, Long npcId);

  List<NpcFollowUp> findAllByUserIdAndDeliveredAtIsNullOrderByReadyAtAsc(Long userId);

  void deleteAllByUserIdAndNpcIdAndDeliveredAtIsNull(Long userId, Long npcId);
}
