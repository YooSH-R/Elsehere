package com.elsehere.ai.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.common.seed.WorldSeedLoader;
import com.elsehere.world.repository.NpcRepository;
import com.elsehere.world.service.PronunciationAudioStore;
import java.util.Map;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.autoconfigure.ImportAutoConfiguration;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jackson.autoconfigure.JacksonAutoConfiguration;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

// 시드가 넣은 NPC(카페 직원은 인격 카드 있음, 편의점 직원은 없음)로 워커 요청의 NPC를 만든다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({
  TestcontainersConfiguration.class,
  WorldSeedLoader.class,
  PronunciationAudioStore.class,
  WorkerNpcs.class
})
@ImportAutoConfiguration(JacksonAutoConfiguration.class)
class WorkerNpcsTest {

  @Autowired private WorkerNpcs workerNpcs;

  @Autowired private NpcRepository npcRepository;

  @Test
  @DisplayName("인격 카드가 있는 NPC는 워커 요청의 NPC에 카드를 칸 이름 그대로 싣는다")
  void of_npcWithProfile_includesProfile() {
    WorkerNpc npc = workerNpcs.of(npcRepository.findByCode("CAFE_STAFF").orElseThrow());

    assertThat(npc.profile())
        .isInstanceOfSatisfying(
            Map.class,
            profile ->
                assertThat(profile)
                    .containsEntry("schemaVersion", "2.0")
                    .containsEntry("name", "Mia")
                    .containsKeys("personality", "speechStyle", "behaviorRules"));
  }

  @Test
  @DisplayName("인격 카드가 없는 NPC는 카드 없이 보내 워커가 역할 한 줄로 대답하게 한다")
  void of_npcWithoutProfile_sendsNoProfile() {
    WorkerNpc npc = workerNpcs.of(npcRepository.findByCode("SHOP_STAFF").orElseThrow());

    assertThat(npc.profile()).isNull();
    assertThat(npc.role()).isEqualTo("the helpful clerk at the convenience store.");
  }
}
