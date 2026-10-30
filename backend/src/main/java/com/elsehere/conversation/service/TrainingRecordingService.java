package com.elsehere.conversation.service;

import com.elsehere.ai.client.SttResult;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.conversation.dto.TranscriptionPurpose;
import com.elsehere.conversation.entity.TrainingRecording;
import com.elsehere.conversation.repository.TrainingRecordingRepository;
import java.time.Clock;
import java.time.Instant;
import java.util.List;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * 학습용 녹음(training_recordings)을 저장하고 지운다. 동의 여부는 부르는 쪽이 확인한다.
 *
 * <p>저장에 실패해도 음성 인식 응답은 막지 않는다.
 */
@Slf4j
@Service
@RequiredArgsConstructor
public class TrainingRecordingService {

  private final TrainingRecordingRepository trainingRecordingRepository;
  private final TrainingRecordingStore trainingRecordingStore;
  private final Clock clock;

  public void save(
      WorkerTrace trace,
      TranscriptionPurpose purpose,
      byte[] audio,
      String contentType,
      SttResult result) {
    String key = null;
    try {
      key = trainingRecordingStore.save(audio, contentType);
      Integer durationMs =
          result.audioS() == null ? null : (int) Math.round(result.audioS() * 1000);
      trainingRecordingRepository.save(
          TrainingRecording.record(
              trace,
              purpose,
              key,
              TrainingRecordingStore.mediaType(contentType),
              result.text(),
              durationMs,
              Instant.now(clock)));
    } catch (RuntimeException exception) {
      log.warn("학습용 녹음을 저장하지 못했습니다", exception);
      deleteQuietly(key);
    }
  }

  /** 사용자의 학습용 녹음 파일과 기록을 모두 지운다. */
  @Transactional
  public void deleteAllOf(Long userId) {
    List<TrainingRecording> recordings = trainingRecordingRepository.findByUserId(userId);
    recordings.forEach(recording -> trainingRecordingStore.delete(recording.getAudioKey()));
    trainingRecordingRepository.deleteAll(recordings);
  }

  // 기록을 남기지 못한 파일은 내보내기에서 찾을 수 없으므로 지운다.
  private void deleteQuietly(String key) {
    try {
      trainingRecordingStore.delete(key);
    } catch (RuntimeException exception) {
      log.warn("기록을 남기지 못한 학습용 녹음 파일을 지우지 못했습니다", exception);
    }
  }
}
