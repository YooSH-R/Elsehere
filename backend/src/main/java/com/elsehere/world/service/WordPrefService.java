package com.elsehere.world.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.vocab.entity.Vocabulary;
import com.elsehere.vocab.repository.VocabularyRepository;
import com.elsehere.world.dto.WordPrefResponse;
import com.elsehere.world.entity.UserWordPref;
import com.elsehere.world.entity.UserWordPrefId;
import com.elsehere.world.repository.UserWordPrefRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 단어 카드 자동 표시 끄기·다시 켜기(WORLD-03). 설정은 단어 단위라 그 단어를 쓰는 모든 장소에 적용된다. */
@Service
@RequiredArgsConstructor
@Transactional
public class WordPrefService {

  private final VocabularyRepository vocabularyRepository;
  private final UserWordPrefRepository userWordPrefRepository;

  public WordPrefResponse changeHoverHidden(Long userId, String wordKey, boolean hoverHidden) {
    Vocabulary vocabulary =
        vocabularyRepository
            .findByWordKey(wordKey)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    // 설정이 이미 있으면 값만 바꾸고, 없으면 새로 만든다.
    userWordPrefRepository
        .findById(new UserWordPrefId(userId, vocabulary.getId()))
        .ifPresentOrElse(
            pref -> pref.changeHoverHidden(hoverHidden),
            () ->
                userWordPrefRepository.save(UserWordPref.create(userId, vocabulary, hoverHidden)));
    return new WordPrefResponse(wordKey, hoverHidden);
  }
}
