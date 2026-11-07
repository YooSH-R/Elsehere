package com.elsehere.mission.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.Version;
import java.util.List;
import org.hibernate.annotations.ColumnDefault;

@Entity
@Table(name = "town_stories")
public class TownStory extends BaseEntity {
  // DINER의 세 번째 비트는 대화 목표가 아니라 착석 후 식사를 받은 상태다. 기존 단계 순서는 유지한다.
  private static final int DINER_MEAL_RECEIVED = 1 << 2;

  public boolean hasDinerMeal() {
    return stage > 0 || (completedGoals & DINER_MEAL_RECEIVED) != 0;
  }

  public boolean canWaitForDinerMeal() {
    return !paused && stage == 0 && hasGoal(0) && !hasDinerMeal();
  }

  public void receiveDinerMeal() {
    if (canWaitForDinerMeal()) {
      completedGoals |= DINER_MEAL_RECEIVED;
    }
  }

  @Id private Long userId;

  @Column(nullable = false)
  private int stage;

  @Column(nullable = false)
  private int completedGoals;

  // 퀘스트 하나를 끝내 다음 퀘스트를 시작하기를 기다리는지. 그동안 대화는 진행을 바꾸지 않는다.
  @ColumnDefault("false")
  @Column(nullable = false)
  private boolean paused;

  @Version private long version;

  protected TownStory() {}

  public static TownStory start(Long userId) {
    TownStory story = new TownStory();
    story.userId = userId;
    return story;
  }

  public int getStage() {
    return stage;
  }

  public boolean isCompleted() {
    return stage == TownStoryStage.values().length;
  }

  public TownStoryStage current() {
    return isCompleted() ? null : TownStoryStage.values()[stage];
  }

  /** 퀘스트 하나를 끝내 다음 퀘스트를 시작하기를 기다리는지 */
  public boolean isPaused() {
    return paused;
  }

  /** 퀘스트 목록에서 다음 퀘스트를 시작한다. */
  public void resume() {
    paused = false;
  }

  public boolean hasGoal(int index) {
    return (completedGoals & (1 << index)) != 0;
  }

  public void accept(int expectedStage, String npcCode, List<String> intents) {
    if (paused
        || stage >= TownStoryStage.values().length
        || expectedStage != stage
        || intents == null) {
      return;
    }
    TownStoryStage current = TownStoryStage.values()[stage];
    if (!current.npcCode().equals(npcCode)) {
      return;
    }
    for (String intent : intents) {
      int index = current.goalIds().indexOf(intent);
      if (index >= 0) {
        completedGoals |= 1 << index;
      }
    }
    int goalMask = (1 << current.goalIds().size()) - 1;
    if ((completedGoals & goalMask) == goalMask) {
      stage = expectedStage + 1;
      completedGoals = 0;
      // 퀘스트 하나를 끝내면 다음 퀘스트를 시작할 때까지 멈춘다.
      paused = Quest.startsNextQuest(stage);
    }
  }
}
