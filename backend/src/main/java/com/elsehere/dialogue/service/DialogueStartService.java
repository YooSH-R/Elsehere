package com.elsehere.dialogue.service;

import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.service.GuestConversationLimit;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.dialogue.dto.ConversationStartResponse;
import com.elsehere.dialogue.dto.StartConversationRequest;
import com.elsehere.favor.service.FavorService;
import com.elsehere.followup.service.FollowUpService;
import com.elsehere.memory.service.MemoryService;
import com.elsehere.mission.entity.PracticeScenario;
import com.elsehere.mission.service.PlacePracticeService;
import com.elsehere.mission.service.QuestTalkService;
import com.elsehere.onboarding.entity.UserLanguageProfile;
import com.elsehere.onboarding.entity.UserLanguageProfileId;
import com.elsehere.onboarding.repository.UserLanguageProfileRepository;
import com.elsehere.review.dto.ReviewCardResponse;
import com.elsehere.review.service.ReviewCardService;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import java.time.Clock;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.data.domain.Limit;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

/**
 * 대화 시작(CONV-01). 첫마디는 {@link OpeningLines}가 고르고, 종료(CONV-08)는 {@link DialogueEndService}가, 상세·진행
 * 중인 대화 조회(CONV-10·11)는 {@link ConversationHistoryService}가 맡는다.
 *
 * <p>NPC가 대화에서 곧 건네기로 한 것은 종료할 때 판정해 응답에 담고({@link DialogueEndService}), 준비된 뒤 그 NPC와 대화를 시작하면 첫마디로
 * 건넨다({@link FollowUpService}).
 *
 * <p>상황 복습과 장소 연습(PRC-02)도 같은 흐름으로 시작한다. 둘 다 지금 퀘스트·기억·건넬 것을 섞지 않고 정한 상황만 연습한다.
 *
 * <p>AI를 부르는 동안 DB 트랜잭션을 잡지 않도록 조회 → AI 호출 → 저장을 나눠 처리한다. 게스트는 대화 횟수로 제한하고, 대화 하나는 발화 30턴까지다. 사용자
 * AI 한도 예약·차감은 아직 하지 않는다.
 */
@Slf4j
@Service
@RequiredArgsConstructor
public class DialogueStartService {
  private final ConversationRepository conversationRepository;
  private final TurnRepository turnRepository;
  private final UserRepository userRepository;
  private final UserLanguageProfileRepository userLanguageProfileRepository;
  private final ConversationLookup conversationLookup;
  private final DialogueContexts dialogueContexts;
  private final ConversationResponses conversationResponses;
  private final MemoryService memoryService;
  private final FollowUpService followUpService;
  private final TransactionTemplate transactionTemplate;
  private final Clock clock;
  private final QuestTalkService questTalks;
  private final GuestConversationLimit guestConversationLimit;
  private final DialogueRequestLocks requestLocks;
  private final ReviewCardService reviewCardService;
  private final PlacePracticeService placePractice;
  private final MeaningPreparation meaningPreparation;
  private final OpeningLines openingLines;
  private final FavorService favorService;

  public ConversationStartResponse start(Long userId, StartConversationRequest request) {
    return start(userId, request, null);
  }

