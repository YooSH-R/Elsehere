package com.elsehere.favor.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.ai.client.WorkerState;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.favor.dto.FavorResponse;
import com.elsehere.favor.entity.Favor;
import com.elsehere.favor.entity.FavorOpening;
import com.elsehere.favor.entity.FavorStatus;
import com.elsehere.favor.repository.FavorRepository;
import com.elsehere.favor.service.FavorService.CallCandidate;
import com.elsehere.favor.service.FavorService.FavorTurn;
import com.elsehere.memory.repository.UserNpcRelationRepository;
import com.elsehere.memory.service.NpcAffinityService;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.repository.NpcRepository;
import com.elsehere.world.service.GameClockService;
import com.elsehere.world.service.GameTime;
import java.time.Instant;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.test.util.ReflectionTestUtils;

// 부탁은 학습자가 건 대화 속(욕구에 반응·주문 상황)과 NPC가 먼저 말 걸기로 나뉜다(설계 7.6). 저장소·게임 시계는 Mock이다.
class FavorServiceTest {

  private static final long USER = 7L;
  private static final long JAKE_ID = 3L;
  private static final long MIA_ID = 4L;
  private static final long CONVERSATION = 10L;
  // 3일차 오전 10시
  private static final GameTime TEN_AM = new GameTime(3, 10 * 60);
  private static final long NOW = 2L * 1440 + 10 * 60;

  private final FavorRepository favors = mock(FavorRepository.class);
  private final GameClockService gameClock = mock(GameClockService.class);
  private final NpcAffinityService affinity = mock(NpcAffinityService.class);
  private final UserNpcRelationRepository relations = mock(UserNpcRelationRepository.class);
  private final NpcRepository npcs = mock(NpcRepository.class);
  private final FavorService service =
      new FavorService(new FavorRules(), favors, gameClock, affinity, relations, npcs);

  @BeforeEach
  void setUp() {
    when(gameClock.currentTime(USER)).thenReturn(TEN_AM);
    when(favors.save(any(Favor.class))).thenAnswer(invocation -> invocation.getArgument(0));
    Npc jake = mock(Npc.class);
    when(jake.getId()).thenReturn(JAKE_ID);
    when(jake.getName()).thenReturn("Jake");
    when(npcs.findByCode("HOTEL_STAFF")).thenReturn(Optional.of(jake));
  }

  @Test
  @DisplayName("배고픈 Jake는 학습자가 그 배고픔에 반응한 턴에만 부탁을 꺼낸다")
  void aHungryNpcAsksOnlyWhenTheLearnerReacts() {
    Conversation talk = jakeTalk(Map.of("HUNGER", 20.0));

    FavorTurn turn = service.plan(USER, talk, "HOTEL_STAFF", 0, false);

    assertThat(turn.intents()).extracting(WorkerState.Intent::id).contains("notice_need");
    assertThat(turn.shapesInstruction()).isTrue();
    assertThat(service.instruction(turn, intent("chat"), false, false)).isEmpty();
    assertThat(service.apply(USER, talk, JAKE_ID, "Jake", turn, intent("chat"), false, false))
        .isEmpty();

    assertThat(service.instruction(turn, intent("notice_need"), false, false))
        .contains("ask the learner");
    List<FavorResponse> updates =
        service.apply(USER, talk, JAKE_ID, "Jake", turn, intent("notice_need"), false, false);
    assertThat(updates)
        .singleElement()
        .satisfies(
            update -> {
              assertThat(update.status()).isEqualTo("OFFERED");
              assertThat(update.templateId()).isEqualTo("bring_food");
              assertThat(update.titleKo()).startsWith("Jake의 부탁 · ");
              assertThat(update.rewardCoins()).isEqualTo(6);
              assertThat(update.affinityDelta()).isEqualTo(8);
            });
  }

  @Test
  @DisplayName("욕구가 넉넉하면 학습자가 걱정해도 부탁 의도를 판정하지 않는다")
  void noNoticeIntentWhenTheNpcIsFine() {
    FavorTurn turn = service.plan(USER, jakeTalk(Map.of("HUNGER", 80.0)), "HOTEL_STAFF", 0, false);

    assertThat(turn).isEqualTo(FavorTurn.NONE);
  }

  @Test
  @DisplayName("점심때 카페의 주문 상황 사건은 학습자의 두 번째 말에서 꺼낸다")
  void anOrderingSituationComesOnTheSecondLearnerLine() {
    when(gameClock.currentTime(USER)).thenReturn(new GameTime(3, 12 * 60 + 30));
    Conversation order = talk(MIA_ID, Map.of());

    assertThat(service.plan(USER, order, "CAFE_STAFF", 0, false)).isEqualTo(FavorTurn.NONE);

    FavorTurn second = service.plan(USER, order, "CAFE_STAFF", 1, false);
    assertThat(second.offer().orElseThrow().request().id()).isEqualTo("rush_hour");
    assertThat(service.instruction(second, intent("chat"), false, false)).isNotEmpty();
  }

