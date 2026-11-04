package com.elsehere.mission.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.Version;
import java.util.List;

@Entity
@Table(name = "town_stories")
public class TownStory extends BaseEntity {
  @Id private Long userId;

  @Column(nullable = false)
  private int stage;

  @Column(nullable = false)
  private int completedGoals;

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

  public boolean hasGoal(int index) {
    return (completedGoals & (1 << index)) != 0;
  }

  public void accept(int expectedStage, String npcCode, List<String> intents) {
    if (stage >= TownStoryStage.values().length || expectedStage != stage || intents == null) {
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
    if (completedGoals == (1 << current.goalIds().size()) - 1) {
      stage = expectedStage + 1;
      completedGoals = 0;
    }
  }
}
