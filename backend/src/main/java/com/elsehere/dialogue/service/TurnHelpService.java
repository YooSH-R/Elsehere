package com.elsehere.dialogue.service;

import com.elsehere.ai.client.AiWorkerClient;
import com.elsehere.ai.client.HintResult;
import com.elsehere.ai.client.MeaningResult;
import com.elsehere.ai.client.WorkerLine;
import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.entity.AiRequestKind;
import com.elsehere.ai.entity.AiRequestStatus;
import com.elsehere.ai.service.AiRequestRecorder;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.GlossaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.dialogue.dto.HintResponse;
import com.elsehere.dialogue.dto.MeaningResponse;
import java.time.Clock;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;
import tools.jackson.core.type.TypeReference;
import tools.jackson.databind.json.JsonMapper;

/**
 * NPC 대사 뜻 보기(CONV-04)와 힌트(CONV-05). 한 번 만든 결과는 턴에 저장해 두고 다시 요청하면 AI를 부르지 않는다.
 *
 * <p>AI를 기다리는 동안 DB 트랜잭션을 잡지 않도록 조회 → AI 호출 → 저장을 나눈다.
 */
@Service
@RequiredArgsConstructor
public class TurnHelpService {

  private static final TypeReference<List<GlossaryResponse>> GLOSSARY = new TypeReference<>() {};

  private final TurnRepository turnRepository;
  private final ConversationSupport support;
  private final AiWorkerClient aiWorkerClient;
  private final AiRequestRecorder aiRequestRecorder;
  private final TransactionTemplate transactionTemplate;
  private final JsonMapper jsonMapper;
  private final Clock clock;

  public MeaningResponse meaning(Long userId, Long turnId, UUID requestId) {
    NpcLine line = transactionTemplate.execute(status -> npcLine(userId, turnId));
    if (line.meaningKo() != null) {
      return new MeaningResponse(
          turnId, line.meaningKo(), jsonMapper.readValue(line.glossary(), GLOSSARY));
    }

    WorkerTrace trace = new WorkerTrace(requestId, userId, line.conversationId());
    MeaningResult result = aiWorkerClient.meaning(trace, line.npc(), line.text(), line.level());
    aiRequestRecorder.record(trace, AiRequestKind.MEANING, result.usage(), AiRequestStatus.OK);
    List<GlossaryResponse> glossary =
        result.glossary() == null
            ? List.of()
            : result.glossary().stream()
                .map(item -> new GlossaryResponse(item.text(), item.meaningKo()))
                .toList();
    String glossaryJson = jsonMapper.writeValueAsString(glossary);
    transactionTemplate.executeWithoutResult(
        status ->
            turnRepository
                .findById(turnId)
                .ifPresent(turn -> turn.attachMeaning(result.meaningKo(), glossaryJson)));
    return new MeaningResponse(turnId, result.meaningKo(), glossary);
  }

  /** 힌트는 워커가 주는 추천 문장 중 가장 쉬운 첫 문장을 쓴다. */
  public HintResponse hint(Long userId, Long turnId, UUID requestId) {
    HintTarget target = transactionTemplate.execute(status -> hintTarget(userId, turnId));
    if (target.hintText() != null) {
      return new HintResponse(turnId, target.hintText());
    }

    WorkerTrace trace = new WorkerTrace(requestId, userId, target.conversationId());
    // 장면별 목표가 아직 없어 NPC 대답처럼 목표를 비워 보낸다(워커는 마지막 NPC 대사에 자연스럽게 대답할 말을 만든다).
    HintResult result =
        aiWorkerClient.hint(trace, target.npc(), target.history(), "", target.level());
    String hintText = result.firstSentence();
    boolean usable = hintText != null && !hintText.isBlank();
    aiRequestRecorder.record(
        trace,
        AiRequestKind.HINT,
        result.usage(),
        usable ? AiRequestStatus.OK : AiRequestStatus.FAILED);
    if (!usable) {
      throw new BusinessException(ErrorCode.AI_RESPONSE_INVALID);
    }
    Instant openedAt = clock.instant();
    transactionTemplate.executeWithoutResult(
        status ->
            turnRepository.findById(turnId).ifPresent(turn -> turn.openHint(hintText, openedAt)));
    return new HintResponse(turnId, hintText);
  }

  private NpcLine npcLine(Long userId, Long turnId) {
    Turn turn = support.ownedNpcTurn(userId, turnId);
    Conversation conversation = turn.getConversation();
    return new NpcLine(
        turn.getText(),
        turn.getMeaningKo(),
        turn.getGlossary(),
        conversation.getId(),
        conversation.getLevelApplied(),
        support.npcById(conversation.getNpcId()).workerNpc());
  }

  private HintTarget hintTarget(Long userId, Long turnId) {
    Turn turn = support.ownedNpcTurn(userId, turnId);
    Conversation conversation = turn.getConversation();
    return new HintTarget(
        turn.getHintText(),
        conversation.getId(),
        conversation.getLevelApplied(),
        support.npcById(conversation.getNpcId()).workerNpc(),
        support.historyUntil(conversation.getId(), turn.getSeq()));
  }

  /** AI 호출 전에 트랜잭션 안에서 꺼내 둔 NPC 턴 정보. */
  private record NpcLine(
      String text,
      String meaningKo,
      String glossary,
      Long conversationId,
      int level,
      WorkerNpc npc) {}

  /**
   * 힌트를 만들 때 쓸 NPC 턴 정보.
   *
   * @param history 이 NPC 대사까지의 앞선 대화
   */
  private record HintTarget(
      String hintText, Long conversationId, int level, WorkerNpc npc, List<WorkerLine> history) {}
}
