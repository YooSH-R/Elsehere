package com.elsehere.followup.service;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.WorkerFollowUp;
import com.elsehere.ai.client.WorkerLine;
import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.ai.client.WorkerTrace;
import com.elsehere.ai.service.WorkerLines;
import com.elsehere.ai.service.WorkerNpcs;
import com.elsehere.common.config.ClockConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.conversation.repository.TurnRepository;
import com.elsehere.followup.dto.FollowUpResponse;
import com.elsehere.followup.entity.FollowUpWait;
import com.elsehere.followup.entity.NpcFollowUp;
import com.elsehere.followup.repository.NpcFollowUpRepository;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import com.elsehere.world.service.PoliceOfficers;
import java.time.Clock;
import java.time.Instant;
import java.util.List;
import java.util.Optional;
import java.util.Set;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.scheduling.annotation.Async;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

/**
 * NPC가 대화에서 곧 건네기로 한 것(음료·음식·빨래 등). 대화가 끝나면 AI 워커로 판정해 준비 시각과 함께 저장하고, 준비된 뒤 그 NPC와 대화를 시작하면 첫마디로
 * 건넨다(CONV-08·13·01).
 *
 * <p>사용자 × NPC마다 기다리는 것은 하나다. 같은 NPC에게 새로 약속받으면 앞의 것을 바꾼다. 진열된 물건을 그 자리에서 건네는 가게 직원은 기다릴 것이 없으므로
 * 판정하지 않는다. 학습자는 기다리는 카드를 닫을 수 있다.
 */
@Slf4j
@Service
@RequiredArgsConstructor
public class FollowUpService {

  private static final int HISTORY_LIMIT = 200;

  // 산 것을 그 자리에서 건네는 직원(우산·간식·꽃·램프·인형뽑기·소포·현금·이발). 기다리는 카드를 만들지 않는다.
  private static final Set<String> HANDS_OVER_ON_THE_SPOT =
      Set.of(
          "SHOP_STAFF",
          "BAKERY_STAFF",
          "FLOWER_SHOP_STAFF",
          "HOME_STORE_STAFF",
          "ARCADE_STAFF",
          "POST_OFFICE_STAFF",
          "BANK_STAFF",
          "BARBER_STAFF");

  private final NpcFollowUpRepository followUpRepository;
  private final ConversationRepository conversationRepository;
  private final TurnRepository turnRepository;
  private final NpcRepository npcRepository;
  private final WorkerNpcs workerNpcs;
  private final WorkerLines workerLines;
  private final AiGateway aiGateway;
  private final TransactionTemplate transactionTemplate;
  private final Clock clock;

  /**
   * 끝난 대화에서 NPC가 곧 건네기로 한 것을 판정해 저장한다. 같은 대화는 한 번만 판정하고, 다시 종료하면 저장한 것을 돌려준다. 판정에 실패하면 건넬 것이 없는 것으로
   * 본다. 대화 종료(CONV-08)가 피드백 생성과 함께 기다리도록 따로 실행한다.
   *
   * @param requestId 대화 종료 요청 ID. AI 호출 기록에 쓴다
   */
  @Async
  public CompletableFuture<Optional<FollowUpResponse>> afterConversation(
      Long userId, Long conversationId, UUID requestId) {
    try {
      return CompletableFuture.completedFuture(judge(userId, conversationId, requestId));
    } catch (RuntimeException exception) {
      log.warn(
          "follow_up_judge_failed conversation_id={} type={}",
          conversationId,
          exception.getClass().getSimpleName());
      return CompletableFuture.completedFuture(Optional.empty());
    }
  }

  /** 아직 건네지 않은 것. 준비 시각 순서다(CONV-13). */
  public List<FollowUpResponse> waiting(Long userId) {
    return transactionTemplate.execute(
        status ->
            followUpRepository.findAllByUserIdAndDeliveredAtIsNullOrderByReadyAtAsc(userId).stream()
                .map(this::response)
                .toList());
  }

  /** 학습자가 기다리는 카드를 닫는다. 건넨 것처럼 목록에서 빼고 그 NPC가 다시 부르지 않는다. 다른 사용자의 것이나 이미 건넨 것은 그대로 둔다. */
  public void dismiss(Long userId, Long followUpId) {
    transactionTemplate.executeWithoutResult(
        status ->
            followUpRepository
                .findById(followUpId)
                .filter(followUp -> followUp.getUserId().equals(userId) && !followUp.isDelivered())
                .ifPresent(followUp -> followUp.dismiss(clock.instant())));
  }

  /** 이 NPC가 지금 건넬 수 있는 것. 대화 시작(CONV-01)의 첫마디를 정할 때 쓴다. */
  public Optional<ReadyFollowUp> readyFor(Long userId, Long npcId) {
    Instant now = clock.instant();
    return followUpRepository
        .findFirstByUserIdAndNpcIdAndDeliveredAtIsNull(userId, npcId)
        .filter(followUp -> followUp.isReady(now))
        .map(followUp -> new ReadyFollowUp(followUp.getId(), followUp.getItem()));
  }

