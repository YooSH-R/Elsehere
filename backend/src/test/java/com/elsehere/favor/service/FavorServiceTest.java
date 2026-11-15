package com.elsehere.favor.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.elsehere.ai.client.JudgeResult;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.conversation.entity.InitiatedBy;
import com.elsehere.favor.dto.FavorResponse;
import com.elsehere.favor.entity.Favor;
import com.elsehere.favor.entity.FavorStatus;
import com.elsehere.favor.repository.FavorRepository;
import com.elsehere.favor.service.FavorService.FavorTurn;
import com.elsehere.memory.repository.UserNpcRelationRepository;
import com.elsehere.memory.service.NpcAffinityService;
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

// 턴마다 부탁 계획을 세우고 저장 트랜잭션에서 반영한다(설계 7.6·7.7·7.9). 저장소·게임 시계는 Mock이다.
class FavorServiceTest {

  private static final long USER = 7L;
  private static final long JAKE_ID = 3L;
  private static final long CONVERSATION = 10L;
  // 3일차 오전 10시
  private static final GameTime TEN_AM = new GameTime(3, 10 * 60);
  private static final long NOW = 2L * 1440 + 10 * 60;

  private final FavorRepository favors = mock(FavorRepository.class);
  private final GameClockService gameClock = mock(GameClockService.class);
  private final NpcAffinityService affinity = mock(NpcAffinityService.class);
  private final UserNpcRelationRepository relations = mock(UserNpcRelationRepository.class);
  private final FavorService service =
      new FavorService(new FavorRules(), favors, gameClock, affinity, relations);

  @BeforeEach
  void setUp() {
    when(gameClock.currentTime(USER)).thenReturn(TEN_AM);
    when(favors.save(any(Favor.class))).thenAnswer(invocation -> invocation.getArgument(0));
  }

  @Test
  @DisplayName("배고픈 Jake는 학습자의 두 번째 말에 부탁을 꺼내고, 첫 번째 말에는 꺼내지 않는다")
  void offersOnTheSecondLearnerLineOnly() {
    Conversation talk = jakeTalk(Map.of("HUNGER", 30.0));

    assertThat(service.plan(USER, talk, "HOTEL_STAFF", 0, false)).isEqualTo(FavorTurn.NONE);

    FavorTurn second = service.plan(USER, talk, "HOTEL_STAFF", 1, false);
    assertThat(second.offer()).isPresent();
    assertThat(second.offer().get().request().id()).isEqualTo("bring_food");
    assertThat(second.shapesInstruction()).isTrue();
  }

  @Test
  @DisplayName("이야기 퀘스트 목표가 걸린 대화와 쉬는 간격 안에서는 꺼내지 않는다")
  void noOfferDuringQuestGoalsOrRest() {
    Conversation talk = jakeTalk(Map.of("HUNGER", 30.0));
    assertThat(service.plan(USER, talk, "HOTEL_STAFF", 1, true)).isEqualTo(FavorTurn.NONE);

    Favor ended = croissant(FavorStatus.OFFERED, CONVERSATION);
    ended.moveTo(FavorStatus.DONE, NOW - 100, 720);
    when(favors.findFirstByUserIdAndEndedGameMinuteIsNotNullOrderByEndedGameMinuteDesc(USER))
        .thenReturn(Optional.of(ended));

    assertThat(service.plan(USER, talk, "HOTEL_STAFF", 1, false)).isEqualTo(FavorTurn.NONE);
  }

  @Test
  @DisplayName("무례로 싫어하는 학습자에게는 부탁하지 않는다")
  void noOfferToADislikedLearner() {
    when(affinity.affinity(USER, JAKE_ID)).thenReturn(-20);

    assertThat(service.plan(USER, jakeTalk(Map.of("HUNGER", 30.0)), "HOTEL_STAFF", 1, false))
        .isEqualTo(FavorTurn.NONE);
  }

  @Test
  @DisplayName("다른 대화에서 꺼냈다가 수락하지 않은 심부름은 거절로 끝낸다")
  void anUnansweredErrandFromAnotherTalkIsDeclined() {
    Favor stale = croissant(FavorStatus.OFFERED, 99L);
    when(favors.findFirstByUserIdAndStatusInOrderByIdDesc(USER, FavorStatus.ACTIVE))
        .thenReturn(Optional.of(stale));

    service.plan(USER, jakeTalk(Map.of()), "HOTEL_STAFF", 0, false);

    assertThat(stale.getStatus()).isEqualTo(FavorStatus.DECLINED);
    assertThat(stale.getEndedGameMinute()).isEqualTo(NOW);
  }

  @Test
  @DisplayName("기한이 지난 심부름은 만료로 끝낸다")
  void anExpiredErrandEnds() {
    Favor late = croissant(FavorStatus.OFFERED, CONVERSATION);
    late.moveTo(FavorStatus.ACCEPTED, NOW - 800, 720);
    when(favors.findFirstByUserIdAndStatusInOrderByIdDesc(USER, FavorStatus.ACTIVE))
        .thenReturn(Optional.of(late));

    assertThat(service.current(USER)).isEmpty();
    assertThat(late.getStatus()).isEqualTo(FavorStatus.EXPIRED);
  }

  @Test
  @DisplayName("꺼낸 부탁은 그 NPC 이름과 고를 때 정한 보상으로 저장한다")
  void anOfferIsSavedWithTheRewardChosenNow() {
    Conversation talk = jakeTalk(Map.of("HUNGER", 30.0));
    FavorTurn turn = service.plan(USER, talk, "HOTEL_STAFF", 1, false);

    List<FavorResponse> updates =
        service.apply(USER, talk, JAKE_ID, "Jake", turn, intent("chat"), false, false);

    assertThat(updates)
        .singleElement()
        .satisfies(
            update -> {
              assertThat(update.status()).isEqualTo("OFFERED");
              assertThat(update.titleKo()).startsWith("Jake의 부탁 · ");
              assertThat(update.rewardCoins()).isEqualTo(6);
              assertThat(update.affinityDelta()).isEqualTo(8);
            });
  }

  @Test
  @DisplayName("무례한 턴과 마무리하는 턴에는 부탁을 꺼내지 않는다")
  void noOfferOnARudeOrClosingTurn() {
    Conversation talk = jakeTalk(Map.of("HUNGER", 30.0));
    FavorTurn turn = service.plan(USER, talk, "HOTEL_STAFF", 1, false);

    assertThat(service.instruction(turn, intent("chat"), true, false)).isEmpty();
    assertThat(service.instruction(turn, intent("chat"), false, true)).isEmpty();
    assertThat(service.apply(USER, talk, JAKE_ID, "Jake", turn, intent("chat"), false, true))
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
    when(favors.findFirstByUserIdAndStatusInOrderByIdDesc(USER, FavorStatus.ACTIVE))
        .thenReturn(Optional.of(got));
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

  private static Conversation jakeTalk(Map<String, Double> needs) {
    Conversation talk = Conversation.start(USER, JAKE_ID, InitiatedBy.USER, 1, Instant.EPOCH);
    ReflectionTestUtils.setField(talk, "id", CONVERSATION);
    talk.recordNpcNeeds(needs);
    return talk;
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
