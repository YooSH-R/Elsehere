package com.elsehere.feedback.service;

import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.service.AiGateway;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.feedback.dto.ModelSpeechResponse;
import com.elsehere.feedback.entity.TurnFeedback;
import com.elsehere.feedback.repository.TurnFeedbackRepository;
import java.util.UUID;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

/**
 * 상세 평가 항목의 모범 음성(FB-04). 고친 문장(betterText)을 합성해 두고, 한 번 만든 음성은 다시 쓴다.
 *
 * <p>목소리는 사물 발음(WORLD-04)과 같게 서버 설정값 하나로 두며, 대화한 NPC 목소리를 쓰지 않는다. 다른 사용자의 피드백은 존재 여부를 드러내지 않도록
 * NOT_FOUND로 응답한다.
 */
@Service
public class ModelSpeechService {

  private static final double SPEED = 1.0;

  private final TurnFeedbackRepository turnFeedbackRepository;
  private final ConversationLookup conversationLookup;
  private final AiGateway aiGateway;
  private final ModelSpeechStore modelSpeechStore;
  private final TransactionTemplate transactionTemplate;
  private final String voice;

  public ModelSpeechService(
      TurnFeedbackRepository turnFeedbackRepository,
      ConversationLookup conversationLookup,
      AiGateway aiGateway,
      ModelSpeechStore modelSpeechStore,
      TransactionTemplate transactionTemplate,
      @Value("${elsehere.model-speech.voice}") String voice) {
    this.turnFeedbackRepository = turnFeedbackRepository;
    this.conversationLookup = conversationLookup;
    this.aiGateway = aiGateway;
    this.modelSpeechStore = modelSpeechStore;
    this.transactionTemplate = transactionTemplate;
    this.voice = voice;
  }

  /** 모범 음성을 만들고 받을 주소를 돌려준다. 이미 만든 음성이 있으면 새로 만들지 않는다. 고칠 표현이 없는 항목은 모범 문장이 없으므로 NOT_FOUND다. */
  public ModelSpeechResponse create(Long userId, Long turnFeedbackId, UUID requestId) {
    SpeechTarget target =
        transactionTemplate.execute(status -> ownedTarget(userId, turnFeedbackId));
    if (target.betterText() == null || target.betterText().isBlank()) {
      throw new BusinessException(ErrorCode.NOT_FOUND);
    }
    // 저장한 파일이 없어졌으면(볼륨 교체 등) 다시 만든다.
    if (modelSpeechStore.exists(target.modelAudioKey())) {
      return ModelSpeechResponse.of(turnFeedbackId);
    }

    WorkerTrace trace = new WorkerTrace(requestId, userId, target.conversationId());
    byte[] wav = aiGateway.synthesize(trace, target.betterText(), voice, SPEED);
    String key = modelSpeechStore.save(wav);
    boolean attached = false;
    try {
      attached =
          Boolean.TRUE.equals(
              transactionTemplate.execute(
                  status ->
                      turnFeedbackRepository.attachModelAudio(
                              turnFeedbackId, target.modelAudioKey(), key)
                          == 1));
    } finally {
      // 붙이지 못한 파일(다른 요청이 먼저 붙임, 피드백이 지워짐, DB 반영 실패)은 아무도 가리키지 않으므로 지운다.
      if (!attached) {
        modelSpeechStore.delete(key);
      }
    }
    return ModelSpeechResponse.of(turnFeedbackId);
  }

  /** 만들어 둔 모범 음성(WAV). 아직 만들지 않았으면 NOT_FOUND다. */
  public byte[] load(Long userId, Long turnFeedbackId) {
    String key =
        transactionTemplate.execute(status -> ownedTarget(userId, turnFeedbackId).modelAudioKey());
    return modelSpeechStore.load(key).orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  private SpeechTarget ownedTarget(Long userId, Long turnFeedbackId) {
    TurnFeedback feedback =
        turnFeedbackRepository
            .findById(turnFeedbackId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    Conversation conversation =
        conversationLookup.ownedTurn(userId, feedback.getTurnId()).getConversation();
    return new SpeechTarget(
        feedback.getBetterText(), feedback.getModelAudioKey(), conversation.getId());
  }

  private record SpeechTarget(String betterText, String modelAudioKey, Long conversationId) {}
}