  /** 대화를 시작하며 첫마디로 건넨 것으로 표시한다. 대화를 저장하는 트랜잭션 안에서 부른다. */
  public void deliver(Long followUpId, Long conversationId) {
    followUpRepository
        .findById(followUpId)
        .filter(followUp -> !followUp.isDelivered())
        .ifPresent(followUp -> followUp.deliver(conversationId, clock.instant()));
  }

  /** 이 대화의 첫마디로 건넨 것. 같은 요청 ID로 대화 시작을 다시 보냈을 때 쓴다. */
  public Optional<Long> deliveredIn(Long conversationId) {
    return followUpRepository.findByDeliveredConversationId(conversationId).map(NpcFollowUp::getId);
  }

  private Optional<FollowUpResponse> judge(Long userId, Long conversationId, UUID requestId) {
    Optional<FollowUpResponse> saved =
        transactionTemplate.execute(
            status -> followUpRepository.findByConversationId(conversationId).map(this::response));
    if (saved.isPresent()) {
      return saved;
    }
    Job job = transactionTemplate.execute(status -> prepare(userId, conversationId));
    if (job == null) {
      return Optional.empty();
    }
    WorkerTrace trace = new WorkerTrace(requestId, userId, conversationId);
    WorkerFollowUp owed;
    try {
      owed =
          (job.policeCall()
                  ? aiGateway.policeCallOutcome(trace, job.npc(), job.history())
                  : aiGateway.followUp(trace, job.npc(), job.history()))
              .followUp();
    } catch (BusinessException exception) {
      log.warn("follow_up_judge_failed code={}", exception.getErrorCode());
      return Optional.empty();
    }
    if (owed == null) {
      return Optional.empty();
    }
    FollowUpWait wait = FollowUpWait.valueOf(owed.waitKind());
    return Optional.of(
        transactionTemplate.execute(
            status -> promise(userId, job.npcId(), conversationId, owed, wait)));
  }

  // 같은 NPC에게 기다리던 것이 있으면 새 약속으로 바꾼다.
  private FollowUpResponse promise(
      Long userId, Long npcId, Long conversationId, WorkerFollowUp owed, FollowUpWait wait) {
    followUpRepository.deleteAllByUserIdAndNpcIdAndDeliveredAtIsNull(userId, npcId);
    NpcFollowUp followUp =
        followUpRepository.save(
            NpcFollowUp.promise(
                userId, npcId, conversationId, owed.item(), owed.itemKo(), wait, clock.instant()));
    log.info(
        "follow_up_promised conversation_id={} follow_up_id={} wait={}",
        conversationId,
        followUp.getId(),
        wait);
    return response(followUp);
  }

  // 판정할 재료. 끝나지 않았거나, 학습자가 말하지 않았거나, 무례로 끝난 대화와 그 자리에서 건네는 직원과의 대화는 판정하지 않는다(null).
  private Job prepare(Long userId, Long conversationId) {
    Conversation conversation =
        conversationRepository.findByIdAndUserId(conversationId, userId).orElse(null);
    if (conversation == null
        || conversation.isActive()
        || conversation.getEndReason() == ConversationEndReason.RUDE) {
      return null;
    }
    List<Turn> turns = turnRepository.findAllByConversationIdOrderBySeqAsc(conversationId);
    if (turns.stream().noneMatch(turn -> turn.getSpeaker() == Speaker.USER)) {
      return null;
    }
    boolean policeDispatch = turns.stream().anyMatch(Turn::isPoliceDispatch);
    return npcRepository
        .findById(conversation.getNpcId())
        .filter(npc -> !HANDS_OVER_ON_THE_SPOT.contains(npc.getCode()))
        .filter(npc -> !PoliceOfficers.includes(npc.getCode()) || policeDispatch)
        .map(
            npc ->
                new Job(
                    npc.getId(),
                    workerNpcs.of(npc),
                    workerLines.recent(turns, HISTORY_LIMIT),
                    PoliceOfficers.includes(npc.getCode())))
        .orElse(null);
  }

  private FollowUpResponse response(NpcFollowUp followUp) {
    Npc npc = npcRepository.findById(followUp.getNpcId()).orElseThrow();
    Instant now = clock.instant();
    return new FollowUpResponse(
        followUp.getId(),
        new NpcSummaryResponse(npc.getCode(), npc.getName(), npc.getRoleLabel()),
        npc.getScene().getCode(),
        followUp.getItem(),
        followUp.getItemKo(),
        followUp.getReadyAt().atZone(ClockConfig.SERVICE_ZONE).toOffsetDateTime(),
        followUp.remainingSeconds(now),
        followUp.isReady(now));
  }

  /**
   * 대화 시작 첫마디로 건넬 것.
   *
   * @param item 대화에 나온 영어 이름. NPC 지시에 넣는다
   */
  public record ReadyFollowUp(Long followUpId, String item) {}

  /**
   * @param policeCall 경찰관과의 대화. 신고를 받아 출동했으면 건넬 것 대신 순찰을 다녀와 알릴 해결 결과를 판정한다
   */
  private record Job(Long npcId, WorkerNpc npc, List<WorkerLine> history, boolean policeCall) {}
}
