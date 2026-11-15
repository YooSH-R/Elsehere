package com.elsehere.favor.service;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.favor.dto.FavorResponse;
import com.elsehere.favor.entity.Favor;
import com.elsehere.favor.entity.FavorOpening;
import com.elsehere.favor.entity.FavorStatus;
import com.elsehere.favor.repository.FavorRepository;
import com.elsehere.favor.service.FavorScript.Role;
import com.elsehere.memory.repository.UserNpcRelationRepository;
import com.elsehere.memory.service.NpcAffinityService;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
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
 * 돌발 부탁(docs/npc-life-design.md 7절). 부탁은 두 곳에서 시작한다.
 *
 * <ul>
 *   <li>대화 속(TALK): 학습자가 건 대화에서 턴마다 판정 전에 계획을 세우고({@link #plan}), 판정 뒤 대답 지시문을 정하며({@link
 *       #instruction}), 저장 트랜잭션에서 상태·보상을 반영한다({@link #apply}). 욕구 부탁은 학습자가 NPC가 내비친 욕구에 반응한 턴에만, 주문
 *       상황 사건은 학습자의 정해진 번째 말에서 꺼낸다.
 *   <li>NPC 말 걸기(CALL): Unity가 근처 NPC를 보내도 되는지 묻고({@link #call}), 플레이어가 그 NPC와 대화를 시작하면 첫마디가 부탁이
 *       된다({@link #pendingCall}·{@link #attachCall}). 간격은 플레이어 기준(사용자별 게임 시각)으로 센다.
 * </ul>
 *
 * <p>게임 상태(부탁·코인·호감)는 여기서만 바꾼다.
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
  private final NpcRepository npcs;

  /**
   * NPC 말 걸기 후보. Unity가 플레이어 근처에서 지금 생활하는 NPC를 가까운 순서로 보낸다.
   *
   * @param needs 지금 그 NPC의 생활 욕구. 모르면 빈 맵
   */
  public record CallCandidate(String npcCode, Map<String, Double> needs) {}

  /** NPC가 말을 걸어 꺼낸 부탁으로 대화를 시작할 때의 첫마디 지시문. */
  public record CallOpening(Long favorId, String instruction) {}

  /**
   * 이번 턴의 부탁 계획.
   *
   * @param npcCode 지금 대화하는 NPC 코드. 꺼내는 부탁의 부탁한 NPC다
   * @param favorId 진행 중 부탁. 없으면 null
   * @param role 지금 대화하는 NPC가 그 부탁에서 맡은 자리
   * @param offer 이번 대답에서 꺼낼 부탁. 무례하거나 대화를 마무리하는 턴이면 꺼내지 않는다
   * @param intents 이 대화의 판정 상태에 더할 의도
   * @param waitsForNotice 학습자가 NPC가 내비친 욕구에 반응한 턴에만 offer를 꺼내는지
   */
  public record FavorTurn(
      String npcCode,
      Long favorId,
      Favor favor,
      Role role,
      Optional<FavorPicker.Pick> offer,
      List<WorkerState.Intent> intents,
      boolean waitsForNotice,
      int day,
      long gameMinute) {

    public static final FavorTurn NONE =
        new FavorTurn(null, null, null, Role.NONE, Optional.empty(), List.of(), false, 0, 0);

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
   * 판정 전에 이번 턴의 부탁 계획을 세운다. 지난 대화에서 꺼냈다가 풀리지 않은 부탁과 기한이 지난 심부름을 먼저 끝낸다. 진행 중 부탁이 없으면, NPC 욕구가 기준
   * 아래일 때 학습자가 그 욕구에 반응하면 꺼낼 부탁을 고르고(반응 의도를 판정에 더함), 그렇지 않으면 학습자의 {@code checkAfterLearnerLines}번째
   * 정상 발화에서 주문 상황 사건을 고른다. 호출하는 쪽의 트랜잭션 안에서 부른다.
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
    Optional<Favor> active = openFavor(userId, conversation.getId(), npcCode, now);
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
          false,
          time.day(),
          now);
    }
    if (questGoals
        || !restedEnough(userId, FavorOpening.TALK, rules.constants().learnerGapMinutes(), now)
        || favors.existsByUserIdAndRequesterNpcCodeAndGameDay(userId, npcCode, time.day())
        || npcAffinityService.affinity(userId, conversation.getNpcId())
            < rules.constants().dislikedBelow()) {
      return FavorTurn.NONE;
    }
    Optional<FavorPicker.Situation> situation =
        rules
            .npc(npcCode)
            .map(
                npc ->
                    new FavorPicker.Situation(
                        npcCode,
                        npc.role(),
                        npc.workplace(),
                        conversation.isNpcOffDuty() ? "OFF" : npc.activityAt(time.minuteOfDay()),
                        needsOf(conversation),
                        time.day(),
                        time.minuteOfDay(),
                        firstTalkOfDay(userId, conversation.getNpcId(), time.day()),
                        npc.minutesLeft(time.minuteOfDay()),
                        situationsUsedToday(userId, time.day())));
    if (situation.isEmpty()) {
      return FavorTurn.NONE;
    }
    // NPC가 내비친 욕구(워커가 말투에 싣는 기준과 같은 30 아래)는 학습자가 반응하면 부탁으로 이어 간다.
    Optional<FavorPicker.Pick> need =
        FavorPicker.pick(rules, situation.get(), FavorPicker.TALK_NEED);
    if (need.isPresent()) {
      return new FavorTurn(
          npcCode,
          null,
          null,
          Role.REQUESTER,
          need,
          List.of(FavorScript.noticeIntent(need.get())),
          true,
          time.day(),
          now);
    }
    if (learnerLinesBefore + 1 != rules.constants().checkAfterLearnerLines()) {
      return FavorTurn.NONE;
    }
    return FavorPicker.pick(rules, situation.get(), FavorPicker.TALK_SITUATION)
        .map(
            pick ->
                new FavorTurn(
                    npcCode,
                    null,
                    null,
                    Role.REQUESTER,
                    Optional.of(pick),
                    List.of(),
                    false,
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
    if (turn.offer().isPresent() && !wrapUp && noticed(turn, intent)) {
      return FavorScript.offerInstruction(rules, turn.offer().get());
    }
    return "";
  }

  // 욕구 부탁은 학습자가 그 욕구에 반응한 턴에만 꺼낸다. 주문 상황 사건은 반응을 기다리지 않는다.
  private static boolean noticed(FavorTurn turn, JudgeResult.Intent intent) {
    return !turn.waitsForNotice() || intentIds(intent).contains(FavorScript.NOTICE);
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
        || !noticed(turn, intent)
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

  /**
   * NPC 말 걸기(FAV-03). 진행 중 부탁이 없고 앞 말 걸기 부탁이 끝난 뒤 {@code callGapMinutes}(플레이어 기준)가 지났으면, 후보 가운데 가장
   * 가까운 NPC부터 꺼낼 부탁이 있는 NPC를 골라 부탁을 저장한다. 그 NPC가 그 게임 날 이미 부탁했거나 학습자를 싫어하면 건너뛴다.
   *
   * @param candidates 플레이어 근처에서 지금 생활하는 NPC. 가까운 순서다
   * @return 꺼낸 부탁. 말을 걸 때가 아니거나 꺼낼 부탁이 없으면 빈 값
   */
  @Transactional
  public Optional<FavorResponse> call(Long userId, List<CallCandidate> candidates) {
    GameTime time = gameClock.currentTime(userId);
    long now = absolute(time);
    if (openFavor(userId, null, null, now).isPresent()
        || !restedEnough(userId, FavorOpening.CALL, rules.constants().callGapMinutes(), now)) {
      return Optional.empty();
    }
    for (CallCandidate candidate : candidates) {
      Optional<FavorRules.Npc> npc = rules.npc(candidate.npcCode());
      Optional<Npc> entity = npcs.findByCode(candidate.npcCode());
      if (npc.isEmpty()
          || entity.isEmpty()
          || favors.existsByUserIdAndRequesterNpcCodeAndGameDay(
              userId, candidate.npcCode(), time.day())
          || npcAffinityService.affinity(userId, entity.get().getId())
              < rules.constants().dislikedBelow()) {
        continue;
      }
      FavorPicker.Situation situation =
          new FavorPicker.Situation(
              candidate.npcCode(),
              npc.get().role(),
              npc.get().workplace(),
              npc.get().activityAt(time.minuteOfDay()),
              candidate.needs() == null ? Map.of() : candidate.needs(),
              time.day(),
              time.minuteOfDay(),
              false,
              npc.get().minutesLeft(time.minuteOfDay()),
              situationsUsedToday(userId, time.day()));
      Optional<FavorPicker.Pick> pick = FavorPicker.pick(rules, situation, FavorPicker.CALL);
      if (pick.isPresent()) {
        var reward = pick.get().request().reward();
        Favor favor =
            favors.save(
                Favor.call(
                    userId,
                    candidate.npcCode(),
                    entity.get().getName(),
                    pick.get().request().id(),
                    pick.get().offerPlace(),
                    pick.get().offer() == null ? null : pick.get().offer().key(),
                    time.day(),
                    reward.coins(),
                    (int) Math.round(reward.impression() * rules.affinityFollowRate())));
        return Optional.of(response(favor));
      }
    }
    return Optional.empty();
  }

  /** 그 NPC가 말을 걸어 꺼냈고 아직 답하지 않은 부탁이 있으면, 대화 첫마디를 그 부탁으로 하는 지시문. 대화 시작(CONV-01) 전에 부른다. */
  @Transactional(readOnly = true)
  public Optional<CallOpening> pendingCall(Long userId, String npcCode) {
    return favors
        .findFirstByUserIdAndStatusInOrderByIdDesc(userId, FavorStatus.ACTIVE)
        .filter(Favor::waitingForCallAnswer)
        .filter(favor -> favor.getRequesterNpcCode().equals(npcCode))
        .map(favor -> new CallOpening(favor.getId(), FavorScript.callInstruction(rules, favor)));
  }

  /** 말을 걸어 꺼낸 부탁을 그 NPC와 시작한 대화에 붙인다. 대화를 저장하는 트랜잭션에서 부른다. */
  public void attachCall(Long favorId, Long conversationId) {
    favors
        .findById(favorId)
        .filter(Favor::waitingForCallAnswer)
        .ifPresent(favor -> favor.attachTo(conversationId));
  }

  /**
   * 아직 답하지 않은 부탁을 거절로 끝낸다(FAV-04). 말을 걸어 온 NPC의 제안을 거절·무시했거나, 부탁을 꺼낸 대화가 답 없이 끝났을 때 Unity가 부른다.
   * 수락했거나 이미 끝난 부탁은 그대로 돌려준다.
   */
  @Transactional
  public FavorResponse decline(Long userId, Long favorId) {
    Favor favor =
        favors
            .findByIdAndUserId(favorId, userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    if (favor.getStatus() == FavorStatus.OFFERED) {
      favor.moveTo(
          FavorStatus.DECLINED,
          absolute(gameClock.currentTime(userId)),
          rules.constants().errandExpiryMinutes());
    }
    return response(favor);
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

  /**
   * 진행 중 부탁을 찾는다. 다른 대화에서 꺼냈다가 풀리지 않은 부탁(대화 안 부탁은 만료, 심부름은 거절)과 기한이 지난 심부름은 끝낸다. NPC가 말을 걸어 꺼낸 부탁은
   * 그 NPC와 대화하면 그 대화에 붙이고, 답하지 않고 다른 NPC와 대화하면 거절로 끝낸다.
   *
   * @param conversationId 지금 대화. 대화 밖에서 찾으면 null
   */
  private Optional<Favor> openFavor(Long userId, Long conversationId, String npcCode, long now) {
    Optional<Favor> found =
        favors.findFirstByUserIdAndStatusInOrderByIdDesc(userId, FavorStatus.ACTIVE);
    if (found.isEmpty()) {
      return found;
    }
    Favor favor = found.get();
    if (conversationId != null && favor.waitingForCallAnswer()) {
      if (!favor.getRequesterNpcCode().equals(npcCode)) {
        favor.moveTo(FavorStatus.DECLINED, now, rules.constants().errandExpiryMinutes());
        return Optional.empty();
      }
      favor.attachTo(conversationId);
    }
    if (conversationId != null
        && favor.getStatus() == FavorStatus.OFFERED
        && !conversationId.equals(favor.getOfferedConversationId())) {
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

  // 그 곳(대화 속·말 걸기)에서 시작한 부탁이 끝난 뒤 쉬는 간격이 지났는지. 사용자별 게임 시각은 플레이하는 동안만 흐른다.
  private boolean restedEnough(Long userId, FavorOpening opening, int gapMinutes, long now) {
    return favors
        .findFirstByUserIdAndOpeningAndEndedGameMinuteIsNotNullOrderByEndedGameMinuteDesc(
            userId, opening)
        .map(last -> now >= last.getEndedGameMinute() + gapMinutes)
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
