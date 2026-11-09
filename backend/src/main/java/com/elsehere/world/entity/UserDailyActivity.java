package com.elsehere.world.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.EmbeddedId;
import jakarta.persistence.Entity;
import jakarta.persistence.Table;
import java.time.Instant;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.ColumnDefault;

/**
 * 사용자가 장소 화면에 들어온 한국 날짜 하루. 게임 시계 신호(WORLD-07)를 받을 때마다 그날의 첫·마지막 접속 시각과 머문 시간을 쌓아, 어드민 이용 현황이 활동한
 * 날과 다시 온 사용자(D1·D7)를 셀 수 있게 한다.
 */
@Entity
@Table(name = "user_daily_activities")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserDailyActivity extends BaseEntity {

  @EmbeddedId private UserDailyActivityId id;

  @Column(nullable = false)
  private Instant firstSeenAt;

  @Column(nullable = false)
  private Instant lastSeenAt;

  // 장소 화면에 머문 실제 초. 대화·학습 창을 보는 시간도 넣고, 신호가 60초 넘게 끊긴 구간은 뺀다.
  @ColumnDefault("0")
  @Column(nullable = false)
  private long activeSeconds;
}
