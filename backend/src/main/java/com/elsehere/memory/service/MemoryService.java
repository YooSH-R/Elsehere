package com.elsehere.memory.service;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.MemoryCardResult;
import com.elsehere.ai.client.RelationshipChange;
import com.elsehere.ai.client.WorkerLine;
import com.elsehere.ai.client.WorkerMemory;
import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.ai.client.WorkerRelationship;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.service.WorkerLines;
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
import com.elsehere.world.service.GameClockService;
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
 * NPC 기억 카드와 관계. 대화가 끝나면 방문 수를 세고 AI 워커(/v1/memory)로 카드를 새로 만들어 통째로 바꾸며, 관계
 * 갱신(/v1/npc/relationship)이 돌려준 알아봄 변화량을 더한다. 호감은 대화 중에 발화마다 바꾼다(TurnService). 다음 대화의 NPC 대답에는 저장된
 * 최신 카드와 관계를 넘긴다. 연결 기준: docs/api/npc-memory-integration.md, docs/api/ai-worker.md 2.7.2절
 *
 * <p>카드·관계 갱신은 대화 종료·피드백 응답을 막지 않도록 따로 실행하고, 둘 중 하나가 실패해도 다른 하나는 반영한다. 실패하면 이전 값을 그대로 둔다. 관계 갱신은
 * 모델을 부르지 않아 AI 호출 기록을 남기지 않는다. 사용자 AI 한도 예약은 아직 하지 않는다.
 */
@Slf4j
@Service
@RequiredArgsConstructor
public class MemoryService {

  // /v1/memory·/v1/npc/relationship가 받는 대화 줄 수 상한
  private static final int HISTORY_LIMIT = 200;
  private static final TypeReference<Map<String, Object>> CARD = new TypeReference<>() {};

  private final UserNpcRelationRepository userNpcRelationRepository;
  private final ConversationRepository conversationRepository;
  private final TurnRepository turnRepository;
  private final NpcRepository npcRepository;
  private final WorkerNpcs workerNpcs;
  private final WorkerLines workerLines;
  private final AiGateway aiGateway;
  private final TransactionTemplate transactionTemplate;
  private final JsonMapper jsonMapper;
  private final GameClockService gameClockService;
  private final Clock clock;

  /** 다음 대화의 NPC 대답에 넘길 기억과 관계. 끝낸 대화가 없으면(처음 만남) null이다. 호출하는 쪽의 트랜잭션 안에서 부른다. */
  public WorkerMemory memoryFor(Long userId, Long npcId) {
    return userNpcRelationRepository
        .findById(new UserNpcRelationId(userId, npcId))
        .filter(relation -> relation.getTalkCount() > 0)
        .map(
            relation ->
                new WorkerMemory(
                    relation.getTalkCount(),
                    null,
                    readCard(relation.getMemoryCard()),
                    relationshipOf(relation, gameClockService.currentDay(userId))))
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
          "npc_memory_update_failed conversation_id={} type={}",
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
    updateCard(trace, job);
    updateRelationship(trace, job);
  }

  private void updateCard(WorkerTrace trace, MemoryJob job) {
    MemoryCardResult result;
    try {
      result =
          aiGateway.memoryCard(trace, job.npc(), job.outcome(), job.history(), job.previousCard());
    } catch (BusinessException exception) {
      // 429·502·503 등. 오류 응답에는 카드가 없으므로 빈 카드로 덮지 않고 이전 카드를 그대로 둔다.
      log.warn("memory_card_update_failed code={}", exception.getErrorCode());
      return;
    }
    transactionTemplate.executeWithoutResult(
        status ->
            userNpcRelationRepository
                .findById(job.relationId())
                .ifPresent(relation -> relation.replaceMemoryCard(result.cardJson())));
  }

  // 워커는 새 값이 아니라 변화량을 돌려주므로 SQL 한 번으로 더한다.
  private void updateRelationship(WorkerTrace trace, MemoryJob job) {
    RelationshipChange change;
    try {
      change = aiGateway.relationship(trace, job.npc(), job.relationship(), job.history());
    } catch (BusinessException exception) {
      // 관계는 바꾸지 않고 이전 값을 그대로 둔다.
      log.warn("relationship_update_failed code={}", exception.getErrorCode());
      return;
    }
    transactionTemplate.executeWithoutResult(
        status ->
            userNpcRelationRepository.addRecognition(
                job.relationId().getUserId(),
                job.relationId().getNpcId(),
                change.recognitionDelta()));
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
    // 관계 갱신에는 이번 대화를 반영하기 전의 관계(지난 대화 뒤 지난 날 수 포함)를 보낸다.
    int today = gameClockService.currentDay(userId);
    WorkerRelationship before = relationshipOf(relation, today);
    boolean firstTime =
        relation.recordVisit(
            conversationId,
            conversation.getEndedAt() != null ? conversation.getEndedAt() : clock.instant(),
            today);
    if (!firstTime) {
      return null;
    }

    WorkerNpc npc =
        npcRepository.findById(conversation.getNpcId()).map(workerNpcs::of).orElse(null);
    if (npc == null) {
      return null;
    }
    return new MemoryJob(
        relationId,
        npc,
        outcome(conversation),
        workerLines.recent(turns, HISTORY_LIMIT),
        readCard(relation.getMemoryCard()),
        before);
  }

  private static WorkerRelationship relationshipOf(UserNpcRelation relation, int today) {
    return new WorkerRelationship(
        relation.getRecognition(), relation.getAffinity(), relation.daysSinceLastTalk(today));
  }

  // 대화 결과 한 줄. 서버가 확정한 사실만 쓰고 추측하지 않는다.
  private static String outcome(Conversation conversation) {
    if (conversation.getStatus() != ConversationStatus.ENDED_BY_NPC) {
      return "The learner ended the conversation normally.";
    }
    if (conversation.getEndReason() == ConversationEndReason.RUDE) {
      return "The NPC ended the conversation because the learner kept being rude.";
    }
    if (conversation.getEndReason() == ConversationEndReason.NPC_ENDED) {
      return "The NPC wrapped up the conversation after the business was done or the learner said goodbye.";
    }
    return "The NPC ended the conversation.";
  }

  private Map<String, Object> readCard(String json) {
    return json == null ? null : jsonMapper.readValue(json, CARD);
  }

  /**
   * @param relationship 이번 대화를 반영하기 전에 저장돼 있던 관계
   */
  private record MemoryJob(
      UserNpcRelationId relationId,
      WorkerNpc npc,
      String outcome,
      List<WorkerLine> history,
      Map<String, Object> previousCard,
      WorkerRelationship relationship) {}
}
