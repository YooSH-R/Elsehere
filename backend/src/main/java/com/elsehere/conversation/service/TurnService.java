package com.elsehere.conversation.service;

import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.client.GuardResult;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.WorkerLine;
import com.elsehere.ai.client.WorkerMemory;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.entity.AiRequestKind;
import com.elsehere.ai.entity.AiRequestStatus;
import com.elsehere.ai.service.AiRequestRecorder;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.TurnRequest;
import com.elsehere.conversation.dto.TurnResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.memory.service.MemoryService;
import com.elsehere.memory.service.NpcAffinityService;
import com.elsehere.mission.service.FirstDayMissionService;
import java.time.Clock;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Executor;
import java.util.concurrent.Executors;
import lombok.RequiredArgsConstructor;
import org.slf4j.MDC;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

/**
 * 발화 전송(CONV-03). 의도·번역 판정(/v1/judge)과 무례 판정(/v1/guard)을 함께 부른 뒤 → NPC 대답(/v1/npc/reply) → 턴 전체 음성
 * 저장 순서로 처리한다.
 *
 * <p>첫날 연계 미션을 시작한 사용자는 현재 단계의 의도를 판정한다. 성공한 대답과 턴을 저장할 때만 미션을 진행한다. 무례한 말은 미션에 반영하지 않고 횟수를 세며 그
 * NPC의 호감도를 깎고, 한도에 닿으면 NPC가 대화를 끝낸다(docs/api/rudeness-handling.md). 사용자 AI 한도 예약·차감은 별도 연동 영역이다.
 */
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
  private final ConversationSupport support;
  private final AiWorkerClient aiWorkerClient;
  private final AiRequestRecorder aiRequestRecorder;
  private final MemoryService memoryService;
  private final NpcAffinityService npcAffinityService;
  private final TransactionTemplate transactionTemplate;
  private final FirstDayMissionService firstDayMissionService;
  private final Clock clock;

  public TurnResponse send(Long userId, Long conversationId, TurnRequest request) {
    // 같은 요청 ID로 다시 보내면 턴을 새로 만들지 않고 저장된 결과를 돌려준다.
    Optional<TurnResponse> replayed =
        transactionTemplate.execute(status -> replay(userId, conversationId, request));
    if (replayed.isPresent()) {
      return replayed.get();
    }

    TurnContext context =
        transactionTemplate.execute(status -> turnContext(userId, conversationId));
    WorkerTrace trace = new WorkerTrace(request.requestId(), userId, conversationId);
    Optional<FirstDayMissionService.Context> mission =
        firstDayMissionService.context(userId, context.npc().summary().npcCode());

    CompletableFuture<GuardResult> guarding = guardAsync(trace, context, request.text());
    JudgeResult judge =
        aiWorkerClient.judge(
            trace,
            context.npc().workerNpc(),
            mission.map(FirstDayMissionService.Context::state).orElse(FREE_TALK),
            context.history(),
            request.text());
    aiRequestRecorder.record(trace, AiRequestKind.JUDGE, judge.usage(), AiRequestStatus.OK);
    if (judge.dropped()) {
      // 행동 묘사만 있는 말은 턴으로 세지 않는다. 응답 형식은 [미정]이라 안내 문구를 오류 정보로 돌려준다.
      throw new BusinessException(
          ErrorCode.VALIDATION_FAILED,
          judge.notice() == null ? null : Map.of("notice", judge.notice()));
    }

    GuardResult guard = guarding.join();
    boolean rude = Boolean.TRUE.equals(guard.abusive());
    boolean leaving = rude && context.rudeCount() + 1 >= ConversationSupport.RUDE_LIMIT;
    String instruction =
        rude
            ? (leaving ? RUDE_LEAVE : RUDE_WARNING)
            : mission.map(value -> value.replyInstruction(judge.intent())).orElse("");

    NpcReply reply =
        aiWorkerClient.reply(
            trace,
            context.npc().workerNpc(),
            context.memory(),
            context.history(),
            judge.text(),
            instruction,
            context.level(),
            !leaving);
    aiRequestRecorder.record(
        trace,
        AiRequestKind.NPC,
        reply.usage(),
        reply.succeeded() ? AiRequestStatus.OK : AiRequestStatus.FAILED);
    if (!reply.succeeded()) {
      throw new BusinessException(AiWorkerClient.toErrorCode(reply.errorCode()));
    }
    String npcAudioKey = support.saveNpcAudio(trace, reply.audioFullId());

    return transactionTemplate.execute(
        status -> {
          Conversation conversation = support.ownedConversation(userId, conversationId);
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
                      guard.errorCode()));
          if (rude) {
            conversation.countRude(ConversationSupport.RUDE_LIMIT, clock.instant());
            npcAffinityService.deductForRudeness(
                userId, conversation.getNpcId(), guard.affinityDelta());
          }
          Turn npcTurn =
              turnRepository.save(
                  Turn.npc(
                      conversation,
                      lastSeq + 2,
                      reply.text(),
                      support.writeWords(reply.suggestWords()),
                      npcAudioKey));
          if (!rude) {
            mission.ifPresent(
                value ->
                    firstDayMissionService.accept(
                        userId, value.stage(), context.npc().summary().npcCode(), judge.intent()));
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
            GuardResult guard =
                aiWorkerClient.guard(trace, context.npc().workerNpc(), context.history(), text);
            aiRequestRecorder.record(trace, AiRequestKind.GUARD, guard.usage(), AiRequestStatus.OK);
            return guard;
          } catch (BusinessException exception) {
            aiRequestRecorder.record(trace, AiRequestKind.GUARD, List.of(), AiRequestStatus.FAILED);
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
    Conversation conversation = support.ownedConversation(userId, conversationId);
    if (!conversation.isActive()) {
      throw new BusinessException(ErrorCode.CONVERSATION_NOT_ACTIVE);
    }
    return new TurnContext(
        support.npcById(conversation.getNpcId()),
        support.history(conversationId),
        conversation.getLevelApplied(),
        memoryService.memoryFor(userId, conversation.getNpcId()),
        conversation.getRudeCount());
  }

  private TurnResponse response(Conversation conversation, Turn userTurn, Turn npcTurn) {
    return new TurnResponse(
        support.userTurn(userTurn),
        support.npcTurn(npcTurn),
        support.state(conversation),
        List.of());
  }

  /**
   * @param memory 이 NPC와의 이전 대화 기억. 처음 만나면 null
   * @param rudeCount 이 턴 전까지 무례로 센 말 수
   */
  private record TurnContext(
      NpcContext npc, List<WorkerLine> history, int level, WorkerMemory memory, int rudeCount) {}
}
