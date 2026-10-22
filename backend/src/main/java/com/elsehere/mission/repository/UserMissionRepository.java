package com.elsehere.mission.repository;

import com.elsehere.mission.entity.UserMission;
import org.springframework.data.jpa.repository.JpaRepository;

public interface UserMissionRepository extends JpaRepository<UserMission, Long> {}
