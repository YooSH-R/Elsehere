package com.elsehere.user.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.api.Assertions.tuple;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.ai.client.SttResult;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.auth.service.ConsentCatalog;
import com.elsehere.common.config.ClockConfig;
import com.elsehere.common.config.JpaAuditingConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.TranscriptionPurpose;
import com.elsehere.conversation.entity.TrainingRecording;
import com.elsehere.conversation.repository.TrainingRecordingRepository;
import com.elsehere.conversation.service.TrainingRecordingService;
import com.elsehere.conversation.service.TrainingRecordingStore;
import com.elsehere.user.UserTestData;
import com.elsehere.user.dto.TrainingConsentResponse;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.ConsentType;
import jakarta.persistence.EntityManager;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

// 학습 활용 동의 조회·변경(USER-02·03). 음성 동의를 끄면 저장한 녹음을 지우는지 테스트 폴더의 파일로 확인한다.
@DataJpaTest(
    properties = {
      "spring.jpa.hibernate.ddl-auto=create-drop",
      "elsehere.training-recording.dir=" + TrainingConsentServiceTest.RECORDING_DIR
    })
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  ClockConfig.class,
  JpaAuditingConfig.class,
  ConsentCatalog.class,
  TrainingConsentService.class,
  TrainingRecordingService.class,
  TrainingRecordingStore.class
})
class TrainingConsentServiceTest {

  static final String RECORDING_DIR = "build/test-training-recordings";

  @Autowired private TrainingConsentService trainingConsentService;

  @Autowired private TrainingRecordingService trainingRecordingService;

  @Autowired private TrainingRecordingRepository trainingRecordingRepository;

  @Autowired private EntityManager entityManager;

  @Test
  @DisplayName("동의한 적이 없으면 대화 기록·녹음 음성 학습 동의가 모두 꺼진 상태로 나온다")
  void findAll_noConsent_bothOff() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    List<TrainingConsentResponse> consents = trainingConsentService.findAll(userId).consents();

    assertThat(consents)
        .extracting(TrainingConsentResponse::consentType, TrainingConsentResponse::agreed)
        .containsExactly(
            tuple(ConsentType.TRAINING_TEXT, false), tuple(ConsentType.TRAINING_VOICE, false));
  }

  @Test
  @DisplayName("가입자가 대화 기록 학습 동의를 켜면 켜진 상태로 저장된다")
  void change_agree_isAgreed() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    TrainingConsentResponse response =
        trainingConsentService.change(userId, ConsentType.TRAINING_TEXT, true);

    assertThat(response.agreed()).isTrue();
    assertThat(trainingConsentService.hasAgreed(userId, ConsentType.TRAINING_TEXT)).isTrue();
    assertThat(trainingConsentService.hasAgreed(userId, ConsentType.TRAINING_VOICE)).isFalse();
  }

  @Test
  @DisplayName("켠 동의를 끄면 동의하지 않은 상태가 되고, 다시 켜면 다시 동의한 상태가 된다")
  void change_withdrawThenAgreeAgain() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    trainingConsentService.change(userId, ConsentType.TRAINING_TEXT, true);

    TrainingConsentResponse withdrawn =
        trainingConsentService.change(userId, ConsentType.TRAINING_TEXT, false);

    assertThat(withdrawn.agreed()).isFalse();
    assertThat(trainingConsentService.hasAgreed(userId, ConsentType.TRAINING_TEXT)).isFalse();

    trainingConsentService.change(userId, ConsentType.TRAINING_TEXT, true);

    assertThat(trainingConsentService.hasAgreed(userId, ConsentType.TRAINING_TEXT)).isTrue();
  }

  @Test
  @DisplayName("지금 문구와 다른 버전에 동의했으면 동의하지 않은 것으로 본다")
  void hasAgreed_otherVersion_isFalse() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    entityManager
        .createNativeQuery(
            "insert into user_consents (user_id, consent_type, version, agreed_at)"
                + " values (?, 'TRAINING_TEXT', '2000-01', now())")
        .setParameter(1, userId)
        .executeUpdate();

    assertThat(trainingConsentService.hasAgreed(userId, ConsentType.TRAINING_TEXT)).isFalse();
  }

  @Test
  @DisplayName("게스트는 학습 동의를 켤 수 없다")
  void change_guest_isRejected() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-1");

    assertThatThrownBy(() -> trainingConsentService.change(userId, ConsentType.TRAINING_TEXT, true))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.VALIDATION_FAILED));
    assertThat(trainingConsentService.hasAgreed(userId, ConsentType.TRAINING_TEXT)).isFalse();
  }

  @Test
  @DisplayName("학습 활용이 아닌 약관(이용약관 등)은 이 기능으로 바꿀 수 없다")
  void change_nonTrainingTerm_isRejected() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    assertThatThrownBy(() -> trainingConsentService.change(userId, ConsentType.TERMS, false))
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception ->
                assertThat(exception.getErrorCode()).isEqualTo(ErrorCode.VALIDATION_FAILED));
  }

  @Test
  @DisplayName("녹음 음성 동의를 끄면 그동안 저장한 녹음 파일과 기록을 지운다")
  void withdrawVoice_deletesRecordings() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    trainingConsentService.change(userId, ConsentType.TRAINING_VOICE, true);
    trainingRecordingService.save(
        new WorkerTrace(UUID.randomUUID(), userId, null),
        TranscriptionPurpose.TUTORIAL,
        new byte[] {1, 2, 3},
        "audio/wav",
        new SttResult("hello", 1.0, List.of()));
    TrainingRecording saved = trainingRecordingRepository.findByUserId(userId).getFirst();
    Path file = Path.of(RECORDING_DIR, saved.getAudioKey());
    assertThat(file).exists();

    trainingConsentService.change(userId, ConsentType.TRAINING_VOICE, false);

    assertThat(trainingRecordingRepository.findByUserId(userId)).isEmpty();
    assertThat(Files.exists(file)).isFalse();
  }
}
