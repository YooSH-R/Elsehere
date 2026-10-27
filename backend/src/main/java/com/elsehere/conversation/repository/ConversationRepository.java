package com.elsehere.conversation.repository;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationStatus;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;

public interface ConversationRepository extends JpaRepository<Conversation, Long> {

  Optional<Conversation> findFirstByUserIdAndStatus(Long userId, ConversationStatus status);

  Optional<Conversation> findByIdAndUserId(Long id, Long userId);
}
