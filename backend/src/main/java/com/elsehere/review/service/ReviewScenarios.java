package com.elsehere.review.service;

import com.elsehere.conversation.entity.Conversation;
import com.elsehere.mission.entity.FirstDayStage;
import com.elsehere.mission.entity.TownStoryStage;
import com.elsehere.review.dto.ReviewScenario;
import java.util.List;

final class ReviewScenarios {
  private ReviewScenarios() {}

  static ReviewScenario capture(Conversation source, String npcCode, String summary) {
    Integer firstDay = source == null ? null : source.getFirstDayStage();
    if (firstDay != null && firstDay >= 0 && firstDay < FirstDayStage.values().length) {
      var stage = FirstDayStage.values()[firstDay];
      List<String> goalIds = source.getFirstDayGoalIds();
      if (stage.npc().equals(npcCode) && goalIds != null && !goalIds.isEmpty()) {
        List<FirstDayStage.Goal> goals =
            stage.goals().stream().filter(goal -> goalIds.contains(goal.id())).toList();
        List<String> notes =
            goals.stream().map(goal -> goal.hint().replace("{name}", "본인의 예약자 이름")).toList();
        boolean allGoalsPending = goals.size() == stage.goals().size();
        String title;
        if (allGoalsPending) {
          title = firstDayReviewTitle(stage);
        } else {
          title = goals.size() == 1 ? goals.getFirst().title() : stage.title();
        }
        String situation =
            allGoalsPending ? stage.situation() : title + ". " + String.join(". ", notes);
        return new ReviewScenario(
            "first_day_" + stage.name() + "_" + String.join("_", goalIds),
            title,
            situation,
            "여행자",
            notes);
      }
    }
    Integer story = source == null ? null : source.getTownStoryStage();
    if (story != null && story >= 0 && story < TownStoryStage.values().length) {
      var stage = TownStoryStage.values()[story];
      List<String> goals = source.getTownStoryGoalIds();
      if (stage.npcCode().equals(npcCode) && goals != null && !goals.isEmpty()) {
        List<String> notes = goals.stream().map(TownStoryStage::goalHint).toList();
        String title =
            goals.size() == 1 ? TownStoryStage.goalTitle(goals.getFirst()) : stage.title();
        return new ReviewScenario(
            "town_story_" + stage.name() + "_" + String.join("_", goals),
            title,
            title + ". " + String.join(". ", notes),
            "손님",
            notes);
      }
      // 식당의 예전 기록은 주문과 쏟은 사건 중 어느 대화였는지 알 수 없다.
      if (stage.npcCode().equals(npcCode) && goals == null && stage != TownStoryStage.DINER) {
        return new ReviewScenario(
            "town_story_" + stage.name(),
            stage.title(),
            stage.situation(),
            "손님",
            stage.goalIds().stream().map(TownStoryStage::goalHint).toList());
      }
    }
    String saved =
        summary == null || summary.isBlank() ? "저장한 표현을 사용해 이 NPC에게 말을 걸어 보세요." : summary;
    String title = saved.split("[.。!?\\n]", 2)[0].strip();
    if (title.length() > 42) {
      title = title.substring(0, 42) + "…";
    }
    return new ReviewScenario(
        "saved_conversation",
        title,
        saved,
        "대화를 시작하는 사람",
        List.of(
            "위에 저장된 상황을 처음부터 다시 대화해 보세요.",
            "구체적인 정보가 없는 경우 색·물건·수량 등을 직접 정해서 말해도 돼요. 첫 질문에서 정한 정보를 계속 사용해요."));
  }

  private static String firstDayReviewTitle(FirstDayStage stage) {
    return switch (stage) {
      case EARLY_ARRIVAL -> "예약 확인과 짐 맡기기";
      case CAFE_BREAK -> "음료 주문하기";
      case MISSING_BAG -> "가방을 두고 온 사정 설명하기";
      case FIND_BAG -> "분실 가방 찾기";
      case RAINY_STREET -> "우산을 살 곳과 길 묻기";
      case BUY_UMBRELLA -> "우산 구매하기";
      case CHECK_IN -> "호텔 체크인하기";
    };
  }
}
