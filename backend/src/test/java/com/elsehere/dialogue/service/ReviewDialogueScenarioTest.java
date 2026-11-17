package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.review.dto.ReviewCardResponse;
import com.elsehere.review.dto.ReviewScenario;
import com.elsehere.review.dto.ReviewTarget;
import java.util.List;
import java.util.stream.IntStream;
import org.junit.jupiter.api.Test;
import tools.jackson.databind.json.JsonMapper;

class ReviewDialogueScenarioTest {
  // 워커는 NPC 지시문을 3,000자까지 받는다. 화면에만 쓰는 해석·설명·예문은 NPC에게 보내지 않고 연습할 영어 표현만 보낸다.
  @Test
  void npcReceivesOnlyTheEnglishExpressionsSoTenTargetsStayWithinTheWorkerLimit() {
    List<ReviewTarget> targets =
        IntStream.range(0, 10)
            .mapToObj(
                i ->
                    new ReviewTarget(
                        "Could I get a cheeseburger without onions, please? " + i,
                        "양파를 빼고 치즈버거를 주실 수 있나요? 화면에만 보이는 해석 " + i,
                        "주문할 때 빼 달라는 재료를 정중하게 말하는 표현이에요. 화면에만 보이는 설명 " + i,
                        "Could I get a burger without pickles, please? " + i,
                        "피클 빼고 버거 주실 수 있나요? " + i))
            .toList();
    var card =
        new ReviewCardResponse(
            1L,
            2L,
            "TOWN",
            3L,
            new NpcSummaryResponse("DINER_STAFF", "Rosa", "햄버거 가게 직원"),
            "햄버거를 주문했어요.",
            targets,
            0,
            null,
            List.of(),
            new ReviewScenario(
                "showcase_ORDER",
                "햄버거 주문 다시 연습",
                "비 오는 저녁 햄버거 가게에서 주문해요.",
                "손님",
                List.of("치즈버거 한 개를 주문해 보세요.", "치즈버거의 양파를 빼 달라고 말해 보세요.")));

    String instruction = new ReviewDialogue(JsonMapper.builder().build()).instruction(card);

    assertThat(instruction).hasSizeLessThanOrEqualTo(3000);
    // 시연 주문을 다시 연습하면 주문 조건 판정 지시가 붙는다. 조건을 하나도 말하지 않은 턴(가장 긴 지시)도 제한 안이다.
    String reply =
        com.elsehere.mission.service.ShowcaseReviewGoals.talk("showcase_ORDER", List.of())
            .orElseThrow()
            .replyInstruction(new com.elsehere.ai.client.JudgeResult.Intent(null, List.of(), null));
    assertThat(instruction.length() + 1 + reply.length()).isLessThanOrEqualTo(3000);
    // 표현은 지시문 상한(S15P31A305-935)에 맞춰 앞의 다섯 개만 보낸다. 시연 주문의 목표는 조건 네 가지라 다 들어간다.
    assertThat(instruction)
        .contains("Could I get a cheeseburger without onions, please? 4", "양파를 빼 달라고")
        .doesNotContain(
            "Could I get a cheeseburger without onions, please? 5",
            "화면에만 보이는 해석",
            "화면에만 보이는 설명",
            "pickles");
  }

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
