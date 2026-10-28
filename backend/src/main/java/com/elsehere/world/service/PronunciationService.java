package com.elsehere.world.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.vocab.repository.VocabularyRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 사물 단어 발음 듣기(WORLD-04). 미리 만든 발음 파일을 돌려주며 AI를 호출하지 않는다. */
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class PronunciationService {

  private final VocabularyRepository vocabularyRepository;
  private final PronunciationAudioStore pronunciationAudioStore;

  public byte[] getPronunciation(Long vocabId) {
    String key =
        vocabularyRepository
            .findById(vocabId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND))
            .getPronunciationAudioKey();
    return pronunciationAudioStore
        .load(key)
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }
}
