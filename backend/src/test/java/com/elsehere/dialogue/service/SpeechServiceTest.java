package com.elsehere.dialogue.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static com.elsehere.conversation.DialogueTestData.npcTurn;
import static com.elsehere.conversation.DialogueTestData.textTurn;
import static com.elsehere.dialogue.service.DialogueServiceTest.TRAINING_RECORDING_DIR;
import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyDouble;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.SttResult;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.TranscriptionPurpose;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.TrainingRecording;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TrainingRecordingRepository;
import com.elsehere.conversation.service.NpcAudioStore;
import com.elsehere.dialogue.dto.SpeechResponse;
import com.elsehere.dialogue.dto.SpeechStatus;
import com.elsehere.dialogue.dto.TranscriptionResponse;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.ConsentType;
import com.elsehere.user.service.OptionalConsentService;
import jakarta.persistence.EntityManager;
import java.nio.file.Path;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import org.assertj.core.api.ThrowableAssert.ThrowingCallable;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.mockito.ArgumentCaptor;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.mock.web.MockMultipartFile;

// 음성 인식(CONV-02)과 NPC 음성 다시 듣기·다시 만들기(CONV-06·07). 워커는 Mock이고 음성 파일은 테스트 폴더에 저장한다.
@DialogueServiceTest
class SpeechServiceTest {

  private static final byte[] RECORDING = {1, 2, 3};
  private static final byte[] WAV = {82, 73, 70, 70};

  @Autowired private SpeechService speechService;

  @Autowired private AiGateway aiGateway;

  @Autowired private EntityManager entityManager;

  @Autowired private OptionalConsentService optionalConsentService;

  @Autowired private TrainingRecordingRepository trainingRecordingRepository;

  @Autowired private NpcAudioStore npcAudioStore;

