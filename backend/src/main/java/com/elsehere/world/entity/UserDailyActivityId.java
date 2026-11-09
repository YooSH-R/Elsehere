package com.elsehere.world.entity;

import jakarta.persistence.Embeddable;
import java.io.Serializable;
import java.time.LocalDate;
import lombok.AccessLevel;
import lombok.AllArgsConstructor;
import lombok.EqualsAndHashCode;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Embeddable
@Getter
@EqualsAndHashCode
@NoArgsConstructor(access = AccessLevel.PROTECTED)
@AllArgsConstructor
public class UserDailyActivityId implements Serializable {

  private Long userId;

  // 한국 날짜(ClockConfig.SERVICE_ZONE)
  private LocalDate activityDate;
}
