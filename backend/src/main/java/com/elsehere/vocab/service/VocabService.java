package com.elsehere.vocab.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.vocab.dto.VocabItemResponse;
import com.elsehere.vocab.dto.VocabListResponse;
import com.elsehere.vocab.dto.VocabSaveResponse;
import com.elsehere.vocab.entity.UserVocab;
import com.elsehere.vocab.entity.Vocabulary;
import com.elsehere.vocab.repository.UserVocabRepository;
import com.elsehere.world.entity.WorldObject;
import com.elsehere.world.repository.WorldObjectRepository;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneId;
import java.util.List;
import java.util.Optional;
import lombok.RequiredArgsConstructor;
import org.springframework.data.domain.Limit;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 단어장(VOCAB-01~04). 저장할 때 AI나 외부 번역 API를 호출하지 않는다. */
@Service
@RequiredArgsConstructor
@Transactional
public class VocabService {

  /** 저장 시각을 보여주는 시간대. MVP는 한국 시간으로 고정한다. */
  private static final ZoneId DISPLAY_ZONE = ZoneId.of("Asia/Seoul");

  private final WorldObjectRepository worldObjectRepository;
  private final UserVocabRepository userVocabRepository;
  private final Clock clock;

  /** 단어장 목록(VOCAB-01). 최근 저장순이다. */
  @Transactional(readOnly = true)
  public VocabListResponse getVocab(
      Long userId, boolean includeMemorized, String cursor, int size) {
    // 다음 페이지가 있는지 알기 위해 한 개를 더 가져온다.
    Limit limit = Limit.of(size + 1);
    List<UserVocab> fetched =
        cursor == null
            ? userVocabRepository.findFirstPage(userId, includeMemorized, limit)
            : findPageAfterCursor(userId, includeMemorized, cursor, limit);
    boolean hasNext = fetched.size() > size;
    List<UserVocab> page = hasNext ? fetched.subList(0, size) : fetched;
    String nextCursor = hasNext ? String.valueOf(page.getLast().getId()) : null;

    // 외운 단어도 보기를 끄면 목록과 개수 모두에서 외운 단어를 뺀다.
    long count =
        includeMemorized
            ? userVocabRepository.countByUserId(userId)
            : userVocabRepository.countByUserIdAndMemorizedAtIsNull(userId);
    return new VocabListResponse(
        new VocabListResponse.Counts(count),
        page.stream().map(userVocab -> VocabItemResponse.from(userVocab, DISPLAY_ZONE)).toList(),
        nextCursor);
  }

  /** 커서는 이전 페이지 마지막 항목의 ID다. 그 항목보다 먼저 저장한 항목부터 가져온다. */
  private List<UserVocab> findPageAfterCursor(
      Long userId, boolean includeMemorized, String cursor, Limit limit) {
    UserVocab last = findCursorEntry(userId, cursor);
    return userVocabRepository.findPageAfter(
        userId, includeMemorized, last.getSavedAt(), last.getId(), limit);
  }

  /** 숫자가 아니거나, 없거나, 다른 사용자의 항목을 가리키는 커서는 잘못된 요청이다. */
  private UserVocab findCursorEntry(Long userId, String cursor) {
    long userVocabId;
    try {
      userVocabId = Long.parseLong(cursor);
    } catch (NumberFormatException exception) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED, exception);
    }
    return userVocabRepository
        .findByIdAndUserId(userVocabId, userId)
        .orElseThrow(() -> new BusinessException(ErrorCode.VALIDATION_FAILED));
  }

  /** 메모·외웠어요 표시를 바꾼다(VOCAB-03). null인 값은 보내지 않은 것으로 보고 그대로 둔다. */
  public VocabItemResponse updateVocab(
      Long userId, Long userVocabId, String memo, Boolean memorized) {
    UserVocab userVocab = findOwnEntry(userId, userVocabId);
    if (memo != null) {
      userVocab.changeMemo(memo);
    }
    if (memorized != null) {
      userVocab.changeMemorized(memorized, Instant.now(clock));
    }
    return VocabItemResponse.from(userVocab, DISPLAY_ZONE);
  }

  /** 내 단어장에서 지운다(VOCAB-04). 공용 어휘(vocabularies)는 다른 사용자도 쓰므로 남긴다. */
  public void deleteVocab(Long userId, Long userVocabId) {
    userVocabRepository.delete(findOwnEntry(userId, userVocabId));
  }

  /** 내 단어장 항목. 다른 사용자의 항목은 존재 여부를 드러내지 않도록 없는 것과 같이 NOT_FOUND로 응답한다. */
  private UserVocab findOwnEntry(Long userId, Long userVocabId) {
    return userVocabRepository
        .findByIdAndUserId(userVocabId, userId)
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }

  /** 사물 카드의 단어를 저장한다(VOCAB-02 OBJECT). 문장은 사물 카드 예문이다. */
  public VocabSaveResponse saveObjectWord(Long userId, String objectKey) {
    WorldObject worldObject =
        worldObjectRepository
            .findByUnityKey(objectKey)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    Vocabulary vocabulary = worldObject.getVocabulary();
    // 이미 저장한 단어면 처음 저장한 항목(문장 포함)을 그대로 돌려준다.
    Optional<UserVocab> existing =
        userVocabRepository.findByUserIdAndVocabularyId(userId, vocabulary.getId());
    if (existing.isPresent()) {
      return new VocabSaveResponse(VocabItemResponse.from(existing.get(), DISPLAY_ZONE), false);
    }
    UserVocab saved =
        userVocabRepository.save(UserVocab.saveFromObject(userId, vocabulary, Instant.now(clock)));
    return new VocabSaveResponse(VocabItemResponse.from(saved, DISPLAY_ZONE), true);
  }
}
