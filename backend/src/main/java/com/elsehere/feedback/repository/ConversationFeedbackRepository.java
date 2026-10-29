package com.elsehere.feedback.repository;

import com.elsehere.feedback.entity.ConversationFeedback;
import org.springframework.data.jpa.repository.JpaRepository;

public interface ConversationFeedbackRepository extends JpaRepository<ConversationFeedback, Long> {}
