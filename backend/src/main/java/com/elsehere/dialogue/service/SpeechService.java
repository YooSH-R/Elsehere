package com.elsehere.dialogue.service;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.SttResult;
import com.elsehere.ai.client.WorkerSpeechPhrase;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.TranscriptionPurpose;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.conversation.service.NpcAudioRetention;
import com.elsehere.conversation.service.TrainingRecordingService;
import com.elsehere.dialogue.dto.SpeechResponse;
import com.elsehere.dialogue.dto.TranscriptionResponse;
import com.elsehere.user.entity.ConsentType;
import com.elsehere.user.service.OptionalConsentService;
import java.io.IOException;
import java.time.Clock;
import java.time.Instant;
import java.util.List;
import java.util.Locale;
import java.util.Optional;
import java.util.Set;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;
import org.springframework.web.multipart.MultipartFile;
import tools.jackson.core.type.TypeReference;
import tools.jackson.databind.json.JsonMapper;

/** 음성 인식(CONV-02)과 NPC 음성 다시 듣기·다시 만들기(CONV-06·07). */
@Service
@RequiredArgsConstructor
public class SpeechService {

  private static final long MAX_AUDIO_BYTES = 3L * 1024 * 1024;
  private static final Set<String> AUDIO_TYPES =
      Set.of("audio/webm", "audio/ogg", "audio/mp4", "audio/wav", "audio/x-wav", "audio/mpeg");
  // 대화 음성과 다시 만든 음성에 같은 속도를 적용한다.
  private static final double NPC_SPEECH_SPEED = 1.1;

  private final TurnRepository turnRepository;
  private final ConversationLookup conversationLookup;
  private final DialogueContexts dialogueContexts;
  private final AiGateway aiGateway;
  private final NpcAudioRetention npcAudioRetention;
  private final TransactionTemplate transactionTemplate;
  private final OptionalConsentService optionalConsentService;
  private final TrainingRecordingService trainingRecordingService;
  private final Clock clock;
  private final JsonMapper jsonMapper;

  /**
   * 녹음을 전사한다. 발화 턴은 만들지 않는다. 음성은 외부 음성 인식 공급자로 가므로 음성 처리(VOICE)에 동의한 사용자의 것만 받는다. 받은 음성은 녹음 음성 학습
   * 활용(TRAINING_VOICE)에 동의한 사용자의 것만 인식 결과와 함께 보관하고, 그 밖에는 보관하지 않는다.
   *
   * @param conversationId 자유 대화 발화(TURN)일 때 진행 중인 대화. 튜토리얼이면 null
   */
  public TranscriptionResponse transcribe(
      Long userId,
      UUID requestId,
      TranscriptionPurpose purpose,
      Long conversationId,
      MultipartFile audio) {
    if (!optionalConsentService.hasAgreed(userId, ConsentType.VOICE)) {
      throw new BusinessException(ErrorCode.VOICE_CONSENT_REQUIRED);
    }
    if (audio.isEmpty()) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }
    if (audio.getSize() > MAX_AUDIO_BYTES) {
      throw new BusinessException(ErrorCode.AUDIO_TOO_LARGE);
    }
    String contentType = audioType(audio.getContentType());
    if (purpose == TranscriptionPurpose.TURN) {
      checkActiveConversation(userId, conversationId);
    }

