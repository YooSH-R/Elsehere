package com.elsehere.feedback.repository;

import com.elsehere.feedback.entity.TurnFeedback;
import java.util.Collection;
import java.util.List;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;

public interface TurnFeedbackRepository extends JpaRepository<TurnFeedback, Long> {

  List<TurnFeedback> findAllByTurnIdInOrderByTurnIdAsc(Collection<Long> turnIds);

  void deleteAllByTurnIdIn(Collection<Long> turnIds);

  /** 처음 읽은 키(expectedKey)가 그대로일 때만 모범 음성 키를 붙인다. 붙였으면 1이고, 그 사이 다른 요청이 먼저 붙였거나 피드백이 지워졌으면 0이다. */
  @Modifying(flushAutomatically = true, clearAutomatically = true)
  @Query(
      "update TurnFeedback f set f.modelAudioKey = :key where f.id = :id"
          + " and (f.modelAudioKey = :expectedKey"
          + " or (f.modelAudioKey is null and :expectedKey is null))")
  int attachModelAudio(Long id, String expectedKey, String key);
}
