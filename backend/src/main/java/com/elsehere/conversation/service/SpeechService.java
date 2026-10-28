package com.elsehere.conversation.service;

import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.client.SttResult;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.entity.AiRequestKind;
import com.elsehere.ai.entity.AiRequestStatus;
import com.elsehere.ai.service.AiRequestRecorder;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.SpeechResponse;
import com.elsehere.conversation.dto.TranscriptionPurpose;
import com.elsehere.conversation.dto.TranscriptionResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TurnRepository;
import java.io.IOException;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;
import org.springframework.web.multipart.MultipartFile;

/** 음성 인식(CONV-02)과 NPC 음성 다시 듣기·다시 만들기(CONV-06·07). */
@Service
@RequiredArgsConstructor
public class SpeechService {

  private static final long MAX_AUDIO_BYTES = 3L * 1024 * 1024;
  private static final Set<String> AUDIO_TYPES =
      Set.of("audio/webm", "audio/ogg", "audio/mp4", "audio/wav", "audio/x-wav", "audio/mpeg");
  // 음성 합성(/v1/tts)은 난이도 배율을 Spring이 곱해 보낸다(AI 명세 1.1).
  private static final Map<Integer, Double> LEVEL_SPEED = Map.of(1, 0.85, 2, 1.0, 3, 1.06);

  private final TurnRepository turnRepository;
  private final ConversationSupport support;
  private final AiWorkerClient aiWorkerClient;
  private final AiRequestRecorder aiRequestRecorder;
  private final NpcAudioStore npcAudioStore;
  private final TransactionTemplate transactionTemplate;

  /**
   * 녹음을 전사한다. 발화 턴은 만들지 않으며 받은 음성은 보관하지 않는다.
   *
   * @param conversationId 자유 대화 발화(TURN)일 때 진행 중인 대화. 튜토리얼이면 null
   */
  public TranscriptionResponse transcribe(
      Long userId,
      UUID requestId,
      TranscriptionPurpose purpose,
      Long conversationId,
      MultipartFile audio) {
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
    SttResult result = aiWorkerClient.transcribe(trace, bytesOf(audio), contentType);
    aiRequestRecorder.record(trace, AiRequestKind.STT, result.usage(), AiRequestStatus.OK);
    if (result.text() == null || result.text().isBlank()) {
      throw new BusinessException(ErrorCode.STT_NO_SPEECH);
    }
    long durationMs = result.audioS() == null ? 0 : Math.round(result.audioS() * 1000);
    return new TranscriptionResponse(result.text(), durationMs);
  }

  /** 저장해 둔 NPC 턴 음성(WAV). [다시 듣기]는 음성을 새로 만들지 않는다. */
  public byte[] npcSpeech(Long userId, Long turnId) {
    String key =
        transactionTemplate.execute(
            status -> support.ownedNpcTurn(userId, turnId).getNpcAudioKey());
    return npcAudioStore.load(key).orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  /** 음성 합성에 실패한 NPC 턴의 음성을 다시 만든다. 이미 음성이 있으면 새로 만들지 않는다. */
  public SpeechResponse regenerateSpeech(Long userId, Long turnId, UUID requestId) {
    SpeechTarget target =
        transactionTemplate.execute(
            status -> {
              Turn turn = support.ownedNpcTurn(userId, turnId);
              Conversation conversation = turn.getConversation();
              NpcContext npc = support.npcById(conversation.getNpcId());
              return new SpeechTarget(
                  turn.getNpcAudioKey(),
                  turn.getText(),
                  npc.voiceId(),
                  conversation.getId(),
                  conversation.getLevelApplied());
            });
    if (target.npcAudioKey() != null) {
      return SpeechResponse.of(turnId, target.npcAudioKey());
    }

    WorkerTrace trace = new WorkerTrace(requestId, userId, target.conversationId());
    double speed = LEVEL_SPEED.getOrDefault(target.level(), 1.0);
    byte[] wav = aiWorkerClient.synthesize(trace, target.text(), target.voiceId(), speed);
    aiRequestRecorder.record(trace, AiRequestKind.TTS, List.of(), AiRequestStatus.OK);
    String key = npcAudioStore.save(wav);
    transactionTemplate.executeWithoutResult(
        status -> turnRepository.findById(turnId).ifPresent(turn -> turn.attachNpcAudio(key)));
    return SpeechResponse.of(turnId, key);
  }

  private void checkActiveConversation(Long userId, Long conversationId) {
    if (conversationId == null) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }
    boolean active =
        Boolean.TRUE.equals(
            transactionTemplate.execute(
                status -> support.ownedConversation(userId, conversationId).isActive()));
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
      String npcAudioKey, String text, String voiceId, Long conversationId, int level) {}
}
