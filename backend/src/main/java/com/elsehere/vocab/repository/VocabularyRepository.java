package com.elsehere.vocab.repository;

import com.elsehere.vocab.entity.Vocabulary;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;

public interface VocabularyRepository extends JpaRepository<Vocabulary, Long> {

  /** Unity 공용 단어 키(content/object-words.json의 key)로 어휘를 찾는다. */
  Optional<Vocabulary> findByWordKey(String wordKey);
}
