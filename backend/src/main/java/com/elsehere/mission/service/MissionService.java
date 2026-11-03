package com.elsehere.mission.service;

import com.elsehere.common.config.ClockConfig;
import com.elsehere.mission.dto.MissionListResponse;
import com.elsehere.mission.dto.MissionResponse;
import com.elsehere.mission.entity.UserMission;
import com.elsehere.mission.repository.MissionRepository;
import com.elsehere.mission.repository.UserMissionRepository;
import java.time.Clock;
import java.time.LocalDate;
import java.time.OffsetDateTime;
import java.time.ZoneId;
import java.util.List;
import java.util.Map;
import java.util.function.Function;
import java.util.stream.Collectors;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 오늘의 미션 조회와 자동 시작(MIS-01). 수락 없이 조회할 때 진행 중(RUN)으로 만든다. */
@Service
@RequiredArgsConstructor
@Transactional
public class MissionService {

  /** 일일 미션 날짜 기준. MVP는 한국 시간으로 고정하며 자정에 바뀐다. */
  private static final ZoneId MISSION_ZONE = ClockConfig.SERVICE_ZONE;

  private final MissionRepository missionRepository;
  private final UserMissionRepository userMissionRepository;
  private final Clock clock;

  public MissionListResponse getTodayMissions(Long userId) {
    LocalDate today = LocalDate.now(clock.withZone(MISSION_ZONE));
    // 오늘 이미 시작한 미션은 그대로 쓰고, 아직 없는 미션만 새로 시작한다.
    Map<Long, UserMission> startedByMissionId =
        userMissionRepository.findAllByUserIdAndMissionDate(userId, today).stream()
            .collect(
                Collectors.toMap(
                    userMission -> userMission.getMission().getId(), Function.identity()));
    List<UserMission> userMissions =
        missionRepository.findAllByNpcSceneOpenTrueOrderByIdAsc().stream()
            .map(
                mission ->
                    startedByMissionId.computeIfAbsent(
                        mission.getId(),
                        missionId ->
                            userMissionRepository.save(UserMission.start(userId, mission, today))))
            .toList();
    return new MissionListResponse(
        today,
        nextReset(today),
        userMissions.stream()
            .map(userMission -> MissionResponse.from(userMission, MISSION_ZONE))
            .toList());
  }

  private static OffsetDateTime nextReset(LocalDate today) {
    return today.plusDays(1).atStartOfDay(MISSION_ZONE).toOffsetDateTime();
  }
}
