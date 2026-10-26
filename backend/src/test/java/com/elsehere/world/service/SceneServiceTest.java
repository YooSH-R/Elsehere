package com.elsehere.world.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.world.dto.SceneSummaryResponse;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

// 장소·NPC·사물 데이터는 시드(WorldSeedLoader가 시작 때 넣음)를 그대로 쓴다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({TestcontainersConfiguration.class, WorldSeedLoader.class, SceneService.class})
class SceneServiceTest {

  @Autowired private SceneService sceneService;

  @Test
  @DisplayName("장소 목록은 시드 순서대로 코드·이름·Unity 씬 이름·열림 여부를 돌려준다")
  void getScenes_returnsScenesInSeedOrder() {
    assertThat(sceneService.getScenes())
        .containsExactly(
            new SceneSummaryResponse("HOME", "집", "HomeScene", true),
            new SceneSummaryResponse("TOWN", "마을", "TownScene", true),
            new SceneSummaryResponse("CAFE", "모퉁이 카페", "CafeScene", true),
            new SceneSummaryResponse("HOTEL", "호텔", "HotelScene", false));
  }
}
