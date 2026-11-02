package com.elsehere.conversation.service;

import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.WorkerLearner;
import com.elsehere.ai.client.WorkerMemory;
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
import com.elsehere.memory.service.MemoryService;
import com.elsehere.mission.service.FirstDayMissionService;
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
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

/**
 * 대화 시작(CONV-01)·종료(CONV-08)·조회(CONV-10·11).
 *
 * <p>AI를 부르는 동안 DB 트랜잭션을 잡지 않도록 조회 → AI 호출 → 저장을 나눠 처리한다. 게스트는 대화 횟수로 제한한다. 사용자 AI 한도 예약·차감은 아직 하지
 * 않는다.
 */
@Slf4j
@Service
@RequiredArgsConstructor
public class ConversationService {

  // 직원의 첫 인사는 AI로 만든다(API 명세 [미정]). 학습자는 아직 말하지 않았으므로 다가온 상황만 알려 준다.
  private static final String GREETING_TEXT = "(The learner walks up to you.)";
  private static final String GREETING_INSTRUCTION =
      "The learner just walked up to you. Greet them first and ask how you can help, "
          + "like you would in your role. 1 short sentence.";

  // 미션 지시에는 학습자가 할 일과 NPC가 해 줄 수 있는 일이 담겨 있다. 첫 인사에서 이를 먼저 다 말하면 학습자가 요청할 일이 사라진다.
  private static final String MISSION_GREETING =
      " Greet the learner and ask how you can help in one short sentence for this situation."
          + " Do not mention the learner's tasks, your offers or directions yet; wait for the learner to ask."
          + " Do not assume their task is complete.";

  private final ConversationRepository conversationRepository;
  private final TurnRepository turnRepository;
  private final UserRepository userRepository;
  private final UserLanguageProfileRepository userLanguageProfileRepository;
  private final ConversationSupport support;
  private final AiWorkerClient aiWorkerClient;
  private final AiRequestRecorder aiRequestRecorder;
  private final FeedbackService feedbackService;
  private final MemoryService memoryService;
  private final TransactionTemplate transactionTemplate;
  private final Clock clock;
  private final FirstDayMissionService firstDayMissionService;
  private final GuestConversationLimit guestConversationLimit;

