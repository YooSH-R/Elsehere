package com.elsehere.mission.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.Version;
import java.util.List;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Entity
@Table(name = "first_day_missions")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class FirstDayMission extends BaseEntity {
  @Id private Long userId;

  @Column(nullable = false)
  private int stage;

  @Column(nullable = false)
  private int objectives;

  @Column(nullable = false)
  private int balance = 20;

  @Version private long version;

  @Column(nullable = false, length = 20)
  private String scene = "HOME";

  public static FirstDayMission start(Long userId) {
    FirstDayMission mission = new FirstDayMission();
    mission.userId = userId;
    return mission;
  }

  public int getStage() {
    return stage;
  }

  public boolean restAtCafe() {
    if (stage != 1 || !goalsCompleted()) {
      return false;
    }
    advance();
    return true;
  }

  public int getBalance() {
    return balance;
  }

  public boolean isCompleted() {
    return stage == FirstDayStage.values().length;
  }

  public FirstDayStage current() {
    return isCompleted() ? null : FirstDayStage.values()[stage];
  }

  public boolean hasGoal(int index) {
    return (objectives & (1 << index)) != 0;
  }

  public void visit(String scene) {
    this.scene = scene;
  }

  public void accept(int expectedStage, String npcCode, List<String> intents) {
    if (intents == null
        || isCompleted()
        || expectedStage != stage
        || !current().npc().equals(npcCode)) {
      return;
    }
    for (String intent : intents) {
      int index = current().intentIds().indexOf(intent);
      if (index >= 0) {
        objectives |= 1 << index;
      }
    }
    if (stage != 1 && goalsCompleted()) {
      if (stage == 5) {
        balance -= 8;
      }
      advance();
    }
  }

  private boolean goalsCompleted() {
    return objectives == (1 << current().goals().size()) - 1;
  }

  public boolean canRest() {
    return stage == 1 && goalsCompleted();
  }

  private void advance() {
    stage++;
    objectives = 0;
  }
}
