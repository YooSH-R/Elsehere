package com.elsehere.conversation.service;

import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.entity.AiRequestKind;
import com.elsehere.ai.entity.AiRequestStatus;
import com.elsehere.ai.service.AiRequestRecorder;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.ConversationDetailResponse;
import com.elsehere.conversation.dto.ConversationStartResponse;
import com.elsehere.conversation.dto.EndConversationRequest;
import com.elsehere.conversation.dto.EndConversationResponse;
import com.elsehere.conversation.dto.StartConversationRequest;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.feedback.dto.FeedbackResponse;
import com.elsehere.feedback.service.FeedbackService;
import com.elsehere.onboarding.entity.UserLanguageProfile;
import com.elsehere.onboarding.entity.UserLanguageProfileId;
import com.elsehere.onboarding.repository.UserLanguageProfileRepository;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import java.time.Clock;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

/**
 * 대화 시작(CONV-01)·종료(CONV-08)·조회(CONV-10·11).
 *
 * <p>AI를 부르는 동안 DB 트랜잭션을 잡지 않도록 조회 → AI 호출 → 저장을 나눠 처리한다. 사용자 AI 한도 예약·차감, 미션 연결, 종료 피드백은 아직 하지
 * 않는다.
 */
@Service
@RequiredArgsConstructor
public class ConversationService {

  // 직원의 첫 인사는 AI로 만든다(API 명세 [미정]). 학습자는 아직 말하지 않았으므로 다가온 상황만 알려 준다.
  private static final String GREETING_TEXT = "(The learner walks up to you.)";
  private static final String GREETING_INSTRUCTION =
      "The learner just walked up to you. Greet them first and ask how you can help, "
          + "like you would in your role. 1 short sentence.";

  private final ConversationRepository conversationRepository;
  private final TurnRepository turnRepository;
  private final UserRepository userRepository;
  private final UserLanguageProfileRepository userLanguageProfileRepository;
  private final ConversationSupport support;
  private final AiWorkerClient aiWorkerClient;
  private final AiRequestRecorder aiRequestRecorder;
  private final FeedbackService feedbackService;
  private final TransactionTemplate transactionTemplate;
  private final Clock clock;

  public ConversationStartResponse start(Long userId, StartConversationRequest request) {
    StartContext context =
        transactionTemplate.execute(status -> startContext(userId, request.npcCode()));
    NpcContext npc = context.npc();

    Opening opening =
        request.initiatedBy() == InitiatedBy.NPC
            ? approachOpening(npc)
            : greetingOpening(new WorkerTrace(request.requestId(), userId, null), npc, context);

    return transactionTemplate.execute(
        status -> {
          Conversation conversation =
              conversationRepository.save(
                  Conversation.start(
                      userId,
                      npc.npcId(),
                      request.initiatedBy(),
                      context.level(),
                      clock.instant()));
          Turn openingTurn =
              turnRepository.save(
                  Turn.npc(
                      conversation,
                      1,
                      opening.text(),
                      support.writeWords(opening.suggestWords()),
                      opening.npcAudioKey()));
          return new ConversationStartResponse(
              conversation.getId(),
              conversation.getStatus(),
              conversation.getLevelApplied(),
              npc.summary(),
              List.of(),
              support.npcTurn(openingTurn));
        });
  }

  /** 대화를 끝내고 짧은 피드백을 만든다. NPC가 이미 끝낸 대화는 상태를 바꾸지 않고 피드백만 만든다. 피드백 생성은 대화 종료를 확정한 뒤 트랜잭션 밖에서 한다. */
  public EndConversationResponse end(
      Long userId, Long conversationId, EndConversationRequest request) {
    EndConversationResponse ended = endConversation(userId, conversationId);
    FeedbackResponse feedback =
        feedbackService.feedbackOnEnd(
            userId, conversationId, request.requestId(), request.skipFeedback());
    return ended.withFeedback(feedback);
  }

  private EndConversationResponse endConversation(Long userId, Long conversationId) {
    return transactionTemplate.execute(
        status -> {
          Conversation conversation = support.ownedConversation(userId, conversationId);
          conversation.endByUser(clock.instant());
          NpcContext npc = support.npcById(conversation.getNpcId());
          long userTurnCount =
              turnRepository.countByConversationIdAndSpeaker(conversationId, Speaker.USER);
          return new EndConversationResponse(
              conversation.getId(),
              conversation.getStatus(),
              conversation.getEndReason(),
              ConversationSupport.toOffset(conversation.getEndedAt()),
              npc.summary(),
              npc.sceneCode(),
              userTurnCount,
              null,
              List.of());
        });
  }

  public ConversationDetailResponse detail(Long userId, Long conversationId) {
    return transactionTemplate.execute(
        status -> support.detail(support.ownedConversation(userId, conversationId)));
  }

  /** 진행 중인 대화. 없으면 빈 값이다. */
  public Optional<ConversationDetailResponse> active(Long userId) {
    return transactionTemplate.execute(
        status ->
            conversationRepository
                .findFirstByUserIdAndStatus(userId, ConversationStatus.ACTIVE)
                .map(support::detail));
  }

  private StartContext startContext(Long userId, String npcCode) {
    conversationRepository
        .findFirstByUserIdAndStatus(userId, ConversationStatus.ACTIVE)
        .ifPresent(
            active -> {
              throw new BusinessException(
                  ErrorCode.CONVERSATION_ALREADY_ACTIVE,
                  Map.of("activeConversationId", active.getId()));
            });
    NpcContext npc = support.npcByCode(npcCode);
    if (!npc.sceneOpen()) {
      throw new BusinessException(ErrorCode.SCENE_CLOSED);
    }
    return new StartContext(npc, selectedLevel(userId));
  }

  // 대화 난이도는 지금 학습 언어에서 고른 난이도다. 설정 전이면 기본값(초급)이다.
  private int selectedLevel(Long userId) {
    User user =
        userRepository
            .findById(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    return userLanguageProfileRepository
        .findById(new UserLanguageProfileId(userId, user.getCurrentLanguage()))
        .map(UserLanguageProfile::getSelectedLevel)
        .orElse(UserLanguageProfile.DEFAULT_SELECTED_LEVEL);
  }

  // 행인 말걸기에 응답하면 이미 보여 준 말걸기 대사를 첫 턴으로 저장하고 AI를 부르지 않는다.
  private Opening approachOpening(NpcContext npc) {
    if (npc.approachLine() == null) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }
    return new Opening(npc.approachLine(), null, npc.approachAudioKey());
  }

  private Opening greetingOpening(WorkerTrace trace, NpcContext npc, StartContext context) {
    NpcReply reply =
        aiWorkerClient.reply(
            trace,
            npc.workerNpc(),
            List.of(),
            GREETING_TEXT,
            GREETING_INSTRUCTION,
            context.level());
    aiRequestRecorder.record(
        trace,
        AiRequestKind.NPC,
        reply.usage(),
        reply.succeeded() ? AiRequestStatus.OK : AiRequestStatus.FAILED);
    if (!reply.succeeded()) {
      throw new BusinessException(AiWorkerClient.toErrorCode(reply.errorCode()));
    }
    return new Opening(
        reply.text(), reply.suggestWords(), support.saveNpcAudio(trace, reply.audioFullId()));
  }

  private record StartContext(NpcContext npc, int level) {}

  private record Opening(String text, List<String> suggestWords, String npcAudioKey) {}
}