  public ConversationStartResponse start(Long userId, StartConversationRequest request) {
    StartContext context =
        transactionTemplate.execute(status -> startContext(userId, request.npcCode()));
    NpcContext npc = context.npc();

    // 말걸기 대사가 있는 NPC가 먼저 말을 걸었으면 그 대사로 시작한다. 직원처럼 말걸기 대사가 없으면 AI로 첫 인사를 만든다.
    Opening opening =
        context.mission().isEmpty()
                && request.initiatedBy() == InitiatedBy.NPC
                && npc.approachLine() != null
            ? approachOpening(npc)
            : greetingOpening(new WorkerTrace(request.requestId(), userId, null), npc, context);

    ConversationStartResponse started =
        transactionTemplate.execute(
            status -> {
              // AI를 기다리는 사이 같은 사용자의 다른 시작 요청이 저장됐을 수 있다. 사용자 행을 잠가 시작 요청을 한 줄로 세우고 다시 확인한다.
              ensureCanStart(
                  userRepository
                      .findForUpdate(userId)
                      .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND)));
              Conversation conversation =
                  conversationRepository.save(
                      Conversation.start(
                          userId,
                          npc.npcId(),
                          request.initiatedBy(),
                          context.level(),
                          clock.instant()));
              context
                  .mission()
                  .ifPresent(
                      value -> {
                        if (value.state() != null) {
                          conversation.attachFirstDayStage(value.stage());
                        }
                      });
              Turn openingTurn =
                  turnRepository.save(
                      Turn.npc(
                          conversation, 1, opening.text(), opening.npcAudioKey(), clock.instant()));
              return new ConversationStartResponse(
                  conversation.getId(),
                  conversation.getStatus(),
                  conversation.getLevelApplied(),
                  npc.summary(),
                  List.of(),
                  support.npcTurn(openingTurn),
                  support.state(conversation));
            });
    // 저장이 끝난 뒤 남긴다. 첫 대사는 대화 본문이라 남기지 않는다.
    log.info(
        "conversation_started conversation_id={} npc={} initiated_by={}",
        started.conversationId(),
        npc.summary().npcCode(),
        request.initiatedBy());
    return started;
  }

  /** 대화를 끝내고 짧은 피드백을 만든다. NPC가 이미 끝낸 대화는 상태를 바꾸지 않고 피드백만 만든다. 피드백 생성은 대화 종료를 확정한 뒤 트랜잭션 밖에서 한다. */
  public EndConversationResponse end(
      Long userId, Long conversationId, EndConversationRequest request) {
    EndConversationResponse ended = endConversation(userId, conversationId);
    // 기억 카드 갱신은 따로 실행되어 피드백 생성과 함께 진행된다. 실패해도 종료·피드백에 영향이 없다.
    memoryService.updateAfterConversation(userId, conversationId, request.requestId());
    FeedbackResponse feedback =
        feedbackService.feedbackOnEnd(
            userId, conversationId, request.requestId(), request.skipFeedback());
    return ended.withFeedback(feedback);
  }

  private EndConversationResponse endConversation(Long userId, Long conversationId) {
    return transactionTemplate.execute(
        status -> {
          Conversation conversation = support.ownedConversation(userId, conversationId);
          boolean endingNow = conversation.isActive();
          conversation.endByUser(clock.instant());
          NpcContext npc = support.npcById(conversation.getNpcId());
          long userTurnCount =
              turnRepository.countByConversationIdAndSpeaker(conversationId, Speaker.USER);
          // NPC가 이미 끝낸 대화(TurnService가 남김)나 다시 보낸 종료 요청은 새로 남기지 않는다.
          if (endingNow) {
            log.info(
                "conversation_ended conversation_id={} status={} end_reason={} user_turns={}",
                conversationId,
                conversation.getStatus(),
                conversation.getEndReason(),
                userTurnCount);
          }
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
    User user =
        userRepository
            .findById(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    ensureCanStart(user);
    NpcContext npc = support.npcByCode(npcCode);
    if (!npc.sceneOpen()) {
      throw new BusinessException(ErrorCode.SCENE_CLOSED);
    }
    return new StartContext(
        npc,
        support.learner(user),
        selectedLevel(user),
        memoryService.memoryFor(userId, npc.npcId()),
        firstDayMissionService.context(userId, npcCode));
  }

  // 진행 중인 대화가 없고 게스트 대화 횟수가 남아 있어야 시작할 수 있다. AI 호출 전과 저장 직전에 두 번 확인한다.
  private void ensureCanStart(User user) {
    conversationRepository
        .findFirstByUserIdAndStatus(user.getId(), ConversationStatus.ACTIVE)
        .ifPresent(
            active -> {
              throw new BusinessException(
                  ErrorCode.CONVERSATION_ALREADY_ACTIVE,
                  Map.of("activeConversationId", active.getId()));
            });
    guestConversationLimit.check(user);
  }

  // 대화 난이도는 지금 학습 언어에서 고른 난이도다. 설정 전이면 기본값(초급)이다.
  private int selectedLevel(User user) {
    return userLanguageProfileRepository
        .findById(new UserLanguageProfileId(user.getId(), user.getCurrentLanguage()))
        .map(UserLanguageProfile::getSelectedLevel)
        .orElse(UserLanguageProfile.DEFAULT_SELECTED_LEVEL);
  }

  // 행인 말걸기에 응답하면 이미 보여 준 말걸기 대사를 첫 턴으로 저장하고 AI를 부르지 않는다.
  private Opening approachOpening(NpcContext npc) {
    return new Opening(npc.approachLine(), npc.approachAudioKey());
  }

  private Opening greetingOpening(WorkerTrace trace, NpcContext npc, StartContext context) {
    NpcReply reply =
        aiWorkerClient.reply(
            trace,
            npc.workerNpc(),
            context.memory(),
            List.of(),
            GREETING_TEXT,
            context
                .mission()
                .map(value -> value.instruction() + MISSION_GREETING)
                .orElse(GREETING_INSTRUCTION),
            context.level(),
            context.learner());
    aiRequestRecorder.record(
        trace,
        AiRequestKind.NPC,
        reply.usage(),
        reply.succeeded() ? AiRequestStatus.OK : AiRequestStatus.FAILED);
    if (!reply.succeeded()) {
      throw new BusinessException(AiWorkerClient.toErrorCode(reply.errorCode()));
    }
    return new Opening(reply.text(), support.saveNpcAudio(trace, reply.audioFullId()));
  }

  /**
   * @param learner 대답 상대인 학습자. NPC가 캐릭터에 맞는 호칭을 쓰게 한다
   * @param memory 이 NPC와의 기억. 처음 만나면 null
   */
  private record StartContext(
      NpcContext npc,
      WorkerLearner learner,
      int level,
      WorkerMemory memory,
      Optional<FirstDayMissionService.Context> mission) {}

  private record Opening(String text, String npcAudioKey) {}
}
