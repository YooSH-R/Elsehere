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
    assertThat(instruction)
        .contains("Could I get a cheeseburger without onions, please? 9", "양파를 빼 달라고")
        .doesNotContain("화면에만 보이는 해석", "화면에만 보이는 설명", "pickles");
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
}
