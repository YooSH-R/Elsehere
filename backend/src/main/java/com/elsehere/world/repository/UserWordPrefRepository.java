package com.elsehere.world.repository;

import com.elsehere.world.entity.UserWordPref;
import com.elsehere.world.entity.UserWordPrefId;
import java.util.Collection;
import java.util.Set;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface UserWordPrefRepository extends JpaRepository<UserWordPref, UserWordPrefId> {

  /** 주어진 어휘 중 사용자가 카드 자동 표시를 끈 어휘의 ID. 설정이 없는 단어는 자동 표시가 켜진 것으로 본다. */
  @Query(
      "select pref.id.vocabId from UserWordPref pref"
          + " where pref.id.userId = :userId and pref.hoverHidden = true"
          + " and pref.id.vocabId in :vocabularyIds")
  Set<Long> findHoverHiddenVocabularyIds(
      @Param("userId") Long userId, @Param("vocabularyIds") Collection<Long> vocabularyIds);
}
