package com.elsehere.world.entity;

import com.elsehere.common.entity.BaseEntity;
import com.elsehere.vocab.entity.Vocabulary;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/** 장소의 사물에 붙은 공용 단어. 여러 사물이 같은 단어를 쓰면 한 행이며, Unity가 내보낸 object-word-bindings.json으로 시드한다. */
@Entity
@Table(
    name = "scene_words",
    uniqueConstraints = @UniqueConstraint(columnNames = {"scene_id", "vocab_id"}))
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class SceneWord extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @ManyToOne(fetch = FetchType.LAZY, optional = false)
  @JoinColumn(name = "scene_id", nullable = false)
  private Scene scene;

  @ManyToOne(fetch = FetchType.LAZY, optional = false)
  @JoinColumn(name = "vocab_id", nullable = false)
  private Vocabulary vocabulary;
}
