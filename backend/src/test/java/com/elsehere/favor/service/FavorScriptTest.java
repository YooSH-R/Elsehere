package com.elsehere.favor.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.ai.client.WorkerState;
import com.elsehere.favor.entity.Favor;
import com.elsehere.favor.entity.FavorStatus;
import com.elsehere.favor.service.FavorScript.Role;
import com.elsehere.favor.service.FavorScript.Step;
import java.util.List;
import java.util.Optional;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

// 돌발 부탁의 판정 의도·지시문·카드 문구(설계 7.7·7.8·7.11). 문장은 규칙 데이터에서 만든다.
class FavorScriptTest {

  private static final FavorRules RULES = new FavorRules();

  @Test
  @DisplayName("심부름 카드는 부탁한 NPC 이름과 제공의 할 일, 해결처·돌아가기 두 단계를 보인다")
  void errandCardTitleAndSteps() {
    Favor favor = croissantForJake();

    assertThat(FavorScript.title(RULES, favor)).isEqualTo("Jake의 부탁 · 크루아상 사다 주기");
    assertThat(FavorScript.steps(RULES, favor))
        .containsExactly(
            new Step("BAKERY", null, "베이커리에서 크루아상 포장 주문하기", false),
            new Step("HOTEL", "HOTEL_STAFF", "Jake에게 돌아가기", false));
  }

  @Test
  @DisplayName("대화 안 부탁 카드는 계기 틀의 제목 한 단계다")
  void talkCardHasOneStep() {
    Favor favor =
        Favor.offer(1L, "HOTEL_STAFF", "Jake", "recommend_a_spot", null, null, 3, 10L, 4, 6);

    assertThat(FavorScript.title(RULES, favor)).isEqualTo("Jake의 부탁 · 구경할 곳 추천하기");
    assertThat(FavorScript.steps(RULES, favor))
        .containsExactly(new Step("HOTEL", "HOTEL_STAFF", "구경할 곳 추천하기", false));
  }

  @Test
  @DisplayName("부탁을 꺼내는 지시문에는 이유와 해결처·물건의 영어 이름이 들어간다")
  void offerInstructionCarriesTheReasonPlaceAndItem() {
    var request = RULES.request("bring_food").orElseThrow();
    var offer = RULES.offer("BAKERY", "croissant").orElseThrow();

    String instruction =
        FavorScript.offerInstruction(RULES, new FavorPicker.Pick(request, "BAKERY", offer));

    assertThat(instruction).contains(request.reason(), "a croissant", "Maple Bakery");
  }

  @Test
  @DisplayName("상황 심부름은 물건을 사다 달라고 하지 않고 해결처에서 그 일을 대신 봐 달라고 한다")
  void aSituationErrandAsksToTakeCareOfItThere() {
    var request = RULES.request("letter_to_send").orElseThrow();
    var offer = RULES.offer("POST_OFFICE", "stamp").orElseThrow();

    String instruction =
        FavorScript.offerInstruction(RULES, new FavorPicker.Pick(request, "POST_OFFICE", offer));

    assertThat(instruction)
        .contains("take care of it", "the post office")
        .doesNotContain("get you");
  }

  @Test
  @DisplayName("부탁한 NPC, 수락한 심부름의 해결처 직원, 그 밖의 NPC를 가른다")
  void rolesOfTheNpcInTheConversation() {
    Favor favor = croissantForJake();
    favor.moveTo(FavorStatus.ACCEPTED, 4000, 720);

    assertThat(FavorScript.roleOf(RULES, favor, "HOTEL_STAFF")).isEqualTo(Role.REQUESTER);
    assertThat(FavorScript.roleOf(RULES, favor, "BAKERY_STAFF")).isEqualTo(Role.SHOP);
    assertThat(FavorScript.roleOf(RULES, favor, "CAFE_STAFF")).isEqualTo(Role.NONE);
  }

  @Test
  @DisplayName("상태와 상대에 따라 판정할 의도가 다르다")
  void intentsFollowTheStateAndTheNpc() {
    Favor errand = croissantForJake();
    assertThat(ids(FavorScript.intents(RULES, errand, Role.REQUESTER)))
        .containsExactly("accept_favor", "decline_favor");

    errand.moveTo(FavorStatus.ACCEPTED, 4000, 720);
    assertThat(ids(FavorScript.intents(RULES, errand, Role.SHOP)))
        .containsExactly("get_for_requester");
    assertThat(FavorScript.intents(RULES, errand, Role.REQUESTER)).isEmpty();

    errand.moveTo(FavorStatus.GOT, 4100, 720);
    assertThat(ids(FavorScript.intents(RULES, errand, Role.REQUESTER)))
        .containsExactly("hand_over");

    Favor talk =
        Favor.offer(1L, "HOTEL_STAFF", "Jake", "recommend_a_spot", null, null, 3, 10L, 4, 6);
    assertThat(ids(FavorScript.intents(RULES, talk, Role.REQUESTER)))
        .containsExactly("recommend_place");
  }

  @Test
  @DisplayName("판정한 의도로 수락·거절·해결처 완료·건넴·대화 안 해결이 다음 상태가 된다")
  void advanceByTheJudgedIntent() {
    Favor errand = croissantForJake();
    assertThat(FavorScript.advance(RULES, errand, Role.REQUESTER, List.of("accept_favor")))
        .contains(FavorStatus.ACCEPTED);
    assertThat(FavorScript.advance(RULES, errand, Role.REQUESTER, List.of("decline_favor")))
        .contains(FavorStatus.DECLINED);
    assertThat(FavorScript.advance(RULES, errand, Role.REQUESTER, List.of("chat"))).isEmpty();

    errand.moveTo(FavorStatus.ACCEPTED, 4000, 720);
    assertThat(FavorScript.advance(RULES, errand, Role.SHOP, List.of("get_for_requester")))
        .contains(FavorStatus.GOT);

    errand.moveTo(FavorStatus.GOT, 4100, 720);
    assertThat(FavorScript.advance(RULES, errand, Role.REQUESTER, List.of("hand_over")))
        .contains(FavorStatus.DONE);

    Favor talk =
        Favor.offer(1L, "HOTEL_STAFF", "Jake", "recommend_a_spot", null, null, 3, 10L, 4, 6);
    assertThat(FavorScript.advance(RULES, talk, Role.REQUESTER, List.of("recommend_place")))
        .contains(FavorStatus.DONE);
  }

  @Test
  @DisplayName("수락하면 어디로 가는지 말하게 하고, 해결처 직원에게는 누구 대신 사는지 알린다")
  void instructionsAfterAcceptingAndAtTheShop() {
    Favor errand = croissantForJake();
    errand.moveTo(FavorStatus.ACCEPTED, 4000, 720);

    assertThat(
            FavorScript.instruction(
                RULES, errand, Role.REQUESTER, Optional.of(FavorStatus.ACCEPTED)))
        .contains("agreed", "Maple Bakery");
    assertThat(FavorScript.instruction(RULES, errand, Role.SHOP, Optional.empty()))
        .contains("a croissant", "Jake");
  }

  private static Favor croissantForJake() {
    return Favor.offer(
        1L, "HOTEL_STAFF", "Jake", "bring_food", "BAKERY", "croissant", 3, 10L, 6, 8);
  }

  private static List<String> ids(List<WorkerState.Intent> intents) {
    return intents.stream().map(WorkerState.Intent::id).toList();
  }
}