  @Test
  @DisplayName("이야기 퀘스트 목표가 걸린 대화와 대화 속 부탁의 쉬는 간격 안에서는 꺼내지 않는다")
  void noOfferDuringQuestGoalsOrRest() {
    Conversation talk = jakeTalk(Map.of("HUNGER", 20.0));
    assertThat(service.plan(USER, talk, "HOTEL_STAFF", 0, true)).isEqualTo(FavorTurn.NONE);

    Favor ended = croissant(FavorStatus.OFFERED, CONVERSATION);
    ended.moveTo(FavorStatus.DONE, NOW - 100, 720);
    when(lastEnded(FavorOpening.TALK)).thenReturn(Optional.of(ended));

    assertThat(service.plan(USER, talk, "HOTEL_STAFF", 0, false)).isEqualTo(FavorTurn.NONE);
  }

  @Test
  @DisplayName("무례로 싫어하는 학습자에게는 부탁하지 않는다")
  void noOfferToADislikedLearner() {
    when(affinity.affinity(USER, JAKE_ID)).thenReturn(-20);

    assertThat(service.plan(USER, jakeTalk(Map.of("HUNGER", 20.0)), "HOTEL_STAFF", 0, false))
        .isEqualTo(FavorTurn.NONE);
  }

  @Test
  @DisplayName("다른 대화에서 꺼냈다가 수락하지 않은 심부름은 거절로 끝낸다")
  void anUnansweredErrandFromAnotherTalkIsDeclined() {
    Favor stale = croissant(FavorStatus.OFFERED, 99L);
    when(active()).thenReturn(Optional.of(stale));

    service.plan(USER, jakeTalk(Map.of()), "HOTEL_STAFF", 0, false);

    assertThat(stale.getStatus()).isEqualTo(FavorStatus.DECLINED);
    assertThat(stale.getEndedGameMinute()).isEqualTo(NOW);
  }

  @Test
  @DisplayName("기한이 지난 심부름은 만료로 끝낸다")
  void anExpiredErrandEnds() {
    Favor late = croissant(FavorStatus.OFFERED, CONVERSATION);
    late.moveTo(FavorStatus.ACCEPTED, NOW - 800, 720);
    when(active()).thenReturn(Optional.of(late));

    assertThat(service.current(USER)).isEmpty();
    assertThat(late.getStatus()).isEqualTo(FavorStatus.EXPIRED);
  }

  @Test
  @DisplayName("무례한 턴과 마무리하는 턴에는 부탁을 꺼내지 않는다")
  void noOfferOnARudeOrClosingTurn() {
    Conversation talk = jakeTalk(Map.of("HUNGER", 20.0));
    FavorTurn turn = service.plan(USER, talk, "HOTEL_STAFF", 0, false);

    assertThat(service.instruction(turn, intent("notice_need"), true, false)).isEmpty();
    assertThat(service.instruction(turn, intent("notice_need"), false, true)).isEmpty();
    assertThat(service.apply(USER, talk, JAKE_ID, "Jake", turn, intent("notice_need"), false, true))
        .isEmpty();
    verify(favors, never()).save(any());
  }

  @Test
  @DisplayName("사 온 것을 건네면 완료하고, 부탁한 NPC의 호감을 올리고 채울 욕구를 알린다")
  void handingOverCompletesTheFavor() {
    Favor got = croissant(FavorStatus.OFFERED, CONVERSATION);
    ReflectionTestUtils.setField(got, "id", 42L);
    got.moveTo(FavorStatus.ACCEPTED, NOW - 200, 720);
    got.moveTo(FavorStatus.GOT, NOW - 100, 720);
    when(active()).thenReturn(Optional.of(got));
    when(favors.findById(42L)).thenReturn(Optional.of(got));
    Conversation talk = jakeTalk(Map.of());
    FavorTurn turn = service.plan(USER, talk, "HOTEL_STAFF", 3, false);

    List<FavorResponse> updates =
        service.apply(USER, talk, JAKE_ID, "Jake", turn, intent("hand_over"), false, false);

    assertThat(got.getStatus()).isEqualTo(FavorStatus.DONE);
    verify(affinity).raiseForEvents(USER, JAKE_ID, 8);
    assertThat(updates)
        .singleElement()
        .satisfies(
            update -> {
              assertThat(update.status()).isEqualTo("DONE");
              assertThat(update.fills()).isEqualTo(Map.of("HUNGER", 40));
            });
  }

  @Test
  @DisplayName("근처의 배고픈 Jake가 플레이어에게 말을 걸어 심부름을 부탁한다")
  void aHungryNpcNearbyCallsThePlayer() {
    FavorResponse call = service.call(USER, List.of(jakeHungry())).orElseThrow();

    assertThat(call.status()).isEqualTo("OFFERED");
    assertThat(call.requesterNpcCode()).isEqualTo("HOTEL_STAFF");
    assertThat(call.templateId()).isEqualTo("bring_food");
  }

