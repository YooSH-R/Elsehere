package com.elsehere.dialogue.service;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.GuardResult;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.NpcReplyChunk;
import com.elsehere.ai.client.NpcReplyListener;
import com.elsehere.ai.client.WorkerLearner;
import com.elsehere.ai.client.WorkerLine;
import com.elsehere.ai.client.WorkerMemory;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.logging.LogContext;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.dialogue.dto.PoliceDispatchResponse;
import com.elsehere.dialogue.dto.TaxiDestinationResponse;
import com.elsehere.dialogue.dto.TurnRequest;
import com.elsehere.dialogue.dto.TurnResponse;
import com.elsehere.memory.service.MemoryService;
import com.elsehere.memory.service.NpcAffinityService;
import com.elsehere.mission.service.FirstDayMissionService;
import com.elsehere.mission.service.MissionDialogueService;
import com.elsehere.mission.service.PlacePracticeService;
import com.elsehere.review.dto.ReviewCardResponse;
import com.elsehere.review.service.ReviewCardService;
import java.time.Clock;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Executor;
import java.util.concurrent.Executors;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

/**
 * 발화 전송(CONV-03). 의도·번역 판정(/v1/judge)과 무례 판정(/v1/guard)을 함께 부른 뒤 → NPC 대답(/v1/npc/reply) → 턴 전체 음성
 * 저장 순서로 처리한다. 스트리밍 요청에서 판정 결과가 대답 지시문에 들어가지 않는 대화는 판정과 동시에 대답을 먼저 시작하고, 판정 뒤에 정한 대답 요청이 같으면 그 대답을
 * 쓴다({@link EarlyReply}).
 *
 * <p>첫날 연계 미션을 시작한 사용자는 현재 단계의 의도를 판정한다. 성공한 대답과 턴을 저장할 때만 미션을 진행한다. 무례한 말은 미션에 반영하지 않고 횟수를 세며 그
 * NPC의 호감도를 깎고, 한도에 닿으면 NPC가 대화를 끝낸다(docs/api/rudeness-handling.md). 무례가 아닌 말은 의도 판정이 고른 대화
 * 사건(칭찬·인사 등)만큼 호감도를 올린다(docs/api/ai-worker.md 2.2절). 미션 목표를 마친 턴이나 학습자가 작별·용건 끝냄을 말한 턴에는 NPC가 마무리
 * 대사로 대화를 끝낸다({@link WrapUp}). 대화당 사용자 발화는 최대 30턴이다.
 *
 * <p>장소 연습 대화는 이야기 퀘스트 대신 고른 상황의 남은 목표를 판정해 그 대화에 기록하고, 목표를 모두 이룬 턴에 마무리한다. 복습처럼 기억과 호감도에는 반영하지
 * 않는다.
 */
@Slf4j
@Service
@RequiredArgsConstructor
public class TurnService {

  // 무례 턴의 NPC 지시문. 웹 프로토타입에서 다듬은 문장 그대로다(무례 처리 합의안 5절).
  static final String RUDE_WARNING =
      "They just said something rude or insulting to you. Do not ignore it. React like a real"
          + " person: calmly but firmly say it is not okay to talk like that. 1-2 short sentences.";
  static final String RUDE_LEAVE =
      "They keep insulting you. Firmly tell them you won't talk with them anymore and they need"
          + " to leave now. Then stop. 1-2 short sentences.";
  static final String TURN_LIMIT_WRAP_UP =
      "This is the final turn. Complete the current business in this reply, do not ask a"
          + " new question, and say goodbye naturally.";

  // 워커에 닿지 못해 판정하지 못한 경우. 워커가 돌려주는 이유 코드(timeout 등)와 구분한다.
  static final String GUARD_WORKER_UNAVAILABLE = "worker_unavailable";

  private static final Executor GUARD_EXECUTOR = Executors.newVirtualThreadPerTaskExecutor();

  // 장면별 상태·의도 규칙을 정하기 전까지 쓰는 자유 대화 상태. 의도는 상태를 바꾸지 않는다.
  private static final WorkerState FREE_TALK =
      new WorkerState(
          "free_talk",
          "Free conversation. The learner can talk about anything with you.",
          List.of(
              new WorkerState.Intent("chat", "Anything the learner says in the conversation.")));

