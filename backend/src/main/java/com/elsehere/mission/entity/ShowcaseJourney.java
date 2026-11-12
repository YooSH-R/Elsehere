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

/** 시연 진행과 결제를 일반 퀘스트와 분리한다. 대화·기억·호감도는 기존 실제 기록을 사용한다. */
@Entity
@Table(name = "showcase_journeys")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class ShowcaseJourney extends BaseEntity {
  @Id private Long userId;

  @Column(nullable = false)
  private int stage;

  @Column(nullable = false)
  private int objectives;

  @Column(nullable = false)
  private boolean active;

  @Column(nullable = false)
  private boolean umbrellaBorrowed;

  @Column(nullable = false)
  private boolean mealPaid;

  @Column(nullable = false)
  private boolean taxiRequested;

  @Column(nullable = false, length = 20)
  private String scene;

  @Version private long version;

  public static ShowcaseJourney start(Long userId) {
    ShowcaseJourney journey = new ShowcaseJourney();
    journey.userId = userId;
    journey.restart();
    return journey;
  }

  public void restart() {
    stage = 0;
    objectives = 0;
    active = true;
    umbrellaBorrowed = false;
    mealPaid = false;
    taxiRequested = false;
    scene = "HOTEL";
  }

  public void stop() {
    active = false;
  }

  public void resume() {
    active = true;
  }

  public ShowcaseStage current() {
    return isCompleted() ? null : ShowcaseStage.values()[stage];
  }

  public boolean isCompleted() {
    return stage == ShowcaseStage.values().length;
  }

  public boolean hasUmbrella() {
    return umbrellaBorrowed && !isCompleted();
  }

  public boolean hasGoal(int index) {
    return (objectives & (1 << index)) != 0;
  }

  public int balance() {
    return Coins.STARTING - (mealPaid ? 7 : 0);
  }

  public String weather() {
    return stage < ShowcaseStage.RETURN.ordinal() ? "RAIN" : "CLEAR";
  }

  public void visit(String scene) {
    this.scene = scene;
  }

  public void accept(int expectedStage, String npcCode, List<String> goals) {
    if (!active
        || isCompleted()
        || stage != expectedStage
        || !current().npcCode().equals(npcCode)
        || current() == ShowcaseStage.TAXI
        || goals == null) {
      return;
    }
    List<String> allowed = current().goalIds();
    for (String goal : goals) {
      int index = allowed.indexOf(goal);
      if (index >= 0) {
        objectives |= 1 << index;
      }
    }
    if (objectives == (1 << allowed.size()) - 1) {
      if (current() == ShowcaseStage.HOTEL) {
        umbrellaBorrowed = true;
      }
      if (current() == ShowcaseStage.ORDER) {
        mealPaid = true;
      }
      advance();
    }
  }

  public void chooseTaxiDestination(String npcCode, String destination) {
    if (active && current() == ShowcaseStage.TAXI && "TAXI_DRIVER".equals(npcCode)) {
      taxiRequested = "DINER".equals(destination);
    }
  }

  public void arriveByTaxi(String destination) {
    if (active && current() == ShowcaseStage.TAXI && taxiRequested && "DINER".equals(destination)) {
      scene = "TOWN";
      advance();
    }
  }

  private void advance() {
    stage++;
    objectives = 0;
  }
}
