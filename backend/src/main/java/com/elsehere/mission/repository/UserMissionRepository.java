package com.elsehere.mission.repository;

import com.elsehere.mission.entity.UserMission;
import java.time.LocalDate;
import java.util.List;
import org.springframework.data.jpa.repository.JpaRepository;

public interface UserMissionRepository extends JpaRepository<UserMission, Long> {

  List<UserMission> findAllByUserIdAndMissionDate(Long userId, LocalDate missionDate);
}