  private final TurnRepository turnRepository;
  private final ConversationLookup conversationLookup;
  private final DialogueContexts dialogueContexts;
  private final ConversationResponses conversationResponses;
  private final NpcAudioReceiver npcAudioReceiver;
  private final AiGateway aiGateway;
  private final MemoryService memoryService;
  private final ReviewCardService reviewCardService;
  private final ReviewDialogue reviewDialogue;
  private final NpcAffinityService npcAffinityService;
  private final TransactionTemplate transactionTemplate;
  private final MissionDialogueService missionDialogueService;
  private final Clock clock;
  private final DialogueRequestLocks requestLocks;
  private final PlacePracticeService placePractice;
  private final EarlyReplies earlyReplies;

  public TurnResponse send(Long userId, Long conversationId, TurnRequest request) {
    return send(userId, conversationId, request, null);
  }

  private void releaseFinalTurnClaim(Long userId, Long conversationId, java.util.UUID requestId) {
    try {
      transactionTemplate.executeWithoutResult(
          status ->
              conversationLookup
                  .ownedConversationForUpdate(userId, conversationId)
                  .releaseFinalTurn(requestId));
    } catch (RuntimeException releaseFailure) {
      log.warn(
          "turn_limit_final_turn_release_failed conversation_id={} request_id={}",
          conversationId,
          requestId,
          releaseFailure);
    }
  }

  /**
   * 발화 전송을 스트리밍으로 받는다. NPC 대답의 글자 조각과 문장 조각 음성을 만들어지는 대로 {@code events}에 넘기고, 처리 순서·저장·결과는 {@link
   * #send(Long, Long, TurnRequest)}와 같다. 같은 요청 ID로 다시 보내 저장된 결과를 돌려줄 때는 조각을 넘기지 않는다.
   */
  public TurnResponse send(
      Long userId, Long conversationId, TurnRequest request, TurnEvents events) {
    try {
      return requestLocks.execute(
          userId, request.requestId(), () -> sendOnce(userId, conversationId, request, events));
    } catch (RuntimeException exception) {
      releaseFinalTurnClaim(userId, conversationId, request.requestId());
      throw exception;
    }
  }

