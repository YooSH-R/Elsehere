package com.elsehere.mission.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Index;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import java.time.Instant;
import java.time.LocalDate;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/**
 * 사용자의 일일 미션 진행. 수락 없이 그날 첫 미션 조회 때 RUN으로 만든다.
 *
 * <p>사용자(users)와 대화(conversations)는 다른 담당 영역의 테이블이라 연관관계 없이 ID만 보관한다.
 */
@Entity
@Table(
    name = "user_missions",
    uniqueConstraints = @UniqueConstraint(columnNames = {"user_id", "mission_id", "mission_date"}),
    indexes = @Index(name = "idx_user_missions_user_date", columnList = "user_id, mission_date"))
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserMission extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @Column(nullable = false)
  private Long userId;

  @ManyToOne(fetch = FetchType.LAZY, optional = false)
  @JoinColumn(name = "mission_id", nullable = false)
  private Mission mission;

  @Column(nullable = false)
  private LocalDate missionDate;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 10)
  private UserMissionStatus status;

  private Instant completedAt;

  private Long conversationId;

  /** 그날 처음 미션을 조회할 때 진행 중(RUN)으로 시작한다. */
  public static UserMission start(Long userId, Mission mission, LocalDate missionDate) {
    UserMission userMission = new UserMission();
    userMission.userId = userId;
    userMission.mission = mission;
    userMission.missionDate = missionDate;
    userMission.status = UserMissionStatus.RUN;
    return userMission;
  }
}
