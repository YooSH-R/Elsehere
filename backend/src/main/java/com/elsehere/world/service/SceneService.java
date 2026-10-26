package com.elsehere.world.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.vocab.repository.UserVocabRepository;
import com.elsehere.world.dto.NpcResponse;
import com.elsehere.world.dto.ObjectCardResponse;
import com.elsehere.world.dto.ObjectVocabResponse;
import com.elsehere.world.dto.SceneDetailResponse;
import com.elsehere.world.dto.SceneSummaryResponse;
import com.elsehere.world.entity.Scene;
import com.elsehere.world.entity.WorldObject;
import com.elsehere.world.repository.NpcRepository;
import com.elsehere.world.repository.SceneRepository;
import com.elsehere.world.repository.UserObjectPrefRepository;
import com.elsehere.world.repository.WorldObjectRepository;
import java.util.List;
import java.util.Set;
import lombok.RequiredArgsConstructor;
import org.springframework.data.domain.Sort;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 장소 목록(WORLD-01)과 장소의 NPC·사물 카드(WORLD-02). */
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class SceneService {

  private final SceneRepository sceneRepository;
  private final WorldObjectRepository worldObjectRepository;
  private final NpcRepository npcRepository;
  private final UserVocabRepository userVocabRepository;
  private final UserObjectPrefRepository userObjectPrefRepository;

  /** 시드에 넣은 순서(집 → 마을 → 카페 → 호텔)로 돌려준다. */
  public List<SceneSummaryResponse> getScenes() {
    return sceneRepository.findAll(Sort.by("id")).stream().map(SceneSummaryResponse::from).toList();
  }

  /** 장소의 NPC와 사물 카드. 사물 카드의 저장·숨김 여부는 {@code userId} 사용자 기준이다. */
  public SceneDetailResponse getScene(String sceneCode, Long userId) {
    Scene scene =
        sceneRepository
            .findByCode(sceneCode)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    List<NpcResponse> npcs =
        npcRepository.findAllBySceneOrderByIdAsc(scene).stream().map(NpcResponse::from).toList();
    List<ObjectCardResponse> objects =
        toObjectCards(worldObjectRepository.findAllBySceneOrderByIdAsc(scene), userId);
    return new SceneDetailResponse(
        scene.getCode(), scene.getName(), scene.getUnityKey(), scene.isOpen(), npcs, objects);
  }

  /** 사물 카드의 저장·숨김 여부를 사물마다 조회하지 않고 장소 단위로 한 번씩 조회한다. */
  private List<ObjectCardResponse> toObjectCards(List<WorldObject> worldObjects, Long userId) {
    if (worldObjects.isEmpty()) {
      return List.of();
    }
    List<Long> vocabularyIds =
        worldObjects.stream().map(object -> object.getVocabulary().getId()).toList();
    List<Long> objectIds = worldObjects.stream().map(WorldObject::getId).toList();
    Set<Long> savedVocabularyIds =
        userVocabRepository.findSavedVocabularyIds(userId, vocabularyIds);
    Set<Long> hoverHiddenObjectIds =
        userObjectPrefRepository.findHoverHiddenObjectIds(userId, objectIds);

    return worldObjects.stream()
        .map(
            object ->
                new ObjectCardResponse(
                    object.getUnityKey(),
                    ObjectVocabResponse.from(object.getVocabulary()),
                    object.getVocabulary().getPronunciationAudioKey() != null,
                    savedVocabularyIds.contains(object.getVocabulary().getId()),
                    hoverHiddenObjectIds.contains(object.getId())))
        .toList();
  }
}
