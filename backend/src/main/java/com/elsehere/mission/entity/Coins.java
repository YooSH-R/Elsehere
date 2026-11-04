package com.elsehere.mission.entity;

import java.util.ArrayList;
import java.util.List;
import java.util.function.IntPredicate;

/**
 * 퀘스트 코인. 따로 저장하지 않고 진행에서 계산한다. 넉넉하게 100으로 시작해 값을 치르는 목표({@link Payment})를 마치는 순간 그 값만큼 줄고, 퀘스트를
 * 끝내면 보상({@link Quest})만큼 늘어난다. 지난 단계의 구매는 모두 치렀고 지금 단계는 그 목표를 마쳤을 때만 치른 것으로 세므로 같은 구매로 두 번 줄지 않는다.
 */
public final class Coins {
  public static final int STARTING = 100;

  private Coins() {}

  /** 시작하지 않은 퀘스트는 null로 넘긴다. */
  public static int balance(FirstDayMission firstDay, TownStory story) {
    int spent = paid(firstDay, story).stream().mapToInt(Payment::coins).sum();
    boolean firstDayDone = firstDay != null && firstDay.isCompleted();
    int storyStage = story == null ? 0 : story.getStage();
    return STARTING - spent + Quest.earned(firstDayDone, storyStage);
  }

  /** 가장 최근에 산 것. 아직 산 것이 없으면 null이다. */
  public static String lastPurchase(FirstDayMission firstDay, TownStory story) {
    List<Payment> paid = paid(firstDay, story);
    return paid.isEmpty() ? null : paid.getLast().item();
  }

  // 진행 순서대로 치른 것. 첫날 퀘스트 다음에 마을 이야기다.
  private static List<Payment> paid(FirstDayMission firstDay, TownStory story) {
    List<Payment> paid = new ArrayList<>();
    if (firstDay != null) {
      for (FirstDayStage stage : FirstDayStage.values()) {
        Payment payment = stage.payment();
        if (payment != null
            && isPaid(
                stage.ordinal(),
                stage.intentIds().indexOf(payment.goalId()),
                firstDay.getStage(),
                firstDay.isCompleted(),
                firstDay::hasGoal)) {
          paid.add(payment);
        }
      }
    }
    if (story != null) {
      for (TownStoryStage stage : TownStoryStage.values()) {
        Payment payment = stage.payment();
        if (payment != null
            && isPaid(
                stage.ordinal(),
                stage.goalIds().indexOf(payment.goalId()),
                story.getStage(),
                story.isCompleted(),
                story::hasGoal)) {
          paid.add(payment);
        }
      }
    }
    return paid;
  }

  private static boolean isPaid(
      int stage, int goal, int current, boolean completed, IntPredicate hasGoal) {
    return completed || stage < current || (stage == current && hasGoal.test(goal));
  }
}
