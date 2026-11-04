package com.elsehere.vocab.entity;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class VocabularyTest {

  @Test
  @DisplayName("대화에서 처음 저장하는 한 단어는 단어(WORD)로 만든다")
  void newWord_singleWord_isWord() {
    Vocabulary vocabulary = Vocabulary.newWord("en-US", "latte", "라떼", VocabularySource.NPC);

    assertThat(vocabulary.getType()).isEqualTo(VocabularyType.WORD);
  }

  @Test
  @DisplayName("두 단어 이상인 표현은 숙어(IDIOM)로 만든다")
  void newWord_multiWordExpression_isIdiom() {
    Vocabulary vocabulary = Vocabulary.newWord("en-US", "to go", "포장해 가는", VocabularySource.NPC);

    assertThat(vocabulary.getType()).isEqualTo(VocabularyType.IDIOM);
    assertThat(vocabulary.getText()).isEqualTo("to go");
  }

  @Test
  @DisplayName("문장 부호로 끝나는 여러 단어 표현은 문장(SENTENCE)으로 만든다")
  void newWord_sentence() {
    Vocabulary vocabulary =
        Vocabulary.newWord("en-US", "I'll be right back.", "금방 올게요.", VocabularySource.FEEDBACK);

    assertThat(vocabulary.getType()).isEqualTo(VocabularyType.SENTENCE);
  }
}
