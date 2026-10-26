package com.elsehere.mission.entity;

import com.elsehere.common.entity.BaseEntity;
import com.elsehere.world.entity.Npc;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.ColumnTransformer;

/** 일일 미션 정의. 튜토리얼 미션은 Unity 대본 콘텐츠라 여기에 두지 않는다. */
@Entity
@Table(name = "missions")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Mission extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  // 시드와 Unity에서 미션을 가리키는 고정 키 (예: CAFE_SOY_LATTE)
  @Column(nullable = false, unique = true, length = 50)
  private String code;

  @ManyToOne(fetch = FetchType.LAZY, optional = false)
  @JoinColumn(name = "npc_id", nullable = false)
  private Npc npc;

  @Column(nullable = false, length = 100)
  private String title;

  @Column(nullable = false, columnDefinition = "text")
  private String description;

  // JSON 문자열을 jsonb 컬럼에 저장한다. 쓰기 때 jsonb로 변환하지 않으면 PostgreSQL이 타입 오류를 낸다.
  @Column(nullable = false, columnDefinition = "jsonb")
  @ColumnTransformer(write = "?::jsonb")
  private String keywords;

  @Column(nullable = false, columnDefinition = "text")
  private String intentRule;
}
