package com.elsehere.world.dto;

import com.elsehere.vocab.entity.Vocabulary;
import com.elsehere.vocab.entity.VocabularyType;

/** 사물 카드에 보여주는 어휘. 미리 등록한 어휘라 AI를 부르지 않는다. */
public record ObjectVocabResponse(
    Long vocabId,
    VocabularyType type,
    String text,
    String meaningKo,
    String example,
    String exampleKo) {

  public static ObjectVocabResponse from(Vocabulary vocabulary) {
    return new ObjectVocabResponse(
        vocabulary.getId(),
        vocabulary.getType(),
        vocabulary.getText(),
        vocabulary.getMeaningKo(),
        vocabulary.getExample(),
        vocabulary.getExampleKo());
  }
}
