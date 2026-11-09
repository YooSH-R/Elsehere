package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.review.dto.ReviewCardResponse;
import com.elsehere.review.dto.ReviewScenario;
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
}
