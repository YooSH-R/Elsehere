package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.review.dto.ReviewCardResponse;
import com.elsehere.review.dto.ReviewScenario;
import com.elsehere.review.dto.ReviewTarget;
import java.util.List;
import org.junit.jupiter.api.Test;
import tools.jackson.databind.json.JsonMapper;

class ReviewDialogueScenarioTest {
  @Test
  void npcReceivesTheSameFocusedSituationAndBagFactsAsTheLearner() {
    var scenario =
        new ReviewScenario(
            "first_day_FIND_BAG",
            "분실 가방 찾기",
            "카페에서 두고 간 가방을 찾아요.",
            "가방 주인",
            List.of("파란색 숄더백과 빨간 열쇠고리", "여권 지갑과 책"));
    var card =
        new ReviewCardResponse(
            1L,
            2L,
            "CAFE",
            3L,
            new NpcSummaryResponse("CAFE_STAFF", "Mia", "카페 직원"),
            "이전에 음료를 주문했고 가방도 돌려받았어요.",
            List.of(),
            0,
            null,
            List.of(),
            scenario);
    var dialogue = new ReviewDialogue(JsonMapper.builder().build());
    assertThat(dialogue.instruction(card))
        .contains("파란색 숄더백", "여권 지갑과 책")
        .doesNotContain("이전에 음료를 주문했고");
    assertThat(dialogue.state(card).desc()).contains("분실 가방 찾기");
  }

  // 운영 11/13: 목표 표현 다섯 개의 한국어 뜻·용법·예문까지 넣은 지시문이 워커 상한(3000자)을 넘어 복습 시작이 400 → 500이 됐다.
  @Test
  void npcReceivesOnlyTargetExpressionsSoAFullCardFitsTheWorkerLimit() {
    var targets =
        List.of(
            target("Could you keep my suitcase until check-in?", "체크인 전까지 캐리어 맡아 주실 수 있나요?"),
            target("I have a reservation under Kim.", "김으로 예약했어요."),
            target("Is there a cafe nearby where I can wait?", "기다릴 만한 카페가 근처에 있나요?"),
            target("What time can I check in?", "몇 시에 체크인할 수 있어요?"),
            target("Thank you so much for your help.", "도와주셔서 정말 고마워요."));
    var card = card(targets, scenario("호텔에서 짐을 맡기고 체크인 시간을 물었어요.", List.of("예약자 이름: 본인의 예약자 이름")));
    var dialogue = new ReviewDialogue(JsonMapper.builder().build());

    String instruction = dialogue.instruction(card);

    assertThat(instruction.length()).isLessThanOrEqualTo(ReviewDialogue.MAX_LENGTH);
    assertThat(instruction)
        .contains("Could you keep my suitcase until check-in?", "What time can I check in?")
        .doesNotContain("체크인 전까지 캐리어 맡아 주실 수 있나요?", USAGE_KO);
  }

  @Test
  void overlongSavedSituationIsClippedToTheLimit() {
    String longText = "아주 긴 저장 상황 설명입니다. ".repeat(200);
    var targets =
        List.of(
            target("a".repeat(600), "뜻"),
            target("b".repeat(600), "뜻"),
            target("c".repeat(600), "뜻"),
            target("d".repeat(600), "뜻"),
            target("e".repeat(600), "뜻"),
            target("f".repeat(600), "뜻"));
    var card =
        card(
            targets, scenario(longText, List.of(longText, longText, longText, longText, longText)));
    var dialogue = new ReviewDialogue(JsonMapper.builder().build());

    assertThat(dialogue.instruction(card).length()).isLessThanOrEqualTo(ReviewDialogue.MAX_LENGTH);
  }

  private static final String USAGE_KO =
      "호텔 프런트에서 체크인 전에 짐을 맡기거나 예약을 확인할 때 공손하게 부탁하는 표현이에요. Could you로 시작하면 정중한 요청이 되고,"
          + " until 뒤에 시점을 붙여 언제까지인지 말할 수 있어요. 비슷한 상황에서 그대로 바꿔 쓸 수 있어요.";

  private static ReviewTarget target(String expression, String originalText) {
    return new ReviewTarget(
        expression,
        "체크인 전에 짐을 맡아 달라고 정중하게 부탁하기",
        USAGE_KO,
        "Could you hold my bags until the room is ready?",
        "방이 준비될 때까지 가방을 맡아 주실 수 있나요?",
        originalText);
  }

  private static ReviewScenario scenario(String situation, List<String> notes) {
    return new ReviewScenario("first_day_EARLY_ARRIVAL", "예약 확인과 짐 맡기기", situation, "여행자", notes);
  }

  private static ReviewCardResponse card(List<ReviewTarget> targets, ReviewScenario scenario) {
    return new ReviewCardResponse(
        1L,
        2L,
        "HOTEL",
        3L,
        new NpcSummaryResponse("HOTEL_STAFF", "Grace", "호텔 직원"),
        scenario.situation(),
        targets,
        0,
        null,
        List.of(),
        scenario);
  }
}
