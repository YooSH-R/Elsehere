package com.elsehere.dialogue.service;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.GuardResult;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.WorkerLearner;
import com.elsehere.ai.client.WorkerLine;
import com.elsehere.ai.client.WorkerMemory;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.conversation.service.ConversationLookup;
import com.elsehere.dialogue.dto.TurnRequest;
import com.elsehere.dialogue.dto.TurnResponse;
import com.elsehere.memory.service.MemoryService;
import com.elsehere.memory.service.NpcAffinityService;
import com.elsehere.mission.service.MissionDialogueService;
import java.time.Clock;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Executor;
import java.util.concurrent.Executors;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.slf4j.MDC;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

/**
 * 발화 전송(CONV-03). 의도·번역 판정(/v1/judge)과 무례 판정(/v1/guard)을 함께 부른 뒤 → NPC 대답(/v1/npc/reply) → 턴 전체 음성
 * 저장 순서로 처리한다.
 *
 * <p>첫날 연계 미션을 시작한 사용자는 현재 단계의 의도를 판정한다. 성공한 대답과 턴을 저장할 때만 미션을 진행한다. 무례한 말은 미션에 반영하지 않고 횟수를 세며 그
 * NPC의 호감도를 깎고, 한도에 닿으면 NPC가 대화를 끝낸다(docs/api/rudeness-handling.md). 무례가 아닌 말은 의도 판정이 고른 대화
 * 사건(칭찬·인사 등)만큼 호감도를 올린다(docs/api/ai-worker.md 2.2절). 미션 목표를 마친 턴이나 학습자가 작별·용건 끝냄을 말한 턴에는 NPC가 마무리
 * 대사로 대화를 끝낸다({@link WrapUp}). 사용자 AI 한도 예약·차감은 별도 연동 영역이다.
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
  private final NpcAffinityService npcAffinityService;
  private final TransactionTemplate transactionTemplate;
  private final MissionDialogueService missionDialogueService;
  private final Clock clock;
  private final DialogueRequestLocks requestLocks;

  public TurnResponse send(Long userId, Long conversationId, TurnRequest request) {
    return requestLocks.execute(
        userId, request.requestId(), () -> sendOnce(userId, conversationId, request));
  }

  private TurnResponse sendOnce(Long userId, Long conversationId, TurnRequest request) {
    // 같은 요청 ID로 다시 보내면 턴을 새로 만들지 않고 저장된 결과를 돌려준다.
    Optional<TurnResponse> replayed =
        transactionTemplate.execute(status -> replay(userId, conversationId, request));
    if (replayed.isPresent()) {
      return replayed.get();
    }

    TurnContext context =
        transactionTemplate.execute(status -> turnContext(userId, conversationId));
    WorkerTrace trace = new WorkerTrace(request.requestId(), userId, conversationId);
    Optional<MissionDialogueService.Context> mission =
        missionDialogueService.context(userId, context.npc().summary().npcCode());

    CompletableFuture<GuardResult> guarding = guardAsync(trace, context, request.text());
    JudgeResult judge =
        aiGateway.judge(
            trace,
            context.npc().workerNpc(),
            mission.map(value -> value.detail().state()).orElse(FREE_TALK),
            context.history(),
            request.text());
    if (judge.dropped()) {
      // 행동 묘사만 있는 말은 턴으로 세지 않는다. 응답 형식은 [미정]이라 안내 문구를 오류 정보로 돌려준다.
      throw new BusinessException(
          ErrorCode.VALIDATION_FAILED,
          judge.notice() == null ? null : Map.of("notice", judge.notice()));
    }

    GuardResult guard = guarding.join();
    boolean rude = Boolean.TRUE.equals(guard.abusive());
    boolean leaving = rude && context.rudeCount() + 1 >= Conversation.RUDE_LIMIT;
    boolean wrapUp = WrapUp.ends(rude, mission, judge);
    String instruction =
        rude
            ? (leaving ? RUDE_LEAVE : RUDE_WARNING)
            : mission.map(value -> value.detail().replyInstruction(judge.intent())).orElse("");
    if (wrapUp) {
      instruction = WrapUp.instruction(instruction);
    }

    NpcReply reply =
        aiGateway.reply(
            trace,
            context.npc().workerNpc(),
            context.memory(),
            context.history(),
            judge.text(),
            instruction,
            context.level(),
            context.learner());
    String npcAudioKey = npcAudioReceiver.save(trace, reply.audioFullId());

    return transactionTemplate.execute(
        status -> {
          // AI를 기다리는 사이 시간 초과로 다시 보낸 같은 요청이 먼저 저장했을 수 있다.
          Optional<TurnResponse> savedMeanwhile = replay(userId, conversationId, request);
          if (savedMeanwhile.isPresent()) {
            return savedMeanwhile.get();
          }
          Conversation conversation = conversationLookup.ownedConversation(userId, conversationId);
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
          if (rude) {
            conversation.countRude(Conversation.RUDE_LIMIT, clock.instant());
            npcAffinityService.deductForRudeness(
                userId, conversation.getNpcId(), guard.affinityDelta());
            // 무례가 쌓여 이 턴으로 NPC가 대화를 끝냈다. 무례한 말은 대화 본문이라 남기지 않는다.
            if (!conversation.isActive()) {
              log.info(
                  "conversation_ended conversation_id={} status={} end_reason={}",
                  conversationId,
                  conversation.getStatus(),
                  conversation.getEndReason());
            }
          } else {
            npcAffinityService.raiseForEvents(
                userId, conversation.getNpcId(), judge.affinityDelta());
          }
          Turn npcTurn =
              turnRepository.save(
                  Turn.npc(conversation, lastSeq + 2, reply.text(), npcAudioKey, clock.instant()));
          if (wrapUp) {
            // 용건이 끝났거나 학습자가 작별해 NPC가 마무리 대사로 대화를 끝냈다.
            conversation.endByNpc(clock.instant());
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
          }
          return response(conversation, userTurn, npcTurn);
        });
  }

  // 의도 판정과 같은 문장으로 동시에 부른다. 워커에 닿지 못해도 턴을 막지 않고 판정하지 못한 것으로 둔다.
  private CompletableFuture<GuardResult> guardAsync(
      WorkerTrace trace, TurnContext context, String text) {
    Map<String, String> logContext = MDC.getCopyOfContextMap();
    return CompletableFuture.supplyAsync(
        () -> {
          if (logContext != null) {
            MDC.setContextMap(logContext);
          }
          try {
            return aiGateway.guard(trace, context.npc().workerNpc(), context.history(), text);
          } catch (BusinessException exception) {
            return new GuardResult(
                new GuardResult.Guard(null, GUARD_WORKER_UNAVAILABLE), List.of());
          } finally {
            MDC.clear();
          }
        },
        GUARD_EXECUTOR);
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

  private TurnContext turnContext(Long userId, Long conversationId) {
    Conversation conversation = conversationLookup.ownedConversation(userId, conversationId);
    if (!conversation.isActive()) {
      throw new BusinessException(ErrorCode.CONVERSATION_NOT_ACTIVE);
    }
    return new TurnContext(
        dialogueContexts.npcOf(conversation),
        dialogueContexts.learner(userId),
        dialogueContexts.history(conversationId),
        conversation.getLevelApplied(),
        memoryService.memoryFor(userId, conversation.getNpcId()),
        conversation.getRudeCount());
  }

  private TurnResponse response(Conversation conversation, Turn userTurn, Turn npcTurn) {
    return new TurnResponse(
        conversationResponses.userTurn(userTurn),
        conversationResponses.npcTurn(npcTurn),
        conversationResponses.state(conversation),
        List.of());
  }

  /**
   * @param learner 대답 상대인 학습자. NPC가 캐릭터에 맞는 호칭을 쓰게 한다
   * @param memory 이 NPC와의 이전 대화 기억. 처음 만나면 null
   * @param rudeCount 이 턴 전까지 무례로 센 말 수
   */
  private record TurnContext(
      NpcContext npc,
      WorkerLearner learner,
      List<WorkerLine> history,
      int level,
      WorkerMemory memory,
      int rudeCount) {}
}
