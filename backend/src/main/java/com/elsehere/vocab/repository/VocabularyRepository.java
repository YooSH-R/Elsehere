package com.elsehere.vocab.repository;

import com.elsehere.vocab.entity.Vocabulary;
import java.util.List;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;

public interface VocabularyRepository extends JpaRepository<Vocabulary, Long> {

  /** Unity 공용 단어 키(content/object-words.json의 key)로 어휘를 찾는다. */
  Optional<Vocabulary> findByWordKey(String wordKey);

  /** 사물 단어(공용 단어 키가 있는 어휘) 전체. */
  List<Vocabulary> findAllByWordKeyIsNotNullOrderByIdAsc();

  /** 대화에서 저장하는 단어가 사전에 있는지. 문장 첫머리처럼 대소문자만 다른 단어도 같은 단어로 본다. */
  Optional<Vocabulary> findFirstByLanguageCodeAndTextIgnoreCaseOrderByIdAsc(
      String languageCode, String text);
}