  private Long userId;
  private Conversation conversation;
  private Turn npcTurn;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    optionalConsentService.change(userId, ConsentType.VOICE, true);
    conversation = conversation(entityManager, userId, "CAFE_STAFF");
    npcTurn = npcTurn(entityManager, conversation, 1, "Hi! What can I get for you?");
    entityManager.flush();
  }

  @Test
  @DisplayName("진행 중인 대화의 녹음을 전사해 문장과 길이를 돌려주고, 워커 요청에 요청 ID와 대화를 붙인다")
  void transcribe_turn_returnsText() {
    given(aiGateway.transcribe(any(), eq(RECORDING), eq("audio/webm;codecs=opus")))
        .willReturn(new SttResult("I want a latte", 1.25, List.of()));
    UUID requestId = UUID.randomUUID();

    TranscriptionResponse response =
        speechService.transcribe(
            userId,
            requestId,
            TranscriptionPurpose.TURN,
            conversation.getId(),
            recording("audio/webm;codecs=opus", RECORDING));

    assertThat(response).isEqualTo(new TranscriptionResponse("I want a latte", 1250));
    then(aiGateway)
        .should()
        .transcribe(
            eq(new WorkerTrace(requestId, userId, conversation.getId())),
            eq(RECORDING),
            eq("audio/webm;codecs=opus"));
  }

  @Test
  @DisplayName("튜토리얼 녹음은 대화 없이 전사하고 워커 요청에 대화 ID를 붙이지 않는다")
  void transcribe_tutorial_doesNotNeedConversation() {
    given(aiGateway.transcribe(any(), any(), anyString()))
        .willReturn(new SttResult("Hello", null, List.of()));

    TranscriptionResponse response =
        speechService.transcribe(
            userId,
            UUID.randomUUID(),
            TranscriptionPurpose.TUTORIAL,
            null,
            recording("audio/wav", RECORDING));

    assertThat(response).isEqualTo(new TranscriptionResponse("Hello", 0));
    ArgumentCaptor<WorkerTrace> trace = ArgumentCaptor.forClass(WorkerTrace.class);
    then(aiGateway).should().transcribe(trace.capture(), any(), anyString());
    assertThat(trace.getValue().conversationId()).isNull();
  }

  @Test
  @DisplayName("음성 처리에 동의하지 않은 사용자의 녹음은 워커로 보내지 않고 VOICE_CONSENT_REQUIRED로 거절한다")
  void transcribe_withoutVoiceConsent_isRejectedWithoutAi() {
    optionalConsentService.change(userId, ConsentType.VOICE, false);

    assertErrorCode(
        () -> transcribeTurn(recording("audio/wav", RECORDING)), ErrorCode.VOICE_CONSENT_REQUIRED);
    then(aiGateway).should(never()).transcribe(any(), any(), anyString());
  }

  @Test
  @DisplayName("빈 녹음·너무 긴 녹음·받지 않는 형식은 워커를 부르지 않고 거절한다")
  void transcribe_invalidRecording_isRejectedWithoutAi() {
    assertErrorCode(
        () -> transcribeTurn(recording("audio/wav", new byte[0])), ErrorCode.VALIDATION_FAILED);
    assertErrorCode(
        () -> transcribeTurn(recording("audio/wav", new byte[3 * 1024 * 1024 + 1])),
        ErrorCode.AUDIO_TOO_LARGE);
    assertErrorCode(
        () -> transcribeTurn(recording("video/mp4", RECORDING)),
        ErrorCode.AUDIO_FORMAT_UNSUPPORTED);
    assertErrorCode(
        () -> transcribeTurn(recording(null, RECORDING)), ErrorCode.AUDIO_FORMAT_UNSUPPORTED);
    then(aiGateway).should(never()).transcribe(any(), any(), anyString());
  }

  @Test
  @DisplayName("발화 녹음인데 대화 ID가 없거나 대화가 끝났으면 거절한다")
  void transcribe_turnWithoutActiveConversation_isRejected() {
    assertErrorCode(
        () ->
            speechService.transcribe(
                userId,
                UUID.randomUUID(),
                TranscriptionPurpose.TURN,
                null,
                recording("audio/wav", RECORDING)),
        ErrorCode.VALIDATION_FAILED);
    conversation.endByUser(Instant.parse("2026-10-27T01:05:00Z"));
    assertErrorCode(
        () -> transcribeTurn(recording("audio/wav", RECORDING)), ErrorCode.CONVERSATION_NOT_ACTIVE);
  }

  @Test
  @DisplayName("말소리를 알아듣지 못하면(빈 전사) STT_NO_SPEECH다")
  void transcribe_blankText_throwsNoSpeech() {
    given(aiGateway.transcribe(any(), any(), anyString()))
        .willReturn(new SttResult("  ", 0.5, List.of()));

    assertErrorCode(
        () -> transcribeTurn(recording("audio/wav", RECORDING)), ErrorCode.STT_NO_SPEECH);
  }

  @Test
  @DisplayName("녹음 음성 학습 활용에 동의한 사용자는 전사한 녹음과 인식 결과를 학습용으로 저장한다")
  void transcribe_voiceTrainingAgreed_savesRecording() {
    optionalConsentService.change(userId, ConsentType.TRAINING_VOICE, true);
    given(aiGateway.transcribe(any(), any(), anyString()))
        .willReturn(new SttResult("I want a latte", 1.25, List.of()));
    UUID requestId = UUID.randomUUID();

    speechService.transcribe(
        userId,
        requestId,
        TranscriptionPurpose.TURN,
        conversation.getId(),
        recording("audio/webm;codecs=opus", RECORDING));

    TrainingRecording saved = trainingRecordingRepository.findByUserId(userId).getFirst();
    assertThat(saved.getRequestId()).isEqualTo(requestId);
    assertThat(saved.getConversationId()).isEqualTo(conversation.getId());
    assertThat(saved.getPurpose()).isEqualTo(TranscriptionPurpose.TURN);
    assertThat(saved.getSttText()).isEqualTo("I want a latte");
    assertThat(saved.getDurationMs()).isEqualTo(1250);
    assertThat(saved.getContentType()).isEqualTo("audio/webm");
    assertThat(Path.of(TRAINING_RECORDING_DIR, saved.getAudioKey())).hasBinaryContent(RECORDING);
  }

  @Test
  @DisplayName("녹음 음성 학습 활용에 동의하지 않았으면 녹음을 저장하지 않는다")
  void transcribe_voiceTrainingNotAgreed_doesNotSave() {
    optionalConsentService.change(userId, ConsentType.TRAINING_TEXT, true);
    given(aiGateway.transcribe(any(), any(), anyString()))
        .willReturn(new SttResult("I want a latte", 1.25, List.of()));

    transcribeTurn(recording("audio/wav", RECORDING));

    assertThat(trainingRecordingRepository.findByUserId(userId)).isEmpty();
  }

  @Test
  @DisplayName("동의했어도 말소리를 알아듣지 못한 녹음은 저장하지 않는다")
  void transcribe_noSpeech_doesNotSave() {
    optionalConsentService.change(userId, ConsentType.TRAINING_VOICE, true);
    given(aiGateway.transcribe(any(), any(), anyString()))
        .willReturn(new SttResult("  ", 0.5, List.of()));

    assertErrorCode(
        () -> transcribeTurn(recording("audio/wav", RECORDING)), ErrorCode.STT_NO_SPEECH);
    assertThat(trainingRecordingRepository.findByUserId(userId)).isEmpty();
  }

  @Test
  @DisplayName("음성 인식을 부르다 워커에 닿지 못하면 오류를 그대로 알린다")
  void transcribe_workerCallFails_throws() {
    given(aiGateway.transcribe(any(), eq(RECORDING), eq("audio/webm;codecs=opus")))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));
    UUID requestId = UUID.randomUUID();

    assertThatThrownBy(
            () ->
                speechService.transcribe(
                    userId,
                    requestId,
                    TranscriptionPurpose.TURN,
                    conversation.getId(),
                    recording("audio/webm;codecs=opus", RECORDING)))
        .isInstanceOf(BusinessException.class);
  }

  @Test
  @DisplayName("음성을 다시 합성하다 워커에 닿지 못하면 오류를 그대로 알린다")
  void regenerateSpeech_workerCallFails_throws() {
    given(aiGateway.synthesize(any(), eq(npcTurn.getText()), eq("af_heart"), eq(1.1)))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));
    UUID requestId = UUID.randomUUID();

    assertThatThrownBy(() -> speechService.regenerateSpeech(userId, npcTurn.getId(), requestId))
        .isInstanceOf(BusinessException.class);
  }

  @Test
  @DisplayName("음성이 없는 NPC 턴은 1.1배 속도로 다시 합성해 저장하고, 이후 다시 듣기로 받을 수 있다")
  void regenerateSpeech_withoutAudio_synthesizesAndSaves() {
    given(aiGateway.synthesize(any(), eq(npcTurn.getText()), eq("af_heart"), eq(1.1)))
        .willReturn(WAV);
    UUID requestId = UUID.randomUUID();

    SpeechResponse response = speechService.regenerateSpeech(userId, npcTurn.getId(), requestId);

    assertThat(response.status()).isEqualTo(SpeechStatus.READY);
    assertThat(response.url()).isEqualTo("/api/v1/turns/" + npcTurn.getId() + "/speech");
    assertThat(speechService.npcSpeech(userId, npcTurn.getId())).isEqualTo(WAV);
    then(aiGateway)
        .should()
        .synthesize(
            eq(new WorkerTrace(requestId, userId, conversation.getId())),
            eq(npcTurn.getText()),
            eq("af_heart"),
            eq(1.1));
  }

  @Test
  @DisplayName("이미 음성이 있는 NPC 턴은 다시 합성하지 않는다")
  void regenerateSpeech_withAudio_doesNotSynthesize() {
    given(aiGateway.synthesize(any(), anyString(), anyString(), anyDouble())).willReturn(WAV);
    speechService.regenerateSpeech(userId, npcTurn.getId(), UUID.randomUUID());

    SpeechResponse again =
        speechService.regenerateSpeech(userId, npcTurn.getId(), UUID.randomUUID());

    assertThat(again.status()).isEqualTo(SpeechStatus.READY);
    then(aiGateway).should().synthesize(any(), anyString(), anyString(), anyDouble());
  }

  @Test
  @DisplayName("보관 정책으로 음성을 지운 턴은 다시 합성하지 않고 EXPIRED를 돌려준다")
  void regenerateSpeech_deletedByPolicy_returnsExpiredWithoutSynthesis() {
    npcTurn.deleteNpcAudio(Instant.parse("2026-10-28T00:00:00Z"));
    entityManager.flush();

    SpeechResponse response =
        speechService.regenerateSpeech(userId, npcTurn.getId(), UUID.randomUUID());

    assertThat(response.status()).isEqualTo(SpeechStatus.EXPIRED);
    assertThat(response.url()).isNull();
    then(aiGateway).should(never()).synthesize(any(), anyString(), anyString(), anyDouble());
  }

  @DisplayName("정기 정리 전 만료됐거나 복원 후 파일이 없는 음성은 재생성 요청에서도 EXPIRED로 처리한다")
  @ParameterizedTest
  @ValueSource(booleans = {true, false})
  void regenerateSpeech_unavailableStoredAudio_returnsExpired(boolean expired) {
    String key = expired ? npcAudioStore.save(WAV) : UUID.randomUUID() + ".wav";
    npcTurn.attachNpcAudio(key, expired ? Instant.EPOCH : Instant.parse("2026-10-29T00:00:00Z"));
    entityManager.flush();

    SpeechResponse response =
        speechService.regenerateSpeech(userId, npcTurn.getId(), UUID.randomUUID());

    assertThat(response.status()).isEqualTo(SpeechStatus.EXPIRED);
    assertThat(response.url()).isNull();
    then(aiGateway).should(never()).synthesize(any(), anyString(), anyString(), anyDouble());
  }

  @Test
  @DisplayName("정기 정리 전이라도 만료된 NPC 음성 조회는 NOT_FOUND이며 삭제 상태를 기록한다")
  void npcSpeech_expiredBeforeScheduledCleanup_throwsNotFound() {
    String key = npcAudioStore.save(WAV);
    npcTurn.attachNpcAudio(key, Instant.EPOCH);
    entityManager.flush();

    assertErrorCode(() -> speechService.npcSpeech(userId, npcTurn.getId()), ErrorCode.NOT_FOUND);

    entityManager.flush();
    entityManager.clear();
    Turn expired = entityManager.find(Turn.class, npcTurn.getId());
    assertThat(expired.getNpcAudioKey()).isNull();
    assertThat(expired.getNpcAudioDeletedAt()).isNotNull();
    assertThat(npcAudioStore.load(key)).isEmpty();
  }

  @Test
  @DisplayName("음성 파일이 없는 턴, 사용자 턴, 다른 사용자의 턴은 다시 듣기에서 NOT_FOUND다")
  void npcSpeech_unavailableTurns_throwNotFound() {
    Turn userTurn = textTurn(entityManager, conversation, 2, "A latte, please.");
    Long otherUserId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Other");

    assertErrorCode(() -> speechService.npcSpeech(userId, npcTurn.getId()), ErrorCode.NOT_FOUND);
    assertErrorCode(() -> speechService.npcSpeech(userId, userTurn.getId()), ErrorCode.NOT_FOUND);
    assertErrorCode(
        () -> speechService.npcSpeech(otherUserId, npcTurn.getId()), ErrorCode.NOT_FOUND);
    assertErrorCode(
        () -> speechService.regenerateSpeech(otherUserId, npcTurn.getId(), UUID.randomUUID()),
        ErrorCode.NOT_FOUND);
  }

  private TranscriptionResponse transcribeTurn(MockMultipartFile audio) {
    return speechService.transcribe(
        userId, UUID.randomUUID(), TranscriptionPurpose.TURN, conversation.getId(), audio);
  }

  private static MockMultipartFile recording(String contentType, byte[] bytes) {
    return new MockMultipartFile("audio", "speech", contentType, bytes);
  }

  private static void assertErrorCode(ThrowingCallable call, ErrorCode expected) {
    assertThatThrownBy(call)
        .isInstanceOfSatisfying(
            BusinessException.class,
            exception -> assertThat(exception.getErrorCode()).isEqualTo(expected));
  }
}
