package com.elsehere.world.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import java.time.Duration;
import java.time.Instant;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.ColumnDefault;

/** 사용자별 게임 시계. 플레이한 실제 시간만 쌓는다. 사용자(users)는 ID만 보관한다. */
@Entity
@Table(name = "user_game_clocks")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserGameClock extends BaseEntity {

  @Id private Long userId;

  // 시간이 흐른 실제 플레이 초. 실제 600초가 게임 하루다.
  @ColumnDefault("0")
  @Column(nullable = false)
  private long playSeconds;

  // 마지막으로 받은 신호 시각. 다음 신호와의 간격만큼 시간이 흐른다.
  private Instant lastBeatAt;

  /**
   * 플레이 중 신호를 받아 지난 신호 뒤 흐른 실제 시간을 더한다. 첫 신호는 시계를 시작만 하고, 대화 중이면 더하지 않으며, 간격이 길면(창을 닫았거나 백그라운드)
   * {@code maxStep}까지만 더한다.
   */
  public void beat(Instant now, boolean paused, Duration maxStep) {
    beat(now, paused, maxStep, null);
  }

  /** 보고된 실제 진행 시간도 서버 신호 간격과 상한 안에서만 반영한다. */
  public void beat(Instant now, boolean paused, Duration maxStep, Long activeSeconds) {
    if (lastBeatAt != null && !paused) {
      long elapsed = Duration.between(lastBeatAt, now).getSeconds();
      long allowed = Math.clamp(elapsed, 0, maxStep.getSeconds());
      playSeconds += activeSeconds == null ? allowed : Math.clamp(activeSeconds, 0, allowed);
    }
    lastBeatAt = now;
  }
}
