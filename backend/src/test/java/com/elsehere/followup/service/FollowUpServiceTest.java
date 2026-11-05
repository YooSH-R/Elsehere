package com.elsehere.followup.service;

import static com.elsehere.conversation.DialogueTestData.conversation;
import static com.elsehere.conversation.DialogueTestData.npcId;
import static com.elsehere.conversation.DialogueTestData.npcTurn;
import static com.elsehere.conversation.DialogueTestData.textTurn;
import static com.elsehere.conversation.DialogueTestData.usage;
import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.times;

import com.elsehere.ai.client.AiGateway;
import com.elsehere.ai.client.FollowUpResult;
import com.elsehere.ai.client.WorkerFollowUp;
import com.elsehere.common.config.AdjustableClock;
import com.elsehere.common.config.TestClockConfig;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.entity.Conversation;
import com.elsehere.dialogue.service.DialogueServiceTest;
import com.elsehere.followup.dto.FollowUpResponse;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.time.Instant;
import java.util.List;
import java.util.Optional;
import java.util.UUID;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;

// 판정은 AI 워커 결과를 Mock으로 정하고, 저장 결과는 기다리는 목록(waiting)과 대화 시작에 쓰는 readyFor로 확인한다.
@DialogueServiceTest
class FollowUpServiceTest {

  private static final WorkerFollowUp LATTE = new WorkerFollowUp("iced latte", "아이스 라테", "QUICK");

  @Autowired private FollowUpService followUpService;

  @Autowired private AiGateway aiGateway;

  @Autowired private AdjustableClock clock;

  @Autowired private EntityManager entityManager;

  private Long userId;
  private Long cafeStaffId;

  @BeforeEach
  void setUp() {
    userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    cafeStaffId = npcId(entityManager, "CAFE_STAFF");
  }

  @AfterEach
  void resetClock() {
    clock.setInstant(TestClockConfig.NOW);
  }

  @Test
  @DisplayName("주문을 받고 끝난 대화는 건넬 것과 남은 초를 저장해 돌려주고, 기다리는 목록에 담는다")
  void afterConversation_orderBeingPrepared_savesTheWait() {
    owes(LATTE);

    Optional<FollowUpResponse> followUp = judge(endedConversation("One iced latte, please."));

    assertThat(followUp).hasValueSatisfying(this::isWaitingLatte);
    assertThat(followUpService.waiting(userId)).singleElement().satisfies(this::isWaitingLatte);
  }

  @Test
  @DisplayName("건넬 것이 없으면 아무것도 저장하지 않는다")
  void afterConversation_nothingOwed_savesNothing() {
    owes(null);

    assertThat(judge(endedConversation("Where is the bank?"))).isEmpty();
    assertThat(followUpService.waiting(userId)).isEmpty();
  }

  @Test
  @DisplayName("같은 대화를 다시 끝내면 워커에 다시 묻지 않고 저장한 것을 돌려준다")
  void afterConversation_sameConversationAgain_returnsTheSavedOne() {
    owes(LATTE);
    Conversation conversation = endedConversation("One iced latte, please.");

    Long first = judge(conversation).orElseThrow().followUpId();
    Long second = judge(conversation).orElseThrow().followUpId();

    assertThat(second).isEqualTo(first);
    then(aiGateway).should(times(1)).followUp(any(), any(), any());
  }

  @Test
  @DisplayName("판정에 실패하면 건넬 것이 없는 것으로 본다")
  void afterConversation_workerFails_isEmpty() {
    given(aiGateway.followUp(any(), any(), any()))
        .willThrow(new BusinessException(ErrorCode.AI_TEMPORARILY_UNAVAILABLE));

    assertThat(judge(endedConversation("One iced latte, please."))).isEmpty();
  }

  @Test
  @DisplayName("학습자가 말하지 않은 대화는 워커에 묻지 않는다")
  void afterConversation_learnerNeverSpoke_doesNotAsk() {
    Conversation conversation = conversation(entityManager, userId, "CAFE_STAFF");
    npcTurn(entityManager, conversation, 1, "Hi! What can I get for you?");
    conversation.endByUser(TestClockConfig.NOW);
    entityManager.flush();

    assertThat(judge(conversation)).isEmpty();
    then(aiGateway).should(never()).followUp(any(), any(), any());
  }

  @Test
  @DisplayName("같은 NPC에게 새로 주문하면 기다리던 것을 새 주문으로 바꾼다")
  void afterConversation_newOrderFromTheSameNpc_replacesTheWaitingOne() {
    owes(LATTE);
    judge(endedConversation("One iced latte, please."));
    owes(new WorkerFollowUp("blueberry muffin", "블루베리 머핀", "QUICK"));

    judge(endedConversation("And a blueberry muffin, please."));

    assertThat(followUpService.waiting(userId))
        .singleElement()
        .extracting(FollowUpResponse::itemKo)
        .isEqualTo("블루베리 머핀");
  }

  @Test
  @DisplayName("준비 시각 전에는 대화 시작에 건넬 것이 없고, 지나면 건넬 것이 있다")
  void readyFor_onlyAfterTheReadyTime() {
    owes(LATTE);
    judge(endedConversation("One iced latte, please."));

    assertThat(followUpService.readyFor(userId, cafeStaffId)).isEmpty();

    clock.setInstant(TestClockConfig.NOW.plusSeconds(45));
    assertThat(followUpService.readyFor(userId, cafeStaffId))
        .hasValueSatisfying(ready -> assertThat(ready.item()).isEqualTo("iced latte"));
    assertThat(followUpService.waiting(userId))
        .singleElement()
        .satisfies(
            waiting -> {
              assertThat(waiting.remainingSeconds()).isZero();
              assertThat(waiting.ready()).isTrue();
            });
  }

