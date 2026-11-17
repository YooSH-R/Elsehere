package com.elsehere.mission.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.EmbeddedId;
import jakarta.persistence.Entity;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/**
 * 시연이 덮어쓰는 NPC 관계와 게임 시각의 보관. 시연을 벗어나면 이 값으로 되돌리고 지운다.
 *
 * <p>테이블 구조를 정하는 매핑이다. 관계 행을 통째로 옮기므로 읽고 쓰기는 {@code ShowcaseScene}이 SQL로 한다.
 */
@Entity
@Table(name = "showcase_saved_states")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class ShowcaseSavedState extends BaseEntity {

  @EmbeddedId private ShowcaseSavedStateId id;

  // 보관한 게임 시계의 누적 플레이 초. 시계가 아직 없던 계정은 null이다.
  private Long playSeconds;

  // 대본이 덮어쓰는 네 NPC와의 user_npc_relations 행 전체 배열. 없던 관계는 들어 있지 않다.
  @Column(nullable = false, columnDefinition = "jsonb")
  private String relations;
}
