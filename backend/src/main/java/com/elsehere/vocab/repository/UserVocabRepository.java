package com.elsehere.vocab.repository;

import com.elsehere.vocab.entity.UserVocab;
import java.util.Collection;
import java.util.Set;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface UserVocabRepository extends JpaRepository<UserVocab, Long> {

  /** 주어진 어휘 중 사용자가 단어장에 저장한 어휘의 ID. 사물 카드의 저장 여부를 한 번에 확인할 때 쓴다. */
  @Query(
      "select userVocab.vocabulary.id from UserVocab userVocab"
          + " where userVocab.userId = :userId and userVocab.vocabulary.id in :vocabularyIds")
  Set<Long> findSavedVocabularyIds(
      @Param("userId") Long userId, @Param("vocabularyIds") Collection<Long> vocabularyIds);
}
