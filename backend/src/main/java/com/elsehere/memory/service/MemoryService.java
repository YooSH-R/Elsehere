package com.elsehere.memory.service;

import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.client.MemoryCardResult;
import com.elsehere.ai.client.WorkerLine;
import com.elsehere.ai.client.WorkerMemory;
import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.entity.AiRequestKind;
import com.elsehere.ai.entity.AiRequestStatus;
import com.elsehere.ai.service.AiRequestRecorder;
import com.elsehere.ai.service.WorkerNpcs;
import com.elsehere.common.error.BusinessException;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.ConversationStatus;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.memory.entity.UserNpcRelation;
import com.elsehere.memory.entity.UserNpcRelationId;
import com.elsehere.memory.repository.UserNpcRelationRepository;
import com.elsehere.world.repository.NpcRepository;
import java.time.Clock;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.scheduling.annotation.Async;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;
import tools.jackson.core.type.TypeReference;
import tools.jackson.databind.json.JsonMapper;

/**
 * NPC 기억 카드. 대화가 끝나면 방문 수를 세고 AI 워커(/v1/memory)로 카드를 새로 만들어 통째로 바꾼다. 다음 대화의 NPC 대답에는 저장된 최신 카드를
 * 넘긴다. 연결 기준: docs/api/npc-memory-integration.md
 *
 * <p>카드 갱신은 대화 종료·피드백 응답을 막지 않도록 따로 실행한다. 실패하면 이전 카드를 그대로 둔다. 사용자 AI 한도 예약은 아직 하지 않는다.
 */
@Slf4j
@Service
@RequiredArgsConstructor
public class MemoryService {

  // /v1/memory가 받는 대화 줄 수 상한
  private static final int HISTORY_LIMIT = 200;
  private static final TypeReference<Map<String, Object>> CARD = new TypeReference<>() {};

  private final UserNpcRelationRepository userNpcRelationRepository;
  private final ConversationRepository conversationRepository;
  private final TurnRepository turnRepository;
  private final NpcRepository npcRepository;
  private final WorkerNpcs workerNpcs;
  private final AiWorkerClient aiWorkerClient;
  private final AiRequestRecorder aiRequestRecorder;
  private final TransactionTemplate transactionTemplate;
  private final JsonMapper jsonMapper;
  private final Clock clock;

  /** 다음 대화의 NPC 대답에 넘길 기억. 끝낸 대화가 없으면 null이다. 호출하는 쪽의 트랜잭션 안에서 부른다. */
  public WorkerMemory memoryFor(Long userId, Long npcId) {
    return userNpcRelationRepository
        .findById(new UserNpcRelationId(userId, npcId))
        .filter(relation -> relation.getTalkCount() > 0)
        .map(
            relation ->
                new WorkerMemory(relation.getTalkCount(), null, readCard(relation.getMemoryCard())))
        .orElse(null);
  }

  /**
   * 대화가 끝난 뒤 방문 수를 세고 기억 카드를 갱신한다. 같은 대화는 한 번만 반영하며, 사용자 발화가 없던 대화는 세지 않는다.
   *
   * @param requestId 대화 종료(CONV-08) 요청 ID. AI 호출 기록에 쓴다
   */
  @Async
  public void updateAfterConversation(Long userId, Long conversationId, UUID requestId) {
    try {
      update(userId, conversationId, requestId);
    } catch (RuntimeException exception) {
      log.warn(
          "NPC 기억을 갱신하지 못했습니다 conversationId={} type={}",
          conversationId,
          exception.getClass().getSimpleName());
    }
  }

  private void update(Long userId, Long conversationId, UUID requestId) {
    MemoryJob job = transactionTemplate.execute(status -> prepare(userId, conversationId));
    if (job == null) {
      return;
    }
    WorkerTrace trace = new WorkerTrace(requestId, userId, conversationId);
    MemoryCardResult result;
    try {
      result =
          aiWorkerClient.memoryCard(
              trace, job.npc(), job.outcome(), job.history(), job.previousCard());
    } catch (BusinessException exception) {
      // 429·502·503 등. 오류 응답에는 카드가 없으므로 빈 카드로 덮지 않고 이전 카드를 그대로 둔다.
      aiRequestRecorder.record(trace, AiRequestKind.MEMORY, List.of(), AiRequestStatus.FAILED);
      log.warn("기억 카드를 갱신하지 못해 이전 카드를 유지합니다 code={}", exception.getErrorCode());
      return;
    }
    aiRequestRecorder.record(trace, AiRequestKind.MEMORY, result.usage(), AiRequestStatus.OK);
    transactionTemplate.executeWithoutResult(
        status ->
            userNpcRelationRepository
                .findById(job.relationId())
                .ifPresent(relation -> relation.replaceMemoryCard(result.cardJson())));
  }

  // 방문 수를 먼저 세고 카드 생성 재료를 모은다. 이미 반영한 대화거나 사용자 발화가 없으면 null이다.
  private MemoryJob prepare(Long userId, Long conversationId) {
    Conversation conversation =
        conversationRepository.findByIdAndUserId(conversationId, userId).orElse(null);
    if (conversation == null || conversation.isActive()) {
      return null;
    }
    List<Turn> turns = turnRepository.findAllByConversationIdOrderBySeqAsc(conversationId);
    if (turns.stream().noneMatch(turn -> turn.getSpeaker() == Speaker.USER)) {
      return null;
    }

    UserNpcRelationId relationId = new UserNpcRelationId(userId, conversation.getNpcId());
    UserNpcRelation relation =
        userNpcRelationRepository
            .findById(relationId)
            .orElseGet(
                () ->
                    userNpcRelationRepository.save(
                        UserNpcRelation.start(userId, conversation.getNpcId())));
    boolean firstTime =
        relation.recordVisit(
            conversationId,
            conversation.getEndedAt() != null ? conversation.getEndedAt() : clock.instant());
    if (!firstTime) {
      return null;
    }

    WorkerNpc npc =
        npcRepository.findById(conversation.getNpcId()).map(workerNpcs::of).orElse(null);
    if (npc == null) {
      return null;
    }
    return new MemoryJob(
        relationId, npc, outcome(conversation), history(turns), readCard(relation.getMemoryCard()));
  }

  // 무례로 확정된 말만 rude로 보낸다(판정 실패는 false). 워커가 무례 원문을 표식으로 바꿔 전달한다.
  private static List<WorkerLine> history(List<Turn> turns) {
    return turns.subList(Math.max(0, turns.size() - HISTORY_LIMIT), turns.size()).stream()
        .map(
            turn ->
                turn.getSpeaker() == Speaker.NPC
                    ? WorkerLine.npc(turn.getText())
                    : new WorkerLine(
                        "me",
                        turn.getText(),
                        turn.getTranslationEn(),
                        Boolean.TRUE.equals(turn.getGuardAbusive())))
        .toList();
  }

  // 대화 결과 한 줄. 서버가 확정한 사실만 쓰고 추측하지 않는다.
  private static String outcome(Conversation conversation) {
    if (conversation.getStatus() != ConversationStatus.ENDED_BY_NPC) {
      return "The learner ended the conversation normally.";
    }
    if (conversation.getEndReason() == ConversationEndReason.RUDE) {
      return "The NPC ended the conversation because the learner kept being rude.";
    }
    return "The NPC ended the conversation.";
  }

  private Map<String, Object> readCard(String json) {
    return json == null ? null : jsonMapper.readValue(json, CARD);
  }

  private record MemoryJob(
      UserNpcRelationId relationId,
      WorkerNpc npc,
      String outcome,
      List<WorkerLine> history,
      Map<String, Object> previousCard) {}
}
