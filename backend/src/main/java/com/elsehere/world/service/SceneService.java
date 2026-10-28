package com.elsehere.world.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.vocab.entity.Vocabulary;
import com.elsehere.vocab.repository.UserVocabRepository;
import com.elsehere.world.dto.NpcResponse;
import com.elsehere.world.dto.ObjectVocabResponse;
import com.elsehere.world.dto.SceneDetailResponse;
import com.elsehere.world.dto.SceneSummaryResponse;
import com.elsehere.world.dto.WordCardResponse;
import com.elsehere.world.entity.Scene;
import com.elsehere.world.entity.SceneWord;
import com.elsehere.world.repository.NpcRepository;
import com.elsehere.world.repository.SceneRepository;
import com.elsehere.world.repository.SceneWordRepository;
import com.elsehere.world.repository.UserWordPrefRepository;
import java.util.List;
import java.util.Set;
import lombok.RequiredArgsConstructor;
import org.springframework.data.domain.Sort;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 장소 목록(WORLD-01)과 장소의 NPC·단어 카드(WORLD-02). */
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class SceneService {

  private final SceneRepository sceneRepository;
  private final SceneWordRepository sceneWordRepository;
  private final NpcRepository npcRepository;
  private final UserVocabRepository userVocabRepository;
  private final UserWordPrefRepository userWordPrefRepository;

  /** 시드에 넣은 순서(집 → 마을 → 카페 → 호텔)로 돌려준다. */
  public List<SceneSummaryResponse> getScenes() {
    return sceneRepository.findAll(Sort.by("id")).stream().map(SceneSummaryResponse::from).toList();
  }

  /** 장소의 NPC와 단어 카드. 단어 카드의 저장·숨김 여부는 {@code userId} 사용자 기준이다. */
  public SceneDetailResponse getScene(String sceneCode, Long userId) {
    Scene scene =
        sceneRepository
            .findByCode(sceneCode)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    List<NpcResponse> npcs =
        npcRepository.findAllBySceneOrderByIdAsc(scene).stream().map(NpcResponse::from).toList();
    List<Vocabulary> words =
        sceneWordRepository.findAllBySceneOrderByIdAsc(scene).stream()
            .map(SceneWord::getVocabulary)
            .toList();
    return new SceneDetailResponse(
        scene.getCode(),
        scene.getName(),
        scene.getUnityKey(),
        scene.isOpen(),
        npcs,
        toWordCards(words, userId));
  }

  /** 단어 카드의 저장·숨김 여부를 단어마다 조회하지 않고 장소 단위로 한 번씩 조회한다. */
  private List<WordCardResponse> toWordCards(List<Vocabulary> words, Long userId) {
    if (words.isEmpty()) {
      return List.of();
    }
    List<Long> vocabularyIds = words.stream().map(Vocabulary::getId).toList();
    Set<Long> savedVocabularyIds =
        userVocabRepository.findSavedVocabularyIds(userId, vocabularyIds);
    Set<Long> hoverHiddenVocabularyIds =
        userWordPrefRepository.findHoverHiddenVocabularyIds(userId, vocabularyIds);

    return words.stream()
        .map(
            word ->
                new WordCardResponse(
                    word.getWordKey(),
                    ObjectVocabResponse.from(word),
                    word.getPronunciationAudioKey() != null,
                    savedVocabularyIds.contains(word.getId()),
                    hoverHiddenVocabularyIds.contains(word.getId())))
        .toList();
  }
}
