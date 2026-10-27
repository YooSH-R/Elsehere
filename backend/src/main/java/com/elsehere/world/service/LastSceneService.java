package com.elsehere.world.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import com.elsehere.world.dto.LastSceneResponse;
import com.elsehere.world.entity.Scene;
import com.elsehere.world.repository.SceneRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 장소를 옮길 때 재접속할 장소를 저장한다(WORLD-05). */
@Service
@RequiredArgsConstructor
@Transactional
public class LastSceneService {

  private final UserRepository userRepository;
  private final SceneRepository sceneRepository;

  public LastSceneResponse saveLastScene(Long userId, String sceneCode) {
    User user =
        userRepository
            .findById(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    Scene scene =
        sceneRepository
            .findByCode(sceneCode)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    if (!scene.isOpen()) {
      throw new BusinessException(ErrorCode.SCENE_CLOSED);
    }
    user.moveTo(scene);
    return new LastSceneResponse(scene.getCode());
  }
}
