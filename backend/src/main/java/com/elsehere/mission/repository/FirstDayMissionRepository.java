package com.elsehere.mission.repository;

import com.elsehere.mission.entity.FirstDayMission;
import jakarta.persistence.LockModeType;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;

public interface FirstDayMissionRepository extends JpaRepository<FirstDayMission, Long> {
  @Lock(LockModeType.PESSIMISTIC_WRITE)
  @Query("select mission from FirstDayMission mission where mission.userId = :userId")
  Optional<FirstDayMission> findForUpdate(Long userId);
}
