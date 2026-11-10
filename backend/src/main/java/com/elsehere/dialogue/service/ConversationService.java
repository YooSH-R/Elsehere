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
import com.elsehere.mission.entity.PracticeScenario;
import com.elsehere.mission.service.FirstDayMissionService;
import com.elsehere.mission.service.MissionDialogueService;
import com.elsehere.mission.service.PlacePracticeService;
import com.elsehere.onboarding.entity.UserLanguageProfile;
import com.elsehere.onboarding.entity.UserLanguageProfileId;
import com.elsehere.onboarding.repository.UserLanguageProfileRepository;
import com.elsehere.review.dto.ReviewCardResponse;
import com.elsehere.review.service.ReviewCardService;
import com.elsehere.review.service.ReviewPracticeResults;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import com.elsehere.world.service.PoliceOfficers;
import java.time.Clock;
import java.time.Instant;
import java.util.List;
import java.util.Map;
import java.util.Objects;
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
 * <p>상황 복습과 장소 연습(PRC-02)도 같은 흐름으로 시작한다. 둘 다 지금 퀘스트·기억·건넬 것을 섞지 않고 정한 상황만 연습한다.
 *
 * <p>AI를 부르는 동안 DB 트랜잭션을 잡지 않도록 조회 → AI 호출 → 저장을 나눠 처리한다. 게스트는 대화 횟수로 제한하고, 대화 하나는 발화 30턴까지다. 사용자
 * AI 한도 예약·차감은 아직 하지 않는다.
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
      " Welcome the learner in your own voice, following your personality and first-meeting behavior. Invite their request in one short sentence for this situation."
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
  // 경찰관이 신고를 받아 직접 순찰차로 다녀온 결과(PoliceDesk, FollowUpService). 학습자의 물건을 찾았으면 돌려준다.
  private static final String POLICE_CALL_TEXT =
      "(The learner who reported the trouble is back at the police station.)";
  private static final String POLICE_CALL_INSTRUCTION =
      "You are back from the call the learner reported, and this is how it ended: %s. Tell them in"
          + " your own way in one or two short sentences, and if you found something of theirs,"
          + " hand it back. Do not ask them to report it again.";

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
  private final DialogueRequestLocks requestLocks;
  private final ReviewCardService reviewCardService;
  private final ReviewDialogue reviewDialogue;
  private final ReviewPracticeResults reviewPracticeResults;
  private final PlacePracticeService placePractice;

  public ConversationStartResponse start(Long userId, StartConversationRequest request) {
    return requestLocks.execute(
        userId, request.requestId(), () -> startOnce(userId, request, null, null));
  }

  public ConversationStartResponse startReview(
      Long userId, Long cardId, UUID requestId, double speechSpeed) {
    return requestLocks.execute(
        userId,
        requestId,
        () -> {
          ReviewCardResponse review = reviewCardService.detail(userId, cardId);
          boolean offDuty =
              transactionTemplate.execute(
                  status ->
                      conversationLookup
                          .ownedConversation(userId, review.sourceConversationId())
                          .isNpcOffDuty());
          return startOnce(
              userId,
              new StartConversationRequest(
                  requestId, review.npc().npcCode(), InitiatedBy.USER, offDuty, null, speechSpeed),
              review,
              null);
        });
  }

  /** 장소 연습(PRC-02). 고른 상황의 가게 직원과 학습자가 말을 거는 대화를 시작한다. 없는 상황이면 NOT_FOUND다. */
  public ConversationStartResponse startPractice(
      Long userId, String scenarioId, UUID requestId, double speechSpeed) {
    PracticeScenario practice = placePractice.scenario(scenarioId);
    return requestLocks.execute(
        userId,
        requestId,
        () ->
            startOnce(
                userId,
                new StartConversationRequest(
                    requestId,
                    practice.place().npcCode(),
                    InitiatedBy.USER,
                    false,
                    null,
                    speechSpeed),
                null,
                practice));
  }

  private ConversationStartResponse startOnce(
      Long userId,
      StartConversationRequest request,
      ReviewCardResponse review,
      PracticeScenario practice) {
    Long reviewCardId = review == null ? null : review.id();
    String practiceScenarioId = practice == null ? null : practice.name();
    // 응답을 받지 못해 같은 요청을 다시 보내면 새로 만들지 않고 처음 시작한 대화를 돌려준다(CONV-03과 같은 방식).
    Optional<ConversationStartResponse> replayed =
        transactionTemplate.execute(
            status -> replay(userId, request.requestId(), reviewCardId, practiceScenarioId));
    if (replayed.isPresent()) {
      return replayed.get();
    }
    String travelSituation =
        review == null && request.npcTravelContext() != null
            ? request.npcTravelContext().describe()
            : null;
    StartContext context =
        transactionTemplate.execute(
            status ->
                startContext(
                    userId,
                    request.npcCode(),
                    request.npcOffDuty(),
                    request.speechSpeed(),
                    review,
                    practice,
                    travelSituation));
    NpcContext npc = context.npc();

    // 이 NPC가 건넬 것이 준비됐으면 건네는 말로 시작한다. 말걸기 대사가 있는 NPC가 먼저 말을 걸었으면 그 대사로 시작한다.
    // 직원처럼 말걸기 대사가 없거나 근무 시간이 아니면 AI로 첫 인사를 만든다.
    WorkerTrace trace = new WorkerTrace(request.requestId(), userId, null);
    Opening opening =
        context
            .followUp()
            .map(
                ready ->
                    PoliceOfficers.includes(npc.summary().npcCode())
                        ? greetingOpening(
                            trace,
                            npc,
                            context,
                            POLICE_CALL_TEXT,
                            POLICE_CALL_INSTRUCTION.formatted(ready.item()))
                        : greetingOpening(
                            trace,
                            npc,
                            context,
                            HANDOVER_TEXT,
                            HANDOVER_INSTRUCTION.formatted(ready.item())))
            .orElseGet(
                () ->
                    context.review() == null
                            && context.mission().isEmpty()
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
              Optional<ConversationStartResponse> saved =
                  replay(userId, request.requestId(), reviewCardId, practiceScenarioId);
              if (saved.isPresent()) {
                return new Started(saved.get(), false);
              }
              ensureCanStart(user);
              if (review != null) {
                reviewCardService.lockForStart(userId, review.id());
              }
              Conversation conversation =
                  conversationRepository.save(
                      Conversation.start(
                          userId,
                          npc.npcId(),
                          request.initiatedBy(),
                          context.level(),
                          clock.instant()));
              conversation.attachStartRequest(request.requestId());
              conversation.recordNpcTravelSituation(travelSituation);
              if (review != null) {
                conversation.attachReviewCard(review.id());
              }
              if (practice != null) {
                conversation.attachPracticeScenario(practice.name());
              }
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
                            conversation.recordTownStoryGoals(value.detail().remainingGoals());
                          } else {
                            conversation.attachFirstDayStage(value.detail().stage());
                            conversation.recordFirstDayGoals(value.detail().remainingGoals());
                          }
                        }
                      });
              Turn openingTurn =
                  turnRepository.save(
                      Turn.npc(
                          conversation, 1, opening.text(), opening.npcAudioKey(), clock.instant()));
              openingTurn.attachSpeechPhrases(opening.speechPhrases());
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

  // 같은 요청 ID로 이미 시작한 대화의 시작 응답. 다른 사용자의 요청 ID이거나 다른 방식(일반·복습·장소 연습)으로 시작한 대화면
  // REQUEST_ID_CONFLICT다.
  private Optional<ConversationStartResponse> replay(
      Long userId, UUID requestId, Long reviewCardId, String practiceScenarioId) {
    return conversationRepository
        .findByRequestId(requestId)
        .map(
            conversation -> {
              if (!conversation.getUserId().equals(userId)
                  || !Objects.equals(conversation.getReviewCardId(), reviewCardId)
                  || !Objects.equals(conversation.getPracticeScenario(), practiceScenarioId)) {
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
                  conversation.isReview() || conversation.isPractice()
                      ? null
                      : followUpService.deliveredIn(conversation.getId()).orElse(null));
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
        deliveredFollowUpId,
        conversation.getReviewCardId(),
        conversation.getPracticeScenario());
  }

  /**
   * 대화를 끝내고 짧은 피드백을 만든다. NPC가 이미 끝낸 대화는 상태를 바꾸지 않고 피드백만 만든다. 피드백 생성은 대화 종료를 확정한 뒤 트랜잭션 밖에서 한다. NPC가
   * 곧 건네기로 한 것도 함께 판정해 담는다.
   *
   * <p>장소 연습 대화는 피드백만 만든다. 이야기·관계와 따로 하는 연습이라 기억 카드와 건넬 것을 만들지 않는다.
   */
  public EndConversationResponse end(
      Long userId, Long conversationId, EndConversationRequest request) {
    Ending ending = endConversation(userId, conversationId);
    EndConversationResponse ended = ending.response();
    if (ended.reviewCardId() != null) {
      return ended;
    }
    CompletableFuture<Optional<FollowUpResponse>> followUp =
        CompletableFuture.completedFuture(Optional.empty());
    if (!ending.practice()) {
      // 기억 카드 갱신은 따로 실행되어 피드백 생성과 함께 진행된다. 실패해도 종료·피드백에 영향이 없다.
      memoryService.updateAfterConversation(userId, conversationId, request.requestId());
      // 건넬 것 판정도 따로 실행해 피드백을 만드는 동안 끝낸다. 실패하면 없는 것으로 본다.
      followUp = followUpService.afterConversation(userId, conversationId, request.requestId());
    }
    FeedbackResponse feedback =
        feedbackService.feedbackOnEnd(
            userId, conversationId, request.requestId(), request.skipFeedback());
    return ended
        .withFeedback(feedback)
        .withDetailedFeedback(
            feedback.status() == com.elsehere.feedback.entity.FeedbackStatus.DONE
                ? feedbackService.detailed(userId, conversationId).items()
                : List.of())
        .withFollowUp(followUp.join().orElse(null));
  }

  private Ending endConversation(Long userId, Long conversationId) {
    return transactionTemplate.execute(
        status -> {
          Conversation conversation =
              conversationLookup.ownedConversationForUpdate(userId, conversationId);
          boolean endingNow = conversation.isActive();
          Instant now = clock.instant();
          conversation.endByUser(now);
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
          EndConversationResponse response =
              new EndConversationResponse(
                      conversation.getId(),
                      conversation.getStatus(),
                      conversation.getEndReason(),
                      ConversationResponses.toOffset(conversation.getEndedAt()),
                      npc.summary(),
                      npc.sceneCode(),
                      userTurnCount,
                      null,
                      List.of(),
                      null,
                      conversation.getReviewCardId())
                  .withReviewResult(
                      conversation.isReview()
                          ? reviewPracticeResults.record(userId, conversation)
                          : null);
          return new Ending(response, conversation.isPractice());
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

  private StartContext startContext(
      Long userId,
      String npcCode,
      boolean npcOffDuty,
      double speechSpeed,
      ReviewCardResponse review,
      PracticeScenario practice,
      String travelSituation) {
    User user =
        userRepository
            .findById(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    ensureCanStart(user);
    NpcContext npc =
        dialogueContexts
            .npcByCode(userId, npcCode, npcOffDuty)
            .withTravelSituation(travelSituation);
    if (!npc.sceneOpen()) {
      throw new BusinessException(ErrorCode.SCENE_CLOSED);
    }
    // 복습과 장소 연습은 지금 퀘스트, 이 NPC와의 기억, 건넬 것을 섞지 않고 정한 상황만 연습한다.
    boolean storyDialogue = review == null && practice == null;
    return new StartContext(
        npc,
        dialogueContexts.learner(user, speechSpeed),
        selectedLevel(user),
        storyDialogue ? memoryService.memoryFor(userId, npc.npcId()) : null,
        storyDialogue ? missionDialogueService.context(userId, npcCode) : Optional.empty(),
        storyDialogue ? followUpService.readyFor(userId, npc.npcId()) : Optional.empty(),
        review,
        practice == null ? null : placePractice.context(practice, List.of()));
  }

  // 진행 중인 대화가 없고 게스트 대화 횟수가 남아 있어야 시작할 수 있다. AI 호출 전과 저장 직전에 확인한다.
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
    return new Opening(npc.approachLine(), npc.approachAudioKey(), null);
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
    return new Opening(
        reply.text(),
        npcAudioReceiver.save(trace, reply.audioFullId()),
        conversationResponses.writeSpeechPhrases(reply.speechPhrases()));
  }

  // 첫날 미션 단계의 NPC는 미션 지시대로 인사한다. 미션 NPC가 근무 시간이 아닐 때 미션을 어떻게 할지는 아직 정하지 않아
  // (docs/npc-life-design.md 11절 "교대와 첫날 미션") 지금은 미션 지시를 따른다.
  private String greetingInstruction(StartContext context, boolean npcOffDuty) {
    if (context.review() != null) {
      return reviewDialogue.instruction(context.review())
          + " Open with one brief welcome and let the learner make their request.";
    }
    if (context.practice() != null) {
      return context.practice().instruction() + MISSION_GREETING;
    }
    return context
        .mission()
        .map(value -> value.detail().instruction() + MISSION_GREETING)
        .orElse(npcOffDuty ? OFF_DUTY_GREETING_INSTRUCTION : GREETING_INSTRUCTION);
  }

  /**
   * @param learner 대답 상대인 학습자. NPC가 캐릭터에 맞는 호칭을 쓰게 한다
   * @param memory 이 NPC와의 기억. 처음 만나면 null
   * @param followUp 이 NPC가 지금 건넬 수 있는 것
   * @param practice 장소 연습 상황의 목표와 직원 지시. 장소 연습이 아니면 null
   */
  private record StartContext(
      NpcContext npc,
      WorkerLearner learner,
      int level,
      WorkerMemory memory,
      Optional<MissionDialogueService.Context> mission,
      Optional<FollowUpService.ReadyFollowUp> followUp,
      ReviewCardResponse review,
      FirstDayMissionService.Context practice) {}

  private record Opening(String text, String npcAudioKey, String speechPhrases) {}

  /**
   * @param practice 장소 연습 대화인지. 종료 뒤 기억 카드와 건넬 것을 만들지 않는다
   */
  private record Ending(EndConversationResponse response, boolean practice) {}

  /**
   * @param created 이번 요청이 대화를 새로 만들었는지. 같은 요청 ID로 이미 만든 대화를 돌려주면 false
   */
  private record Started(ConversationStartResponse response, boolean created) {}
}
