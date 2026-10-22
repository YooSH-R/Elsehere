package com.elsehere.vocab.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

@Entity
@Table(
    name = "vocabularies",
    uniqueConstraints = @UniqueConstraint(columnNames = {"language_code", "text"}))
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Vocabulary extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @Column(nullable = false, length = 10)
  private String languageCode;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 10)
  private VocabularyType type;

  @Column(nullable = false, columnDefinition = "text")
  private String text;

  @Column(nullable = false, columnDefinition = "text")
  private String meaningKo;

  @Column(columnDefinition = "text")
  private String example;

  @Column(columnDefinition = "text")
  private String exampleKo;

  @Column(length = 100)
  private String pronunciationAudioKey;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 20)
  private VocabularySource source;
}
