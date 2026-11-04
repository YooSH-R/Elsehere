package com.elsehere.world.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
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
import org.hibernate.annotations.ColumnDefault;
import org.hibernate.annotations.ColumnTransformer;

@Entity
@Table(name = "npcs")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Npc extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @ManyToOne(fetch = FetchType.LAZY, optional = false)
  @JoinColumn(name = "scene_id", nullable = false)
  private Scene scene;

  @Column(nullable = false, unique = true, length = 30)
  private String code;

  @Column(nullable = false, length = 30)
  private String name;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 20)
  private NpcType npcType;

  // 화면에 보여주는 역할 이름(행인, 카페 직원). persona는 AI 프롬프트용이라 따로 둔다.
  @Column(nullable = false, length = 30)
  private String roleLabel;

  // NpcPersona를 JSON 문자열로 jsonb 컬럼에 저장한다. 쓰기 때 jsonb로 변환하지 않으면 PostgreSQL이 타입 오류를 낸다.
  @Column(nullable = false, columnDefinition = "jsonb")
  @ColumnTransformer(write = "?::jsonb")
  private String persona;

  // 인격 카드(AI 공통 스키마의 npcProfile) JSON. 카드가 없는 NPC는 null이고, 워커는 persona의 역할 한 줄로 대답한다.
  @Column(columnDefinition = "jsonb")
  @ColumnTransformer(write = "?::jsonb")
  private String profile;

  @Column(nullable = false, length = 30)
  private String voiceId;

  @ColumnDefault("false")
  @Column(nullable = false)
  private boolean canInitiate;

  @Column(columnDefinition = "text")
  private String approachLine;

  @Column(length = 100)
  private String approachAudioKey;

  @Column(columnDefinition = "text")
  private String farewellLine;

  @Column(length = 100)
  private String farewellAudioKey;

  /** 페르소나를 바꾼다. 진행 중인 대화도 다음 턴부터 바뀐 값을 쓴다. */
  public void changePersona(String personaJson) {
    this.persona = personaJson;
  }
}
