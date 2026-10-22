package com.elsehere.world.entity;

import com.elsehere.common.entity.BaseEntity;
import com.elsehere.vocab.entity.Vocabulary;
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

@Entity
@Table(name = "world_objects")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class WorldObject extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @ManyToOne(fetch = FetchType.LAZY, optional = false)
  @JoinColumn(name = "scene_id", nullable = false)
  private Scene scene;

  @Column(nullable = false, unique = true, length = 50)
  private String unityKey;

  @ManyToOne(fetch = FetchType.LAZY, optional = false)
  @JoinColumn(name = "vocab_id", nullable = false)
  private Vocabulary vocabulary;
}