  /**
   * 대화 시작을 스트리밍으로 받는다. AI가 만드는 첫 인사의 글자 조각과 문장 조각 음성을 만들어지는 대로 {@code events}에 넘기고, 처리 순서·저장·결과는
   * {@link #start(Long, StartConversationRequest)}와 같다. 말걸기 대사로 시작하거나 같은 요청 ID로 이미 시작한 대화를 돌려줄 때는
   * AI를 부르지 않으므로 조각을 넘기지 않는다.
   *
   * @param events null이면 첫 인사를 끝까지 모아 한 번에 응답한다
   */
  public ConversationStartResponse start(
      Long userId, StartConversationRequest request, TurnEvents events) {
    return requestLocks.execute(
        userId, request.requestId(), () -> startOnce(userId, request, StartMode.STORY, events));
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
                  requestId,
                  review.npc().npcCode(),
                  InitiatedBy.USER,
                  offDuty,
                  null,
                  speechSpeed,
                  null),
              StartMode.review(review),
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
                StartMode.practice(practice),
                null));
  }

  private ConversationStartResponse startOnce(
      Long userId, StartConversationRequest request, StartMode mode, TurnEvents events) {
    // 응답을 받지 못해 같은 요청을 다시 보내면 새로 만들지 않고 처음 시작한 대화를 돌려준다(CONV-03과 같은 방식).
    Optional<ConversationStartResponse> replayed =
        transactionTemplate.execute(status -> replay(userId, request.requestId(), mode));
    if (replayed.isPresent()) {
      return replayed.get();
    }
    // 복습 대화에는 지금의 이동·위치를 덧붙이지 않는다.
    String travelSituation = mode.isReview() ? null : request.npcSituation();
    StartContext context =
        transactionTemplate.execute(status -> startContext(userId, request, mode, travelSituation));
    NpcContext npc = context.npc();

    // 첫마디를 고르고 AI로 만들 첫마디면 만든다(OpeningLines). 이 사이에는 DB 트랜잭션을 잡지 않는다.
    WorkerTrace trace = new WorkerTrace(request.requestId(), userId, null);
    OpeningLines.Opening opening = openingLines.open(context, request, trace, events);

    Started started =
        transactionTemplate.execute(
            status -> {
              // AI를 기다리는 사이 같은 사용자의 다른 시작 요청이 저장됐을 수 있다. 사용자 행을 잠가 시작 요청을 한 줄로 세우고 다시 확인한다.
              User user =
                  userRepository
                      .findForUpdate(userId)
                      .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
              // 같은 요청 ID의 재요청이 먼저 저장했으면 거절하지 않고 그 대화를 돌려준다.
              Optional<ConversationStartResponse> saved = replay(userId, request.requestId(), mode);
              if (saved.isPresent()) {
                return new Started(saved.get(), false);
              }
              ensureCanStart(user);
              if (mode.isReview()) {
                reviewCardService.lockForStart(userId, mode.reviewCardId());
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
              if (!mode.isReview()) {
                conversation.recordNpcNeeds(request.needs());
              }
              mode.applyTo(conversation);
              if (request.npcOffDuty()) {
                conversation.markNpcOffDuty();
              }
              context.quest().ifPresent(quest -> quest.recordStage(conversation));
              Turn openingTurn =
                  turnRepository.save(
                      Turn.npc(
                          conversation, 1, opening.text(), opening.npcAudioKey(), clock.instant()));
              openingTurn.attachSpeechPhrases(opening.speechPhrases());
              if (opening.meaning() != null) {
                // 저장 공연은 원문과 번역을 함께 기록해 뜻 미리 생성에서도 AI를 부르지 않는다.
                openingTurn.attachMeaning(opening.meaning(), "[]");
              }
              context
                  .favorCall()
                  .ifPresent(call -> favorService.attachCall(call.favorId(), conversation.getId()));
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
      if (opening.streamAfterSave() && events != null) {
        events.delta(opening.text());
      }
      // 저장이 끝난 뒤 남긴다. 첫 대사는 대화 본문이라 남기지 않는다.
      log.info(
          "conversation_started conversation_id={} npc={} initiated_by={}",
          started.response().conversationId(),
          npc.summary().npcCode(),
          request.initiatedBy());
      // 첫 대사를 저장한 트랜잭션이 끝난 뒤 [뜻 보기]를 미리 만든다.
      meaningPreparation.prepare(
          userId, started.response().openingTurn().turnId(), request.requestId());
    }
    return started.response();
  }

  // 같은 요청 ID로 이미 시작한 대화의 시작 응답. 다른 사용자의 요청 ID이거나 다른 방식(일반·복습·장소 연습)으로 시작한 대화면
  // REQUEST_ID_CONFLICT다.
  private Optional<ConversationStartResponse> replay(Long userId, UUID requestId, StartMode mode) {
    return conversationRepository
        .findByRequestId(requestId)
        .map(
            conversation -> {
              if (!conversation.getUserId().equals(userId) || !mode.matches(conversation)) {
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

  private StartContext startContext(
      Long userId, StartConversationRequest request, StartMode mode, String travelSituation) {
    User user =
        userRepository
            .findById(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    ensureCanStart(user);
    NpcContext npc =
        dialogueContexts
            .npcByCode(userId, request.npcCode(), request.npcOffDuty())
            .withTravelSituation(travelSituation)
            .withNeeds(mode.isReview() ? null : request.needs());
    if (!npc.sceneOpen()) {
      throw new BusinessException(ErrorCode.SCENE_CLOSED);
    }
    // 복습은 카드에 저장된 시연 조건을 처음부터 연습하고, 장소 연습은 선택한 상황만 연습한다.
    var review = mode.review();
    var quest =
        review != null
            ? questTalks.forReview(
                review.scenario() == null ? null : review.scenario().key(), List.of())
            : questTalks.forStart(userId, request.npcCode(), false, mode.practice());
    boolean showcase =
        quest.filter(selected -> selected.source() == QuestTalkService.Source.SHOWCASE).isPresent();
    var followUp =
        mode.story() && !showcase
            ? followUpService.readyFor(userId, npc.npcId())
            : Optional.<FollowUpService.ReadyFollowUp>empty();
    // NPC가 먼저 말을 걸어 꺼낸 부탁이 있으면 첫마디를 그 부탁으로 한다(docs/npc-life-design.md 7.6).
    var favorCall =
        mode.story() && followUp.isEmpty()
            ? favorService.pendingCall(userId, request.npcCode())
            : Optional.<FavorService.CallOpening>empty();
    StreetGreeting streetGreeting = null;
    if (mode.story()
        && quest.isEmpty()
        && followUp.isEmpty()
        && favorCall.isEmpty()
        && StreetGreeting.appliesTo(request)) {
      List<String> recent =
          turnRepository.findRecentNpcOpenings(
              userId, npc.npcId(), Limit.of(StreetGreeting.RECENT_LIMIT));
      streetGreeting = StreetGreeting.choose(request, recent);
    }
    return new StartContext(
        npc,
        dialogueContexts.learner(user, request.speechSpeed()),
        selectedLevel(user),
        mode.story() ? memoryService.memoryFor(userId, npc.npcId()) : null,
        quest,
        followUp,
        favorCall,
        mode,
        streetGreeting);
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

  /**
   * @param created 이번 요청이 대화를 새로 만들었는지. 같은 요청 ID로 이미 만든 대화를 돌려주면 false
   */
  private record Started(ConversationStartResponse response, boolean created) {}
}