  // events가 null이면 대답을 끝까지 모아 한 번에 응답한다.
  private TurnResponse sendOnce(
      Long userId, Long conversationId, TurnRequest request, TurnEvents events) {
    // 같은 요청 ID로 다시 보내면 턴을 새로 만들지 않고 저장된 결과를 돌려준다.
    Optional<TurnResponse> replayed =
        transactionTemplate.execute(status -> replay(userId, conversationId, request));
    if (replayed.isPresent()) {
      return replayed.get();
    }

    TurnContextSelection selection =
        transactionTemplate.execute(
            status ->
                turnContext(userId, conversationId, request.requestId(), request.speechSpeed()));
    if (selection.rejected()) {
      throw new BusinessException(ErrorCode.CONVERSATION_NOT_ACTIVE);
    }
    TurnContext context = selection.context();
    WorkerTrace trace = new WorkerTrace(request.requestId(), userId, conversationId);
    Optional<MissionDialogueService.Context> mission =
        context.review() == null && context.practice() == null
            ? missionDialogueService.context(userId, context.npc().summary().npcCode())
            : Optional.empty();
    // 이 턴에 판정할 목표. 장소 연습은 이야기 퀘스트 대신 고른 상황의 남은 목표를 판정한다.
    Optional<FirstDayMissionService.Context> goals =
        context.practice() != null
            ? Optional.of(context.practice())
            : mission.map(MissionDialogueService.Context::detail);

    Optional<WorkerState> missionState = goals.map(FirstDayMissionService.Context::state);
    // 경찰관은 미션 목표를 맡지 않은 동안 창구에서 신고를 받는다.
    boolean policeDesk =
        context.review() == null
            && !context.npcOffDuty()
            && missionState.isEmpty()
            && PoliceDesk.serves(context.npc().summary().npcCode());
    // 택시 기사는 미션 목표를 맡지 않은 동안 뒷좌석 승객의 목적지를 듣는다.
    boolean taxiRide =
        context.review() == null
            && !context.npcOffDuty()
            && missionState.isEmpty()
            && TaxiRide.serves(context.npc().summary().npcCode());

    TurnScope scope = new TurnScope(context, goals, policeDesk, taxiRide, selection.finalTurn());
    CompletableFuture<GuardResult> guarding = guardAsync(trace, context, request.text());
    EarlyReply early = startEarlyReply(trace, scope, request.text(), events);
    JudgeResult judge;
    GuardResult guard;
    TurnDecision decision;
    try {
      judge =
          aiGateway.judge(
              trace,
              context.npc().workerNpc(),
              context.review() != null
                  ? reviewDialogue.state(context.review())
                  : missionState.orElse(
                      policeDesk ? PoliceDesk.STATE : taxiRide ? TaxiRide.STATE : FREE_TALK),
              context.history(),
              request.text());
      if (judge.dropped()) {
        // 행동 묘사만 있는 말은 턴으로 세지 않는다. 응답 형식은 [미정]이라 안내 문구를 오류 정보로 돌려준다.
        throw new BusinessException(
            ErrorCode.VALIDATION_FAILED,
            judge.notice() == null ? null : Map.of("notice", judge.notice()));
      }
      guard = guarding.join();
      decision = decide(scope, judge, Boolean.TRUE.equals(guard.abusive()));
    } catch (RuntimeException exception) {
      if (early != null) {
        early.discard();
      }
      throw exception;
    }

    boolean rude = decision.rude();
    boolean wrapUp = decision.wrapUp();
    Optional<PoliceDesk.Dispatch> dispatch = decision.dispatch();
    Optional<TaxiRide.Destination> destination = decision.destination();
    NpcReply reply = reply(trace, context, decision.reply(), events, early);
    String npcAudioKey = npcAudioReceiver.save(trace, reply.audioFullId());

    return transactionTemplate.execute(
        status -> {
          // AI를 기다리는 사이 시간 초과로 다시 보낸 같은 요청이 먼저 저장했을 수 있다.
          Optional<TurnResponse> savedMeanwhile = replay(userId, conversationId, request);
          if (savedMeanwhile.isPresent()) {
            return savedMeanwhile.get();
          }
          Conversation conversation =
              conversationLookup.ownedConversationForUpdate(userId, conversationId);
          // AI를 기다리는 사이 대화가 끝났을 수 있다.
          if (!conversation.isActive()) {
            throw new BusinessException(ErrorCode.CONVERSATION_NOT_ACTIVE);
          }
          int lastSeq = turnRepository.findLastSeq(conversationId);
          Turn userTurn =
              turnRepository.save(
                  Turn.user(
                      conversation,
                      lastSeq + 1,
                      request.requestId(),
                      request.inputType(),
                      judge.text(),
                      request.sttRawText(),
                      judge.english(),
                      guard.abusive(),
                      guard.errorCode(),
                      conversationResponses.writeWords(judge.events())));
          // 복습과 장소 연습은 연습이라 이 NPC와의 호감도에 반영하지 않는다.
          boolean affectsAffinity = !conversation.isReview() && !conversation.isPractice();
          if (rude) {
            conversation.countRude(Conversation.RUDE_LIMIT, clock.instant());
            if (affectsAffinity) {
              npcAffinityService.deductForRudeness(
                  userId, conversation.getNpcId(), guard.affinityDelta());
            }
            // 무례가 쌓여 이 턴으로 NPC가 대화를 끝냈다. 무례한 말은 대화 본문이라 남기지 않는다.
            if (!conversation.isActive()) {
              log.info(
                  "conversation_ended conversation_id={} status={} end_reason={}",
                  conversationId,
                  conversation.getStatus(),
                  conversation.getEndReason());
            }
          } else if (affectsAffinity) {
            npcAffinityService.raiseForEvents(
                userId, conversation.getNpcId(), judge.affinityDelta());
          }
          Turn npcTurn =
              turnRepository.save(
                  Turn.npc(conversation, lastSeq + 2, reply.text(), npcAudioKey, clock.instant()));
          npcTurn.attachSpeechPhrases(
              conversationResponses.writeSpeechPhrases(reply.speechPhrases()));
          if (wrapUp) {
            // 용건이 끝났거나 학습자가 작별해 NPC가 마무리 대사로 대화를 끝냈다.
            conversation.endByNpc(clock.instant());
            log.info(
                "conversation_ended conversation_id={} status={} end_reason={}",
                conversationId,
                conversation.getStatus(),
                conversation.getEndReason());
          }
          if (conversation.isActive()
              && turnRepository.countByConversationIdAndSpeaker(conversationId, Speaker.USER)
                  >= Conversation.TURN_LIMIT) {
            conversation.endByTurnLimit(clock.instant());
            log.info(
                "conversation_ended conversation_id={} status={} end_reason={}",
                conversationId,
                conversation.getStatus(),
                conversation.getEndReason());
          }
          if (!rude) {
            mission.ifPresent(
                value ->
                    missionDialogueService.accept(
                        userId, context.npc().summary().npcCode(), value, judge.intent()));
            if (context.practice() != null) {
              placePractice.accept(conversation, judge.intent());
            }
          }
          dispatch.ifPresent(value -> userTurn.recordPoliceDispatch(value.placeCode()));
          destination.ifPresent(value -> userTurn.recordTaxiDestination(value.placeCode()));
          return response(conversation, userTurn, npcTurn);
        });
  }