    WorkerTrace trace =
        new WorkerTrace(
            requestId, userId, purpose == TranscriptionPurpose.TURN ? conversationId : null);
    byte[] recording = bytesOf(audio);
    SttResult result = aiGateway.transcribe(trace, recording, contentType);
    if (result.text() == null || result.text().isBlank()) {
      throw new BusinessException(ErrorCode.STT_NO_SPEECH);
    }
    Integer durationMs = result.audioS() == null ? null : (int) Math.round(result.audioS() * 1000);
    if (optionalConsentService.hasAgreed(userId, ConsentType.TRAINING_VOICE)) {
      trainingRecordingService.save(
          trace.userId(),
          trace.requestId(),
          trace.conversationId(),
          purpose,
          recording,
          contentType,
          result.text(),
          durationMs);
    }
    return new TranscriptionResponse(result.text(), durationMs == null ? 0 : durationMs);
  }

  /** 저장해 둔 NPC 턴 음성(WAV). [다시 듣기]는 음성을 새로 만들지 않는다. */
  public byte[] npcSpeech(Long userId, Long turnId) {
    Optional<byte[]> wav =
        transactionTemplate.execute(
            status -> npcAudioRetention.load(conversationLookup.ownedNpcTurn(userId, turnId)));
    // 404를 반환해도 만료·파일 없음 정리는 커밋되어야 한다.
    return wav.orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  /**
   * 음성 합성에 실패한 NPC 턴의 음성을 다시 만든다. 이미 음성이 있거나 보관 정책으로 지운 음성이면 새로 만들지 않는다. 다시 만든 음성을 보관 상한 때문에 저장하지
   * 못하면 FAILED로 돌려주고 자막만 보여 준다.
   */
  public SpeechResponse regenerateSpeech(Long userId, Long turnId, UUID requestId) {
    SpeechTarget target =
        transactionTemplate.execute(
            status -> {
              Turn turn = conversationLookup.ownedNpcTurn(userId, turnId);
              npcAudioRetention.load(turn);
              Conversation conversation = turn.getConversation();
              NpcContext npc = dialogueContexts.npcById(conversation.getNpcId());
              return new SpeechTarget(
                  turn.getNpcAudioKey(),
                  turn.getNpcAudioDeletedAt(),
                  turn.getText(),
                  npc.voiceId(),
                  conversation.getId(),
                  turn.getSpeechPhrases());
            });
    if (target.npcAudioKey() != null || target.npcAudioDeletedAt() != null) {
      return SpeechResponse.of(turnId, target.npcAudioKey(), target.npcAudioDeletedAt());
    }

    WorkerTrace trace = new WorkerTrace(requestId, userId, target.conversationId());
    List<WorkerSpeechPhrase> phrases =
        target.speechPhrases() == null
            ? List.of()
            : jsonMapper.readValue(
                target.speechPhrases(), new TypeReference<List<WorkerSpeechPhrase>>() {});
    byte[] wav =
        phrases.isEmpty()
            ? aiGateway.synthesize(trace, target.text(), target.voiceId(), NPC_SPEECH_SPEED)
            : aiGateway.synthesize(
                trace, target.text(), target.voiceId(), NPC_SPEECH_SPEED, phrases);
    String key = npcAudioRetention.save(wav);
    if (key != null) {
      transactionTemplate.executeWithoutResult(
          status ->
              turnRepository
                  .findById(turnId)
                  .ifPresent(turn -> turn.attachNpcAudio(key, clock.instant())));
    }
    return SpeechResponse.of(turnId, key);
  }

  private void checkActiveConversation(Long userId, Long conversationId) {
    if (conversationId == null) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }
    boolean active =
        Boolean.TRUE.equals(
            transactionTemplate.execute(
                status -> conversationLookup.ownedConversation(userId, conversationId).isActive()));
    if (!active) {
      throw new BusinessException(ErrorCode.CONVERSATION_NOT_ACTIVE);
    }
  }

  // "audio/webm;codecs=opus"처럼 매개변수가 붙어도 형식만 본다.
  private static String audioType(String contentType) {
    if (contentType == null) {
      throw new BusinessException(ErrorCode.AUDIO_FORMAT_UNSUPPORTED);
    }
    String type = contentType.split(";", 2)[0].trim().toLowerCase(Locale.ROOT);
    if (!AUDIO_TYPES.contains(type)) {
      throw new BusinessException(ErrorCode.AUDIO_FORMAT_UNSUPPORTED);
    }
    return contentType;
  }

  private static byte[] bytesOf(MultipartFile audio) {
    try {
      return audio.getBytes();
    } catch (IOException exception) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED, exception);
    }
  }

  private record SpeechTarget(
      String npcAudioKey,
      Instant npcAudioDeletedAt,
      String text,
      String voiceId,
      Long conversationId,
      String speechPhrases) {}
}
