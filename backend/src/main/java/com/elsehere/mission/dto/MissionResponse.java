package com.elsehere.mission.dto;

import com.elsehere.mission.entity.Mission;
import com.elsehere.mission.entity.UserMission;
import com.elsehere.mission.entity.UserMissionStatus;
import com.elsehere.world.entity.Npc;
import java.time.OffsetDateTime;
import java.time.ZoneId;

/**
 * MIS-01 미션 한 개와 사용자의 진행 상태. 완료 판정 기준(keywords, intent_rule)은 서버 내부 정보라 담지 않는다.
 *
 * @param missionCode Unity 미션 콘텐츠와 같은 고정 키(missions.code)
 * @param completedAt 완료 시각. 진행 중이면 null이다.
 */
public record MissionResponse(
    Long missionId,
    String missionCode,
    String title,
    String description,
    String sceneCode,
    String npcCode,
    String npcName,
    Long userMissionId,
    UserMissionStatus status,
    OffsetDateTime completedAt) {

  /** 시각은 미션 날짜와 같은 시간대({@code zone})로 보여준다. */
  public static MissionResponse from(UserMission userMission, ZoneId zone) {
    Mission mission = userMission.getMission();
    Npc npc = mission.getNpc();
    OffsetDateTime completedAt =
        userMission.getCompletedAt() == null
            ? null
            : userMission.getCompletedAt().atZone(zone).toOffsetDateTime();
    return new MissionResponse(
        mission.getId(),
        mission.getCode(),
        mission.getTitle(),
        mission.getDescription(),
        npc.getScene().getCode(),
        npc.getCode(),
        npc.getName(),
        userMission.getId(),
        userMission.getStatus(),
        completedAt);
  }
}
