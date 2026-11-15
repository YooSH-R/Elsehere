package com.elsehere.favor.service;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.favor.dto.FavorResponse;
import com.elsehere.favor.entity.Favor;
import com.elsehere.favor.entity.FavorStatus;
import com.elsehere.favor.repository.FavorRepository;
import com.elsehere.favor.service.FavorScript.Role;
import com.elsehere.memory.repository.UserNpcRelationRepository;
import com.elsehere.memory.service.NpcAffinityService;
import com.elsehere.world.service.GameClockService;
import com.elsehere.world.service.GameTime;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * 대화 중 돌발 부탁(docs/npc-life-design.md 7절). 턴마다 판정 전에 계획을 세우고({@link #plan}), 판정 뒤 대답 지시문을 정하며({@link
 * #instruction}), 저장 트랜잭션에서 상태·보상을 반영한다({@link #apply}). 게임 상태(부탁·코인·호감)는 여기서만 바꾼다.
 */
@Service
@RequiredArgsConstructor
public class FavorService {

  private static final int MINUTES_PER_DAY = 24 * 60;

  private final FavorRules rules;
  private final FavorRepository favors;
  private final GameClockService gameClock;
  private final NpcAffinityService npcAffinityService;
  private final UserNpcRelationRepository relations;

  /**
   * 이번 턴의 부탁 계획.
   *
   * @param npcCode 지금 대화하는 NPC 코드. 꺼내는 부탁의 부탁한 NPC다
   * @param favorId 진행 중 부탁. 없으면 null
   * @param role 지금 대화하는 NPC가 그 부탁에서 맡은 자리
   * @param offer 이번 대답에서 꺼낼 부탁. 무례하거나 대화를 마무리하는 턴이면 꺼내지 않는다
   * @param intents 이 대화의 판정 상태에 더할 의도
   */
  public record FavorTurn(
      String npcCode,
      Long favorId,
      Favor favor,
      Role role,
      Optional<FavorPicker.Pick> offer,
      List<WorkerState.Intent> intents,
      int day,
      long gameMinute) {

    public static final FavorTurn NONE =
        new FavorTurn(null, null, null, Role.NONE, Optional.empty(), List.of(), 0, 0);

    /** 판정 결과가 대답 지시문에 들어가는지. 부탁이 걸린 턴은 판정을 기다려 대답한다. */
    public boolean shapesInstruction() {
      return (favor != null && role != Role.NONE) || offer.isPresent();
    }

    /** 판정 상태에 부탁 의도를 더한다. 더할 의도가 없으면 그대로 둔다. */
    public WorkerState mergeInto(WorkerState state) {
      if (intents.isEmpty()) {
        return state;
      }
      List<WorkerState.Intent> merged = new ArrayList<>(state.intents());
      merged.addAll(intents);
      return new WorkerState(
          state.id(),
          state.desc(),
          merged,
          state.accumulateHistory(),
          state.slots(),
          state.recentLines());
    }
  }

  /**
   * 판정 전에 이번 턴의 부탁 계획을 세운다. 지난 대화에서 꺼냈다가 풀리지 않은 부탁과 기한이 지난 심부름을 먼저 끝내고, 진행 중 부탁이 없으면 학습자의 {@code
   * checkAfterLearnerLines}번째 정상 발화에서 꺼낼 부탁을 고른다. 호출하는 쪽의 트랜잭션 안에서 부른다.
   *
   * @param learnerLinesBefore 이 턴 전까지 학습자의 정상 발화(무례가 아닌 말) 수
   * @param questGoals 이 대화에 첫날 미션·이야기 퀘스트·장소 연습 목표가 걸려 있는지
   */
  public FavorTurn plan(
      Long userId,
      Conversation conversation,
      String npcCode,
      long learnerLinesBefore,
      boolean questGoals) {
    if (conversation.isReview() || conversation.isPractice()) {
      return FavorTurn.NONE;
    }
    GameTime time = gameClock.currentTime(userId);
    long now = absolute(time);
    Optional<Favor> active = openFavor(userId, conversation.getId(), now);
    if (active.isPresent()) {
      Favor favor = active.get();
      Role role = FavorScript.roleOf(rules, favor, npcCode);
      return new FavorTurn(
          npcCode,
          favor.getId(),
          favor,
          role,
          Optional.empty(),
          FavorScript.intents(rules, favor, role),
          time.day(),
          now);
    }
    if (questGoals
        || learnerLinesBefore + 1 != rules.constants().checkAfterLearnerLines()
        || !restedEnough(userId, now)
        || favors.existsByUserIdAndRequesterNpcCodeAndGameDay(userId, npcCode, time.day())
        || npcAffinityService.affinity(userId, conversation.getNpcId())
            < rules.constants().dislikedBelow()) {
      return FavorTurn.NONE;
    }
    Optional<FavorPicker.Pick> offer =
        rules
            .npc(npcCode)
            .flatMap(
                npc ->
                    FavorPicker.pick(
                        rules,
                        new FavorPicker.Situation(
                            npcCode,
                            npc.role(),
                            npc.workplace(),
                            conversation.isNpcOffDuty()
                                ? "OFF"
                                : npc.activityAt(time.minuteOfDay()),
                            needsOf(conversation),
                            time.day(),
                            time.minuteOfDay(),
                            firstTalkOfDay(userId, conversation.getNpcId(), time.day()),
                            npc.minutesLeft(time.minuteOfDay()),
                            situationsUsedToday(userId, time.day()))));
    return offer
        .map(
            pick ->
                new FavorTurn(
                    npcCode,
                    null,
                    null,
                    Role.REQUESTER,
                    Optional.of(pick),
                    List.of(),
                    time.day(),
                    now))
        .orElse(FavorTurn.NONE);
  }

  /** 이번 대답에 더할 부탁 지시문. 무례한 턴에는 부탁 이야기를 하지 않고, 대화를 마무리하는 턴에는 새 부탁을 꺼내지 않는다. */
  public String instruction(
      FavorTurn turn, JudgeResult.Intent intent, boolean rude, boolean wrapUp) {
    if (rude) {
      return "";
    }
    if (turn.favor() != null && turn.role() != Role.NONE) {
      Optional<FavorStatus> after =
          FavorScript.advance(rules, turn.favor(), turn.role(), intentIds(intent));
      return FavorScript.instruction(rules, turn.favor(), turn.role(), after);
    }
    if (turn.offer().isPresent() && !wrapUp) {
      return FavorScript.offerInstruction(rules, turn.offer().get());
    }
    return "";
  }

  /**
   * 저장 트랜잭션에서 이번 턴의 부탁을 반영한다. 판정한 의도로 상태를 바꾸고, 완료하면 부탁한 NPC의 호감을 올린다. 꺼낸 부탁은 새로 저장한다.
   *
   * @param npcId 지금 대화하는 NPC(완료 때 호감을 올릴 부탁한 NPC)
   * @param npcName 지금 대화하는 NPC 이름. 새 부탁 카드에 쓴다
   * @return 이번 턴에 바뀐 부탁 소식(턴 응답 missionUpdates)
   */
  public List<FavorResponse> apply(
      Long userId,
      Conversation conversation,
      Long npcId,
      String npcName,
      FavorTurn turn,
      JudgeResult.Intent intent,
      boolean rude,
      boolean wrapUp) {
    if (rude) {
      return List.of();
    }
    if (turn.favorId() != null && turn.role() != Role.NONE) {
      Favor favor = favors.findById(turn.favorId()).orElseThrow();
      Optional<FavorStatus> after =
          FavorScript.advance(rules, favor, turn.role(), intentIds(intent));
      if (after.isEmpty()) {
        return List.of();
      }
      favor.moveTo(after.get(), turn.gameMinute(), rules.constants().errandExpiryMinutes());
      if (after.get() == FavorStatus.DONE) {
        npcAffinityService.raiseForEvents(userId, npcId, favor.getAffinityDelta());
      }
      return List.of(response(favor));
    }
    if (turn.offer().isEmpty()
        || wrapUp
        || favors.existsByUserIdAndRequesterNpcCodeAndGameDay(userId, turn.npcCode(), turn.day())) {
      return List.of();
    }
    FavorPicker.Pick pick = turn.offer().get();
    var reward = pick.request().reward();
    Favor favor =
        favors.save(
            Favor.offer(
                userId,
                turn.npcCode(),
                npcName,
                pick.request().id(),
                pick.offerPlace(),
                pick.offer() == null ? null : pick.offer().key(),
                turn.day(),
                conversation.getId(),
                reward.coins(),
                (int) Math.round(reward.impression() * rules.affinityFollowRate())));
    return List.of(response(favor));
  }

  /** 학습자의 진행 중 부탁(FAV-01). 기한이 지났으면 끝내고 빈 값을 돌려준다. */
  @Transactional
  public Optional<FavorResponse> current(Long userId) {
    long now = absolute(gameClock.currentTime(userId));
    return favors
        .findFirstByUserIdAndStatusInOrderByIdDesc(userId, FavorStatus.ACTIVE)
        .filter(favor -> !expire(favor, now))
        .map(this::response);
  }

  /** 진행 중 부탁을 그만둔다(FAV-02). 이미 끝났으면 그대로 돌려준다. */
  @Transactional
  public FavorResponse abandon(Long userId, Long favorId) {
    Favor favor =
        favors
            .findByIdAndUserId(favorId, userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    if (favor.getStatus().active()) {
      favor.moveTo(
          FavorStatus.ABANDONED,
          absolute(gameClock.currentTime(userId)),
          rules.constants().errandExpiryMinutes());
    }
    return response(favor);
  }

  public FavorResponse response(Favor favor) {
    Map<String, Integer> fills =
        favor.getStatus() == FavorStatus.DONE ? FavorScript.fills(rules, favor) : null;
    return new FavorResponse(
        favor.getId(),
        favor.getTemplateId(),
        favor.getStatus().name(),
        favor.getRequesterNpcCode(),
        FavorScript.title(rules, favor),
        FavorScript.steps(rules, favor).stream()
            .map(
                step ->
                    new FavorResponse.StepResponse(
                        step.placeCode(), step.npcCode(), step.textKo(), step.done()))
            .toList(),
        favor.getRewardCoins(),
        favor.getAffinityDelta(),
        fills,
        favor.getExpiresGameMinute());
  }

  // 진행 중 부탁을 찾는다. 다른 대화에서 꺼냈다가 풀리지 않은 부탁(대화 안 부탁은 만료, 심부름은 거절)과 기한이 지난 심부름은 끝낸다.
  private Optional<Favor> openFavor(Long userId, Long conversationId, long now) {
    Optional<Favor> found =
        favors.findFirstByUserIdAndStatusInOrderByIdDesc(userId, FavorStatus.ACTIVE);
    if (found.isEmpty()) {
      return found;
    }
    Favor favor = found.get();
    if (favor.getStatus() == FavorStatus.OFFERED
        && !favor.getOfferedConversationId().equals(conversationId)) {
      favor.moveTo(
          favor.errand() ? FavorStatus.DECLINED : FavorStatus.EXPIRED,
          now,
          rules.constants().errandExpiryMinutes());
      return Optional.empty();
    }
    return expire(favor, now) ? Optional.empty() : found;
  }

  private boolean expire(Favor favor, long now) {
    if (favor.getExpiresGameMinute() != null && now >= favor.getExpiresGameMinute()) {
      favor.moveTo(FavorStatus.EXPIRED, now, rules.constants().errandExpiryMinutes());
      return true;
    }
    return false;
  }

  private boolean restedEnough(Long userId, long now) {
    return favors
        .findFirstByUserIdAndEndedGameMinuteIsNotNullOrderByEndedGameMinuteDesc(userId)
        .map(last -> now >= last.getEndedGameMinute() + rules.constants().learnerGapMinutes())
        .orElse(true);
  }

  private boolean firstTalkOfDay(Long userId, Long npcId, int day) {
    return relations.findLastTalkGameDay(userId, npcId).map(last -> last != day).orElse(true);
  }

  private Set<String> situationsUsedToday(Long userId, int day) {
    Set<String> used = new HashSet<>();
    for (FavorRules.Request request : rules.requests()) {
      if (!request.needTrigger()
          && favors.existsByUserIdAndTemplateIdAndGameDay(userId, request.id(), day)) {
        used.add(request.id());
      }
    }
    return used;
  }

  private static Map<String, Double> needsOf(Conversation conversation) {
    return conversation.getNpcNeeds() == null ? Map.of() : conversation.getNpcNeeds();
  }

  private static List<String> intentIds(JudgeResult.Intent intent) {
    if (intent == null) {
      return List.of();
    }
    if (intent.ids() != null && !intent.ids().isEmpty()) {
      return intent.ids();
    }
    return intent.id() == null ? List.of() : List.of(intent.id());
  }

  private static long absolute(GameTime time) {
    return (long) (time.day() - 1) * MINUTES_PER_DAY + time.minuteOfDay();
  }
}
