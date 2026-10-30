package com.elsehere.conversation.repository;

import com.elsehere.conversation.entity.TrainingRecording;
import java.util.List;
import org.springframework.data.jpa.repository.JpaRepository;

public interface TrainingRecordingRepository extends JpaRepository<TrainingRecording, Long> {

  List<TrainingRecording> findByUserId(Long userId);
}
