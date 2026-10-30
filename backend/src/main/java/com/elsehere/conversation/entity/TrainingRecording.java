package com.elsehere.conversation.entity;

import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.common.entity.BaseEntity;
import com.elsehere.conversation.dto.TranscriptionPurpose;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Index;
import jakarta.persistence.Table;
import java.time.Instant;
import java.util.UUID;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/**
 * 녹음 음성 학습 활용(TRAINING_VOICE)에 동의한 사용자의 녹음과 그 인식 결과. 음성 인식(CONV-02) 한 번이 한 행이다.
 *
 * <p>녹음 파일은 {@code audioKey} 이름으로 학습용 녹음 폴더에 있다. 사용자(users)와 대화(conversations)는 ID만 보관한다.
 */
@Entity
@Table(
    name = "training_recordings",
    indexes = @Index(name = "idx_training_recordings_user", columnList = "user_id"))
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class TrainingRecording extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @Column(nullable = false)
  private Long userId;

  @Column(nullable = false)
  private UUID requestId;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 20)
  private TranscriptionPurpose purpose;

  private Long conversationId;

  @Column(nullable = false, length = 100)
  private String audioKey;

  @Column(nullable = false, length = 100)
  private String contentType;

  @Column(nullable = false, columnDefinition = "text")
  private String sttText;

  private Integer durationMs;

  @Column(nullable = false)
  private Instant recordedAt;

  public static TrainingRecording record(
      WorkerTrace trace,
      TranscriptionPurpose purpose,
      String audioKey,
      String contentType,
      String sttText,
      Integer durationMs,
      Instant recordedAt) {
    TrainingRecording recording = new TrainingRecording();
    recording.userId = trace.userId();
    recording.requestId = trace.requestId();
    recording.conversationId = trace.conversationId();
    recording.purpose = purpose;
    recording.audioKey = audioKey;
    recording.contentType = contentType;
    recording.sttText = sttText;
    recording.durationMs = durationMs;
    recording.recordedAt = recordedAt;
    return recording;
  }
}
