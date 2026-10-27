package com.elsehere.feedback.repository;

import com.elsehere.feedback.entity.TurnFeedback;
import java.util.Collection;
import java.util.List;
import org.springframework.data.jpa.repository.JpaRepository;

public interface TurnFeedbackRepository extends JpaRepository<TurnFeedback, Long> {

  List<TurnFeedback> findAllByTurnIdInOrderByTurnIdAsc(Collection<Long> turnIds);

  void deleteAllByTurnIdIn(Collection<Long> turnIds);
}