  @Test
  @DisplayName("건넨 것은 기다리는 목록에서 빠지고, 건넨 대화로 다시 찾을 수 있다")
  void deliver_removesItFromTheWaitingList() {
    owes(LATTE);
    Long followUpId =
        judge(endedConversation("One iced latte, please.")).orElseThrow().followUpId();
    clock.setInstant(TestClockConfig.NOW.plusSeconds(60));
    Conversation handover = conversation(entityManager, userId, "CAFE_STAFF");

    followUpService.deliver(followUpId, handover.getId());
    entityManager.flush();

    assertThat(followUpService.waiting(userId)).isEmpty();
    assertThat(followUpService.readyFor(userId, cafeStaffId)).isEmpty();
    assertThat(followUpService.deliveredIn(handover.getId())).contains(followUpId);
  }

  @Test
  @DisplayName("가게에서 물건을 바로 건네는 직원(드럭스토어 우산 등)과의 대화는 워커에 묻지 않고 기다리는 것도 만들지 않는다")
  void afterConversation_goodsHandedOverOnTheSpot_owesNothing() {
    owes(new WorkerFollowUp("umbrella", "우산", "QUICK"));

    assertThat(judge(endedConversation("SHOP_STAFF", "I'll buy this umbrella. Cash, please.")))
        .isEmpty();
    assertThat(followUpService.waiting(userId)).isEmpty();
    then(aiGateway).should(never()).followUp(any(), any(), any());
  }

  @Test
  @DisplayName("경찰관에게 신고해 출동한 대화는 출동 결과로 판정해, 순찰을 마치고 돌아올 즈음(90초) 결과를 알릴 수 있게 저장한다")
  void afterConversation_policeCall_savesTheResultTheOfficerWillReport() {
    given(aiGateway.policeCallOutcome(any(), any(), any()))
        .willReturn(
            new FollowUpResult(
                new WorkerFollowUp("your wallet, found at Maple Diner", "지갑 수색 결과", "SHORT"),
                List.of(usage("follow_up"))));

    Optional<FollowUpResponse> result =
        judge(endedConversation("POLICE_OFFICER", "Someone took my wallet at the diner."));

    assertThat(result)
        .hasValueSatisfying(
            followUp -> {
              assertThat(followUp.npc().npcCode()).isEqualTo("POLICE_OFFICER");
              assertThat(followUp.item()).isEqualTo("your wallet, found at Maple Diner");
              assertThat(followUp.itemKo()).isEqualTo("지갑 수색 결과");
              assertThat(followUp.remainingSeconds()).isEqualTo(90);
            });
    then(aiGateway).should(never()).followUp(any(), any(), any());
  }

  @Test
  @DisplayName("학습자가 카드를 닫으면 기다리는 목록에서 빠지고 그 NPC가 다시 부르지 않는다. 다른 사용자는 닫을 수 없다")
  void dismiss_removesTheCardAndTheCall() {
    owes(LATTE);
    Long followUpId =
        judge(endedConversation("One iced latte, please.")).orElseThrow().followUpId();
    Long otherUser =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-2", "Moon");

    followUpService.dismiss(otherUser, followUpId);
    assertThat(followUpService.waiting(userId)).hasSize(1);

    followUpService.dismiss(userId, followUpId);
    entityManager.flush();

    assertThat(followUpService.waiting(userId)).isEmpty();
    clock.setInstant(TestClockConfig.NOW.plusSeconds(60));
    assertThat(followUpService.readyFor(userId, cafeStaffId)).isEmpty();
  }

  private void isWaitingLatte(FollowUpResponse followUp) {
    assertThat(followUp.npc().npcCode()).isEqualTo("CAFE_STAFF");
    assertThat(followUp.sceneCode()).isEqualTo("CAFE");
    assertThat(followUp.item()).isEqualTo("iced latte");
    assertThat(followUp.itemKo()).isEqualTo("아이스 라테");
    assertThat(followUp.readyAt().toInstant()).isEqualTo(TestClockConfig.NOW.plusSeconds(45));
    assertThat(followUp.remainingSeconds()).isEqualTo(45);
    assertThat(followUp.ready()).isFalse();
  }

  private void owes(WorkerFollowUp owed) {
    given(aiGateway.followUp(any(), any(), any()))
        .willReturn(new FollowUpResult(owed, List.of(usage("follow_up"))));
  }

  private Optional<FollowUpResponse> judge(Conversation conversation) {
    return followUpService
        .afterConversation(userId, conversation.getId(), UUID.randomUUID())
        .join();
  }

  private Conversation endedConversation(String userText) {
    return endedConversation("CAFE_STAFF", userText);
  }

  private Conversation endedConversation(String npcCode, String userText) {
    Conversation conversation = conversation(entityManager, userId, npcCode);
    npcTurn(entityManager, conversation, 1, "Hi! What can I get for you?");
    textTurn(entityManager, conversation, 2, userText);
    npcTurn(entityManager, conversation, 3, "Coming right up.");
    conversation.endByUser(Instant.parse("2026-11-02T00:59:00Z"));
    entityManager.flush();
    return conversation;
  }
}
