package com.elsehere.dialogue.service;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.WorkerLearner;
import com.elsehere.ai.client.WorkerMemory;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.service.GuestConversationLimit;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.dialogue.dto.ConversationDetailResponse;
import com.elsehere.dialogue.dto.ConversationStartResponse;
import com.elsehere.dialogue.dto.EndConversationRequest;
import com.elsehere.dialogue.dto.EndConversationResponse;
import com.elsehere.dialogue.dto.StartConversationRequest;
import com.elsehere.feedback.dto.FeedbackResponse;
import com.elsehere.feedback.service.FeedbackService;
import com.elsehere.followup.dto.FollowUpResponse;
import com.elsehere.followup.service.FollowUpService;
import com.elsehere.memory.service.MemoryService;
import com.elsehere.mission.service.MissionDialogueService;
import com.elsehere.onboarding.entity.UserLanguageProfile;
import com.elsehere.onboarding.entity.UserLanguageProfileId;
import com.elsehere.onboarding.repository.UserLanguageProfileRepository;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import java.time.Clock;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

/**
 * 대화 시작(CONV-01)·종료(CONV-08)·조회(CONV-10·11).
 *
 * <p>NPC가 대화에서 곧 건네기로 한 것은 종료할 때 판정해 응답에 담고, 준비된 뒤 그 NPC와 대화를 시작하면 첫마디로 건넨다({@link
 * FollowUpService}).
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
      "The learner just walked up to you. Open in your own voice, following your personality, "
          + "first-meeting behavior, familiarity and current situation. If you are a passerby, react "
          + "as a local approached by someone, not a shop assistant: you may ask what brings them "
          + "over, cautiously or warmly as fits your character. Only offer service if you are "
          + "actually serving customers now. Give them an easy conversational opening. 1 or 2 short sentences.";

  // 미션 지시에는 학습자가 할 일과 NPC가 해 줄 수 있는 일이 담겨 있다. 첫 인사에서 이를 먼저 다 말하면 학습자가 요청할 일이 사라진다.
  private static final String MISSION_GREETING =
      " Greet the learner and ask how you can help in one short sentence for this situation."
          + " Do not mention the learner's tasks, your offers or directions yet; wait for the learner to ask."
          + " Do not assume their task is complete.";

  // 근무 시간이 아닌 NPC는 손님을 맞지 않으므로 무엇을 도와줄지 묻지 않고 쉬는 중에 만난 사람처럼 인사한다.
  private static final String OFF_DUTY_GREETING_INSTRUCTION =
      "The learner just walked up to you while you are off work. Greet them casually, the way"
          + " you would in your free time, following your personality, familiarity and first-meeting "
          + "behavior. You may share a small observation or ask what brings them over. "
          + "Do not offer to help or serve them. 1 or 2 short sentences.";
  // 준비된 것을 건네는 첫마디. 학습자는 아직 말하지 않았으므로 NPC가 먼저 부른다.
  private static final String HANDOVER_TEXT = "(The learner is waiting nearby for their order.)";
  private static final String HANDOVER_INSTRUCTION =
      "The %s the learner asked you for is ready now. Call them and hand it over warmly in your own"
          + " way, in one or two short sentences. Do not ask them to order again.";

  private final ConversationRepository conversationRepository;
  private final TurnRepository turnRepository;
  private final UserRepository userRepository;
  private final UserLanguageProfileRepository userLanguageProfileRepository;
  private final ConversationLookup conversationLookup;
  private final DialogueContexts dialogueContexts;
  private final ConversationResponses conversationResponses;
  private final NpcAudioReceiver npcAudioReceiver;
  private final AiGateway aiGateway;
  private final FeedbackService feedbackService;
  private final MemoryService memoryService;
  private final FollowUpService followUpService;
  private final TransactionTemplate transactionTemplate;
  private final Clock clock;
  private final MissionDialogueService missionDialogueService;
  private final GuestConversationLimit guestConversationLimit;

  public ConversationStartResponse start(Long userId, StartConversationRequest request) {
    // 응답을 받지 못해 같은 요청을 다시 보내면 새로 만들지 않고 처음 시작한 대화를 돌려준다(CONV-03과 같은 방식).
    Optional<ConversationStartResponse> replayed =
        transactionTemplate.execute(status -> replay(userId, request.requestId()));
    if (replayed.isPresent()) {
      return replayed.get();
    }
    StartContext context =
        transactionTemplate.execute(
            status -> startContext(userId, request.npcCode(), request.npcOffDuty()));
    NpcContext npc = context.npc();

    // 이 NPC가 건넬 것이 준비됐으면 건네는 말로 시작한다. 말걸기 대사가 있는 NPC가 먼저 말을 걸었으면 그 대사로 시작한다.
    // 직원처럼 말걸기 대사가 없거나 근무 시간이 아니면 AI로 첫 인사를 만든다.
    WorkerTrace trace = new WorkerTrace(request.requestId(), userId, null);
    Opening opening =
        context
            .followUp()
            .map(
                ready ->
                    greetingOpening(
                        trace,
                        npc,
                        context,
                        HANDOVER_TEXT,
                        HANDOVER_INSTRUCTION.formatted(ready.item())))
            .orElseGet(
                () ->
                    context.mission().isEmpty()
                            && !request.npcOffDuty()
                            && request.initiatedBy() == InitiatedBy.NPC
                            && npc.approachLine() != null
                        ? approachOpening(npc)
                        : greetingOpening(
                            trace,
                            npc,
                            context,
                            GREETING_TEXT,
                            greetingInstruction(context, request.npcOffDuty())));

    Started started =
        transactionTemplate.execute(
            status -> {
              // AI를 기다리는 사이 같은 사용자의 다른 시작 요청이 저장됐을 수 있다. 사용자 행을 잠가 시작 요청을 한 줄로 세우고 다시 확인한다.
              User user =
                  userRepository
                      .findForUpdate(userId)
                      .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
              // 같은 요청 ID의 재요청이 먼저 저장했으면 거절하지 않고 그 대화를 돌려준다.
              Optional<ConversationStartResponse> saved = replay(userId, request.requestId());
              if (saved.isPresent()) {
                return new Started(saved.get(), false);
              }
              ensureCanStart(user);
              Conversation conversation =
                  conversationRepository.save(
                      Conversation.start(
                          userId,
                          npc.npcId(),
                          request.initiatedBy(),
                          context.level(),
                          clock.instant()));
              conversation.attachStartRequest(request.requestId());
              if (request.npcOffDuty()) {
                conversation.markNpcOffDuty();
              }
              context
                  .mission()
                  .ifPresent(
                      value -> {
                        if (value.detail().state() != null) {
                          if (value.townStory()) {
                            conversation.attachTownStoryStage(value.detail().stage());
                          } else {
                            conversation.attachFirstDayStage(value.detail().stage());
                          }
                        }
                      });
              Turn openingTurn =
                  turnRepository.save(
                      Turn.npc(
                          conversation, 1, opening.text(), opening.npcAudioKey(), clock.instant()));
              Long deliveredFollowUpId =
                  context
                      .followUp()
                      .map(
                          ready -> {
                            followUpService.deliver(ready.followUpId(), conversation.getId());
                            return ready.followUpId();
                          })
                      .orElse(null);
              return new Started(
                  startResponse(conversation, npc, openingTurn, deliveredFollowUpId), true);
            });
    if (started.created()) {
      // 저장이 끝난 뒤 남긴다. 첫 대사는 대화 본문이라 남기지 않는다.
      log.info(
          "conversation_started conversation_id={} npc={} initiated_by={}",
          started.response().conversationId(),
          npc.summary().npcCode(),
          request.initiatedBy());
    }
    return started.response();
  }

  // 같은 요청 ID로 이미 시작한 대화의 시작 응답. 다른 사용자의 요청 ID면 REQUEST_ID_CONFLICT다.
  private Optional<ConversationStartResponse> replay(Long userId, UUID requestId) {
    return conversationRepository
        .findByRequestId(requestId)
        .map(
            conversation -> {
              if (!conversation.getUserId().equals(userId)) {
                throw new BusinessException(ErrorCode.REQUEST_ID_CONFLICT);
              }
              Turn openingTurn =
                  turnRepository
                      .findByConversationIdAndSeq(conversation.getId(), 1)
                      .orElseThrow(() -> new BusinessException(ErrorCode.INTERNAL_ERROR));
              return startResponse(
                  conversation,
                  dialogueContexts.npcById(conversation.getNpcId()),
                  openingTurn,
                  followUpService.deliveredIn(conversation.getId()).orElse(null));
            });
  }

  private ConversationStartResponse startResponse(
      Conversation conversation, NpcContext npc, Turn openingTurn, Long deliveredFollowUpId) {
    return new ConversationStartResponse(
        conversation.getId(),
        conversation.getStatus(),
        conversation.getLevelApplied(),
        npc.summary(),
        List.of(),
        conversationResponses.npcTurn(openingTurn),
        conversationResponses.state(conversation),
        deliveredFollowUpId);
  }

  /**
   * 대화를 끝내고 짧은 피드백을 만든다. NPC가 이미 끝낸 대화는 상태를 바꾸지 않고 피드백만 만든다. 피드백 생성은 대화 종료를 확정한 뒤 트랜잭션 밖에서 한다. NPC가
   * 곧 건네기로 한 것도 함께 판정해 담는다.
   */
  public EndConversationResponse end(
      Long userId, Long conversationId, EndConversationRequest request) {
    EndConversationResponse ended = endConversation(userId, conversationId);
    // 기억 카드 갱신은 따로 실행되어 피드백 생성과 함께 진행된다. 실패해도 종료·피드백에 영향이 없다.
    memoryService.updateAfterConversation(userId, conversationId, request.requestId());
    // 건넬 것 판정도 따로 실행해 피드백을 만드는 동안 끝낸다. 실패하면 없는 것으로 본다.
    CompletableFuture<Optional<FollowUpResponse>> followUp =
        followUpService.afterConversation(userId, conversationId, request.requestId());
    FeedbackResponse feedback =
        feedbackService.feedbackOnEnd(
            userId, conversationId, request.requestId(), request.skipFeedback());
    return ended.withFeedback(feedback).withFollowUp(followUp.join().orElse(null));
  }

  private EndConversationResponse endConversation(Long userId, Long conversationId) {
    return transactionTemplate.execute(
        status -> {
          Conversation conversation = conversationLookup.ownedConversation(userId, conversationId);
          boolean endingNow = conversation.isActive();
          conversation.endByUser(clock.instant());
          NpcContext npc = dialogueContexts.npcById(conversation.getNpcId());
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
              ConversationResponses.toOffset(conversation.getEndedAt()),
              npc.summary(),
              npc.sceneCode(),
              userTurnCount,
              null,
              List.of(),
              null);
        });
  }

  public ConversationDetailResponse detail(Long userId, Long conversationId) {
    return transactionTemplate.execute(
        status ->
            conversationResponses.detail(
                conversationLookup.ownedConversation(userId, conversationId)));
  }

  /** 진행 중인 대화. 없으면 빈 값이다. */
  public Optional<ConversationDetailResponse> active(Long userId) {
    return transactionTemplate.execute(
        status ->
            conversationRepository
                .findFirstByUserIdAndStatus(userId, ConversationStatus.ACTIVE)
                .map(conversationResponses::detail));
  }

  private StartContext startContext(Long userId, String npcCode, boolean npcOffDuty) {
    User user =
        userRepository
            .findById(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    ensureCanStart(user);
    NpcContext npc = dialogueContexts.npcByCode(npcCode, npcOffDuty);
    if (!npc.sceneOpen()) {
      throw new BusinessException(ErrorCode.SCENE_CLOSED);
    }
    return new StartContext(
        npc,
        dialogueContexts.learner(user),
        selectedLevel(user),
        memoryService.memoryFor(userId, npc.npcId()),
        missionDialogueService.context(userId, npcCode),
        followUpService.readyFor(userId, npc.npcId()));
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

  private Opening greetingOpening(
      WorkerTrace trace, NpcContext npc, StartContext context, String text, String instruction) {
    NpcReply reply =
        aiGateway.reply(
            trace,
            npc.workerNpc(),
            context.memory(),
            List.of(),
            text,
            instruction,
            context.level(),
            context.learner());
    return new Opening(reply.text(), npcAudioReceiver.save(trace, reply.audioFullId()));
  }

  // 첫날 미션 단계의 NPC는 미션 지시대로 인사한다. 미션 NPC가 근무 시간이 아닐 때 미션을 어떻게 할지는 아직 정하지 않아
  // (docs/npc-life-design.md 11절 "교대와 첫날 미션") 지금은 미션 지시를 따른다.
  private static String greetingInstruction(StartContext context, boolean npcOffDuty) {
    return context
        .mission()
        .map(value -> value.detail().instruction() + MISSION_GREETING)
        .orElse(npcOffDuty ? OFF_DUTY_GREETING_INSTRUCTION : GREETING_INSTRUCTION);
  }

  /**
   * @param learner 대답 상대인 학습자. NPC가 캐릭터에 맞는 호칭을 쓰게 한다
   * @param memory 이 NPC와의 기억. 처음 만나면 null
   * @param followUp 이 NPC가 지금 건넬 수 있는 것
   */
  private record StartContext(
      NpcContext npc,
      WorkerLearner learner,
      int level,
      WorkerMemory memory,
      Optional<MissionDialogueService.Context> mission,
      Optional<FollowUpService.ReadyFollowUp> followUp) {}

  private record Opening(String text, String npcAudioKey) {}

  /**
   * @param created 이번 요청이 대화를 새로 만들었는지. 같은 요청 ID로 이미 만든 대화를 돌려주면 false
   */
  private record Started(ConversationStartResponse response, boolean created) {}
}
