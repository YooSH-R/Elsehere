package com.elsehere.world.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.world.dto.ObjectPrefResponse;
import com.elsehere.world.entity.UserObjectPref;
import com.elsehere.world.entity.UserObjectPrefId;
import com.elsehere.world.entity.WorldObject;
import com.elsehere.world.repository.UserObjectPrefRepository;
import com.elsehere.world.repository.WorldObjectRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 사물 카드 자동 표시 끄기·다시 켜기(WORLD-03). */
@Service
@RequiredArgsConstructor
@Transactional
public class ObjectPrefService {

  private final WorldObjectRepository worldObjectRepository;
  private final UserObjectPrefRepository userObjectPrefRepository;

  public ObjectPrefResponse changeHoverHidden(Long userId, String objectKey, boolean hoverHidden) {
    WorldObject worldObject =
        worldObjectRepository
            .findByUnityKey(objectKey)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    // 설정이 이미 있으면 값만 바꾸고, 없으면 새로 만든다.
    userObjectPrefRepository
        .findById(new UserObjectPrefId(userId, worldObject.getId()))
        .ifPresentOrElse(
            pref -> pref.changeHoverHidden(hoverHidden),
            () ->
                userObjectPrefRepository.save(
                    UserObjectPref.create(userId, worldObject, hoverHidden)));
    return new ObjectPrefResponse(objectKey, hoverHidden);
  }
}
