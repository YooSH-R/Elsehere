package com.elsehere.world.repository;

import com.elsehere.world.entity.UserDailyActivity;
import com.elsehere.world.entity.UserDailyActivityId;
import java.time.Instant;
import java.time.LocalDate;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;

public interface UserDailyActivityRepository
    extends JpaRepository<UserDailyActivity, UserDailyActivityId> {

  /** 그날의 첫 신호면 접속을 남기고, 이미 있으면 마지막 접속 시각을 바꾸고 머문 초를 더한다. 여러 창에서 신호가 동시에 와도 한 줄만 남는다. */
  @Modifying
  @Query(
      value =
          """
          insert into user_daily_activities
            (user_id, activity_date, first_seen_at, last_seen_at, active_seconds)
          values (:userId, :day, :now, :now, :stayedSeconds)
          on conflict (activity_date, user_id) do update
          set last_seen_at = greatest(user_daily_activities.last_seen_at, excluded.last_seen_at),
              active_seconds = user_daily_activities.active_seconds + excluded.active_seconds,
              updated_at = now()
          """,
      nativeQuery = true)
  void recordVisit(Long userId, LocalDate day, Instant now, long stayedSeconds);
}
