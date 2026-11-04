package com.elsehere.world.repository;

import com.elsehere.world.entity.UserGameClock;
import jakarta.persistence.LockModeType;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;

public interface UserGameClockRepository extends JpaRepository<UserGameClock, Long> {

  /** 시계가 없으면 0초로 만든다. 여러 창에서 첫 신호가 동시에 와도 하나만 만든다. */
  @Modifying
  @Query(
      value =
          "insert into user_game_clocks (user_id) values (:userId)"
              + " on conflict (user_id) do nothing",
      nativeQuery = true)
  void insertIfAbsent(Long userId);

  @Lock(LockModeType.PESSIMISTIC_WRITE)
  @Query("select clock from UserGameClock clock where clock.userId = :userId")
  Optional<UserGameClock> findForUpdate(Long userId);
}
