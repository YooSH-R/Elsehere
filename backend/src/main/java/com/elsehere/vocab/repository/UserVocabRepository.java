package com.elsehere.vocab.repository;

import com.elsehere.vocab.entity.UserVocab;
import java.time.Instant;
import java.util.Collection;
import java.util.List;
import java.util.Optional;
import java.util.Set;
import org.springframework.data.domain.Limit;
import org.springframework.data.jpa.repository.EntityGraph;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface UserVocabRepository extends JpaRepository<UserVocab, Long> {

  Optional<UserVocab> findByUserIdAndVocabularyId(Long userId, Long vocabularyId);

  Optional<UserVocab> findByIdAndUserId(Long id, Long userId);

  /**
   * 단어장 첫 페이지(최근 저장순). 저장 시각이 같으면 나중에 저장한 항목(ID가 큰 쪽)이 먼저다. {@code includeMemorized}가 false면 외운 단어를
   * 뺀다.
   */
  @EntityGraph(attributePaths = "vocabulary")
  @Query(
      "select userVocab from UserVocab userVocab"
          + " where userVocab.userId = :userId"
          + " and (:includeMemorized = true or userVocab.memorizedAt is null)"
          + " order by userVocab.savedAt desc, userVocab.id desc")
  List<UserVocab> findFirstPage(
      @Param("userId") Long userId,
      @Param("includeMemorized") boolean includeMemorized,
      Limit limit);

  /** 커서 항목({@code savedAt}, {@code id}) 다음부터의 단어장 한 페이지. 정렬은 첫 페이지와 같다. */
  @EntityGraph(attributePaths = "vocabulary")
  @Query(
      "select userVocab from UserVocab userVocab"
          + " where userVocab.userId = :userId"
          + " and (:includeMemorized = true or userVocab.memorizedAt is null)"
          + " and (userVocab.savedAt < :savedAt"
          + " or (userVocab.savedAt = :savedAt and userVocab.id < :id))"
          + " order by userVocab.savedAt desc, userVocab.id desc")
  List<UserVocab> findPageAfter(
      @Param("userId") Long userId,
      @Param("includeMemorized") boolean includeMemorized,
      @Param("savedAt") Instant savedAt,
      @Param("id") Long id,
      Limit limit);

  long countByUserId(Long userId);

  long countByUserIdAndMemorizedAtIsNull(Long userId);

  /** 주어진 어휘 중 사용자가 단어장에 저장한 어휘의 ID. 사물 카드의 저장 여부를 한 번에 확인할 때 쓴다. */
  @Query(
      "select userVocab.vocabulary.id from UserVocab userVocab"
          + " where userVocab.userId = :userId and userVocab.vocabulary.id in :vocabularyIds")
  Set<Long> findSavedVocabularyIds(
      @Param("userId") Long userId, @Param("vocabularyIds") Collection<Long> vocabularyIds);
}