  @Test
  @DisplayName("말 걸기는 앞 말 걸기 부탁이 끝나고 게임 12시간(플레이 5분)이 지나야 다시 한다")
  void callsWaitForThePlayerGap() {
    Favor lastCall = croissant(FavorStatus.OFFERED, CONVERSATION);
    lastCall.moveTo(FavorStatus.DECLINED, NOW - 100, 720);
    when(lastEnded(FavorOpening.CALL)).thenReturn(Optional.of(lastCall));

    assertThat(service.call(USER, List.of(jakeHungry()))).isEmpty();

    lastCall.moveTo(FavorStatus.DECLINED, NOW - 800, 720);
    assertThat(service.call(USER, List.of(jakeHungry()))).isPresent();
  }

  @Test
  @DisplayName("대화 속 부탁의 쉬는 간격은 말 걸기를 막지 않는다")
  void aTalkRestDoesNotBlockACall() {
    Favor lastTalk = croissant(FavorStatus.OFFERED, CONVERSATION);
    lastTalk.moveTo(FavorStatus.DONE, NOW - 10, 720);
    when(lastEnded(FavorOpening.TALK)).thenReturn(Optional.of(lastTalk));

    assertThat(service.call(USER, List.of(jakeHungry()))).isPresent();
  }

  @Test
  @DisplayName("진행 중인 부탁이 있으면 말을 걸지 않는다")
  void noCallWhileAFavorIsInProgress() {
    Favor accepted = croissant(FavorStatus.OFFERED, CONVERSATION);
    accepted.moveTo(FavorStatus.ACCEPTED, NOW - 10, 720);
    when(active()).thenReturn(Optional.of(accepted));

    assertThat(service.call(USER, List.of(jakeHungry()))).isEmpty();
  }

  @Test
  @DisplayName("말을 걸어 온 NPC와 대화를 시작하면 그 부탁으로 첫마디를 하고 그 대화에서 수락을 판정한다")
  void aCallOpensTheTalkWithTheFavor() {
    Favor call = calling();
    when(active()).thenReturn(Optional.of(call));
    when(favors.findById(42L)).thenReturn(Optional.of(call));

    FavorService.CallOpening opening = service.pendingCall(USER, "HOTEL_STAFF").orElseThrow();
    assertThat(opening.instruction()).contains("walked up to the learner");
    service.attachCall(opening.favorId(), CONVERSATION);

    assertThat(call.getOfferedConversationId()).isEqualTo(CONVERSATION);
    FavorTurn turn = service.plan(USER, jakeTalk(Map.of()), "HOTEL_STAFF", 0, false);
    assertThat(turn.intents()).extracting(WorkerState.Intent::id).contains("accept_favor");
  }

  @Test
  @DisplayName("말을 걸어 온 부탁을 거절하거나, 답하지 않고 다른 NPC와 대화하면 거절로 끝낸다")
  void anUnansweredCallIsDeclined() {
    Favor call = calling();
    when(active()).thenReturn(Optional.of(call));
    when(favors.findByIdAndUserId(42L, USER)).thenReturn(Optional.of(call));

    assertThat(service.decline(USER, 42L).status()).isEqualTo("DECLINED");
    assertThat(call.getEndedGameMinute()).isEqualTo(NOW);

    Favor another = calling();
    when(active()).thenReturn(Optional.of(another));
    service.plan(USER, talk(MIA_ID, Map.of()), "CAFE_STAFF", 0, false);
    assertThat(another.getStatus()).isEqualTo(FavorStatus.DECLINED);
  }

  private Optional<Favor> active() {
    return favors.findFirstByUserIdAndStatusInOrderByIdDesc(USER, FavorStatus.ACTIVE);
  }

  private Optional<Favor> lastEnded(FavorOpening opening) {
    return favors.findFirstByUserIdAndOpeningAndEndedGameMinuteIsNotNullOrderByEndedGameMinuteDesc(
        USER, opening);
  }

  private static CallCandidate jakeHungry() {
    return new CallCandidate("HOTEL_STAFF", Map.of("HUNGER", 20.0));
  }

  private static Conversation jakeTalk(Map<String, Double> needs) {
    return talk(JAKE_ID, needs);
  }

  private static Conversation talk(long npcId, Map<String, Double> needs) {
    Conversation talk = Conversation.start(USER, npcId, InitiatedBy.USER, 1, Instant.EPOCH);
    ReflectionTestUtils.setField(talk, "id", CONVERSATION);
    talk.recordNpcNeeds(needs);
    return talk;
  }

  private static Favor calling() {
    Favor favor =
        Favor.call(USER, "HOTEL_STAFF", "Jake", "bring_food", "BAKERY", "croissant", 3, 6, 8);
    ReflectionTestUtils.setField(favor, "id", 42L);
    return favor;
  }

  private static Favor croissant(FavorStatus status, long conversationId) {
    Favor favor =
        Favor.offer(
            USER,
            "HOTEL_STAFF",
            "Jake",
            "bring_food",
            "BAKERY",
            "croissant",
            3,
            conversationId,
            6,
            8);
    if (status != FavorStatus.OFFERED) {
      favor.moveTo(status, NOW, 720);
    }
    return favor;
  }

  private static JudgeResult.Intent intent(String id) {
    return new JudgeResult.Intent(id, List.of(id), Map.of(), null);
  }
}