  // 판정 결과로 무례 처리·출동·목적지·마무리와 이번 NPC 대답 요청을 정한다.
  private TurnDecision decide(TurnScope scope, JudgeResult judge, boolean rude) {
    TurnContext context = scope.context();
    boolean leaving = rude && context.rudeCount() + 1 >= Conversation.RUDE_LIMIT;
    Optional<PoliceDesk.Dispatch> dispatch =
        scope.policeDesk() && !rude ? PoliceDesk.dispatch(judge) : Optional.empty();
    Optional<TaxiRide.Destination> destination =
        scope.taxiRide() && !rude ? TaxiRide.destination(judge) : Optional.empty();
    // 출동하는 경찰관은 직접 순찰차로 가야 하므로 이 턴에 대화를 마친다.
    // 택시는 대화가 끝나면 승객이 내리므로 작별·용건 끝냄으로 마치지 않고, 도착하면 Unity가 대화를 마친다.
    boolean wrapUp =
        dispatch.isPresent() || (!scope.taxiRide() && WrapUp.ends(rude, scope.goals(), judge));
    String instruction =
        rude
            ? (leaving ? RUDE_LEAVE : RUDE_WARNING)
            : scope.goals().map(detail -> detail.replyInstruction(judge.intent())).orElse("");
    if (context.review() != null) {
      instruction = join(reviewDialogue.instruction(context.review()), instruction);
    }
    if (scope.policeDesk() && !rude) {
      instruction = join(instruction, PoliceDesk.instruction(judge, dispatch));
    }
    if (scope.taxiRide() && !rude) {
      instruction = join(instruction, TaxiRide.instruction(judge, destination));
    }
    if (wrapUp) {
      instruction = WrapUp.instruction(instruction);
    }
    if (scope.finalTurn()) {
      instruction =
          instruction.isBlank() ? TURN_LIMIT_WRAP_UP : instruction + " " + TURN_LIMIT_WRAP_UP;
    }
    String reaction = DialogueReaction.select(rude, context.rudeCount(), judge.events());
    return new TurnDecision(
        rude, dispatch, destination, wrapUp, new ReplyRequest(judge.text(), instruction, reaction));
  }

  private static String join(String first, String second) {
    return first.isEmpty() ? second : first + " " + second;
  }

  /**
   * 판정 결과가 대답 지시문에 들어가지 않는 대화는 무례·대화 사건이 없다고 보고 판정과 동시에 대답을 시작한다. 판정 결과가 지시문에 들어가는 대화(미션·장소 연습 목표,
   * 경찰 창구, 택시)와 조각 없이 한 번에 받는 요청은 판정을 기다린다.
   */
  private EarlyReply startEarlyReply(
      WorkerTrace trace, TurnScope scope, String text, TurnEvents events) {
    if (events == null || !earlyReplies.enabled() || scope.judgeShapesInstruction()) {
      return null;
    }
    ReplyRequest assumed = decide(scope, withoutEvents(text), false).reply();
    return earlyReplies.start(
        assumed, listener -> reply(trace, scope.context(), assumed, listener));
  }

