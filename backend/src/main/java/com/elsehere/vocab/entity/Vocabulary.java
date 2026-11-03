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

  // Unity 공용 단어 키(예: air_conditioner). 사물 단어만 값이 있으며 시드가 object-words.json에서 채운다.
  @Column(unique = true, length = 50)
  private String wordKey;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 20)
  private VocabularySource source;

  /**
   * 대화에서 처음 저장하는 단어·숙어·문장. 사전(시드)에 없던 말이라 예문과 단어 키가 없다. 문장 부호로 끝나면 문장(SENTENCE), 두 단어 이상이면
   * 숙어(IDIOM)로 둔다.
   */
  public static Vocabulary newWord(
      String languageCode, String text, String meaningKo, VocabularySource source) {
    Vocabulary vocabulary = new Vocabulary();
    vocabulary.languageCode = languageCode;
    vocabulary.type = typeOf(text);
    vocabulary.text = text;
    vocabulary.meaningKo = meaningKo;
    vocabulary.source = source;
    return vocabulary;
  }

  // 문장 부호로 끝나는 말은 문장, 두 단어 이상은 숙어, 나머지는 단어다.
  private static VocabularyType typeOf(String text) {
    String trimmed = text.strip();
    if (trimmed.matches(".*\\s.*[.?!]$")) {
      return VocabularyType.SENTENCE;
    }
    return trimmed.chars().anyMatch(Character::isWhitespace)
        ? VocabularyType.IDIOM
        : VocabularyType.WORD;
  }

  /** 만든 발음 파일을 연결한다(WORLD-04). */
  public void attachPronunciation(String pronunciationAudioKey) {
    this.pronunciationAudioKey = pronunciationAudioKey;
  }
}
