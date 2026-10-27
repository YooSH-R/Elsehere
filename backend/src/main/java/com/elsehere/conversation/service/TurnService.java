package com.elsehere.conversation.service;

import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.NpcReply;
import com.elsehere.ai.client.WorkerLine;
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
import java.util.List;
import java.util.Map;
import java.util.Optional;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

/**
 * 발화 전송(CONV-03). 의도·번역 판정(/v1/judge) → NPC 대답(/v1/npc/reply) → 턴 전체 음성 저장 순서로 처리한다.
 *
 * <p>아직 하지 않는 것: 무례 판정(/v1/guard)과 누적·종료, 장면별 상태·의도 규칙과 미션 판정, 사용자 AI 한도 예약·차감. 장면 규칙이 없어 모든 NPC를
 * 자유 대화 상태 하나로 판정한다.
 */
@Service
@RequiredArgsConstructor
public class TurnService {

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
  private final TransactionTemplate transactionTemplate;

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

    JudgeResult judge =
        aiWorkerClient.judge(
            trace, context.npc().workerNpc(), FREE_TALK, context.history(), request.text());
    aiRequestRecorder.record(trace, AiRequestKind.JUDGE, judge.usage(), AiRequestStatus.OK);
    if (judge.dropped()) {
      // 행동 묘사만 있는 말은 턴으로 세지 않는다. 응답 형식은 [미정]이라 안내 문구를 오류 정보로 돌려준다.
      throw new BusinessException(
          ErrorCode.VALIDATION_FAILED,
          judge.notice() == null ? null : Map.of("notice", judge.notice()));
    }

    NpcReply reply =
        aiWorkerClient.reply(
            trace, context.npc().workerNpc(), context.history(), judge.text(), "", context.level());
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
                      judge.english()));
          Turn npcTurn =
              turnRepository.save(
                  Turn.npc(
                      conversation,
                      lastSeq + 2,
                      reply.text(),
                      support.writeWords(reply.suggestWords()),
                      npcAudioKey));
          return response(conversation, userTurn, npcTurn);
        });
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
        conversation.getLevelApplied());
  }

  private TurnResponse response(Conversation conversation, Turn userTurn, Turn npcTurn) {
    return new TurnResponse(
        support.userTurn(userTurn),
        support.npcTurn(npcTurn),
        support.state(conversation),
        List.of());
  }

  private record TurnContext(NpcContext npc, List<WorkerLine> history, int level) {}
}