  // 무례·대화 사건이 없다고 본 판정. 먼저 시작할 대답 요청도 판정 뒤와 같은 규칙(decide)으로 만든다.
  private static JudgeResult withoutEvents(String text) {
    return new JudgeResult(text, false, null, null, List.of(), null, List.of(), null);
  }

  // 먼저 시작한 대답이 판정 뒤에 정한 요청과 같으면 그 대답을 쓰고, 다르면 버리고 다시 부른다.
  private NpcReply reply(
      WorkerTrace trace,
      TurnContext context,
      ReplyRequest request,
      TurnEvents events,
      EarlyReply early) {
    NpcReplyListener listener = events == null ? NpcReplyListener.NONE : relayTo(trace, events);
    if (early == null) {
      return reply(trace, context, request, listener);
    }
    if (early.request().equals(request)) {
      log.info("npc_early_reply result={}", "kept");
      return early.keep(listener);
    }
    early.discard();
    log.info(
        "npc_early_reply result={} changed={}",
        "discarded",
        String.join(",", early.request().changedFrom(request)));
    return reply(trace, context, request, listener);
  }

  private NpcReply reply(
      WorkerTrace trace, TurnContext context, ReplyRequest request, NpcReplyListener listener) {
    if (request.reaction() != null) {
      return aiGateway.replyReacting(
          trace,
          context.npc().workerNpc(),
          context.memory(),
          context.history(),
          request.text(),
          request.instruction(),
          context.level(),
          context.learner(),
          listener,
          request.reaction());
    }
    if (listener == NpcReplyListener.NONE) {
      return aiGateway.reply(
          trace,
          context.npc().workerNpc(),
          context.memory(),
          context.history(),
          request.text(),
          request.instruction(),
          context.level(),
          context.learner());
    }
    return aiGateway.reply(
        trace,
        context.npc().workerNpc(),
        context.memory(),
        context.history(),
        request.text(),
        request.instruction(),
        context.level(),
        context.learner(),
        listener);
  }

  // 조각 음성은 워커 메모리에서 바로 받아 넘긴다(같은 서버라 조각마다 수 ms). 받지 못한 조각은 자막만 보여 주도록 음성 없이 넘긴다.
  private NpcReplyListener relayTo(WorkerTrace trace, TurnEvents events) {
    return new NpcReplyListener() {
      @Override
      public void onDelta(String text) {
        events.delta(text);
      }

      @Override
      public void onChunk(NpcReplyChunk chunk) {
        events.audio(chunk.seq(), chunk.text(), chunkAudio(trace, chunk.audioId()));
      }
    };
  }

  private byte[] chunkAudio(WorkerTrace trace, String audioId) {
    if (audioId == null) {
      return null;
    }
    try {
      return aiGateway.audio(trace, audioId);
    } catch (BusinessException exception) {
      log.warn("npc_chunk_audio_download_failed code={}", exception.getErrorCode());
      return null;
    }
  }

  // 의도 판정과 같은 문장으로 동시에 부른다. 워커에 닿지 못해도 턴을 막지 않고 판정하지 못한 것으로 둔다.
  private CompletableFuture<GuardResult> guardAsync(
      WorkerTrace trace, TurnContext context, String text) {
    return CompletableFuture.supplyAsync(
        () -> {
          try {
            return aiGateway.guard(trace, context.npc().workerNpc(), context.history(), text);
          } catch (BusinessException exception) {
            return new GuardResult(
                new GuardResult.Guard(null, GUARD_WORKER_UNAVAILABLE), List.of());
          }
        },
        LogContext.propagating(GUARD_EXECUTOR));
  }

  private Optional<TurnResponse> replay(Long userId, Long conversationId, TurnRequest request) {
    return turnRepository
        .findByRequestId(request.requestId())
        .map(
            userTurn -> {
              Conversation conversation = userTurn.getConversation();
              if (!conversation.getId().equals(conversationId)
                  || !conversation.getUserId().equals(userId)) {
                throw new BusinessException(ErrorCode.REQUEST_ID_CONFLICT);
              }
              Turn npcTurn =
                  turnRepository
                      .findByConversationIdAndSeq(conversationId, userTurn.getSeq() + 1)
                      .orElseThrow(() -> new BusinessException(ErrorCode.INTERNAL_ERROR));
              return response(conversation, userTurn, npcTurn);
            });
  }

