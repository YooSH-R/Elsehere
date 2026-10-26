package com.elsehere.world.service;

import com.elsehere.world.dto.SceneSummaryResponse;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 장소 목록(WORLD-01)과 장소의 NPC·사물 카드(WORLD-02). */
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class SceneService {

  public List<SceneSummaryResponse> getScenes() {
    return List.of();
  }
}