  private TurnContextSelection turnContext(
      Long userId, Long conversationId, java.util.UUID requestId, double speechSpeed) {
    Conversation conversation =
        conversationLookup.ownedConversationForUpdate(userId, conversationId);
    if (!conversation.isActive()) {
      throw new BusinessException(ErrorCode.CONVERSATION_NOT_ACTIVE);
    }
    long userTurns = turnRepository.countByConversationIdAndSpeaker(conversationId, Speaker.USER);
    if (userTurns >= Conversation.TURN_LIMIT) {
      conversation.endByTurnLimit(clock.instant());
      return new TurnContextSelection(null, true, false);
    }
    boolean finalTurn = userTurns == Conversation.TURN_LIMIT - 1;
    if (finalTurn && !conversation.claimFinalTurn(requestId)) {
      return new TurnContextSelection(null, true, false);
    }
    return new TurnContextSelection(
        new TurnContext(
            dialogueContexts.npcOf(conversation),
            dialogueContexts.learner(userId, speechSpeed),
            dialogueContexts.history(conversationId),
            conversation.getLevelApplied(),
            conversation.isReview() || conversation.isPractice()
                ? null
                : memoryService.memoryFor(userId, conversation.getNpcId()),
            conversation.getRudeCount(),
            conversation.isNpcOffDuty(),
            conversation.isReview()
                ? reviewCardService.detail(userId, conversation.getReviewCardId())
                : null,
            placePractice.context(conversation).orElse(null)),
        false,
        finalTurn);
  }

  private TurnResponse response(Conversation conversation, Turn userTurn, Turn npcTurn) {
    return new TurnResponse(
        conversationResponses.userTurn(userTurn),
        conversationResponses.npcTurn(npcTurn),
        conversationResponses.state(conversation),
        List.of(),
        userTurn.isPoliceDispatch()
            ? new PoliceDispatchResponse(userTurn.getPoliceDispatchPlaceCode())
            : null,
        userTurn.getTaxiDestinationCode() == null
            ? null
            : new TaxiDestinationResponse(userTurn.getTaxiDestinationCode()));
  }

  /**
   * @param learner 대답 상대인 학습자. NPC가 캐릭터에 맞는 호칭을 쓰게 한다
   * @param memory 이 NPC와의 이전 대화 기억. 처음 만나면 null
   * @param rudeCount 이 턴 전까지 무례로 센 말 수
   * @param practice 장소 연습 대화에서 이 턴 전까지 남은 목표와 직원 지시. 장소 연습이 아니면 null
   */
  private record TurnContext(
      NpcContext npc,
      WorkerLearner learner,
      List<WorkerLine> history,
      int level,
      WorkerMemory memory,
      int rudeCount,
      boolean npcOffDuty,
      ReviewCardResponse review,
      FirstDayMissionService.Context practice) {}

  private record TurnContextSelection(TurnContext context, boolean rejected, boolean finalTurn) {}

  /**
   * 이번 턴에서 판정 전에 정해진 것.
   *
   * @param goals 이 턴에 판정할 목표(이야기 퀘스트나 장소 연습). 없으면 빈 값
   * @param finalTurn 대화의 마지막 턴이라 대답에서 마무리해야 하는지
   */
  private record TurnScope(
      TurnContext context,
      Optional<FirstDayMissionService.Context> goals,
      boolean policeDesk,
      boolean taxiRide,
      boolean finalTurn) {

    // 판정한 의도·목적지가 대답 지시문에 들어가는 대화인지
    boolean judgeShapesInstruction() {
      return goals.isPresent() || policeDesk || taxiRide;
    }
  }

  /** 판정 결과로 정한 이번 턴의 처리. */
  private record TurnDecision(
      boolean rude,
      Optional<PoliceDesk.Dispatch> dispatch,
      Optional<TaxiRide.Destination> destination,
      boolean wrapUp,
      ReplyRequest reply) {}
}
