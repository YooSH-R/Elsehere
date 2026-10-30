package com.elsehere.ai.service;

import com.elsehere.ai.client.WorkerNpc;
import com.elsehere.world.entity.Npc;
import com.elsehere.world.entity.NpcPersona;
import java.util.Map;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;
import tools.jackson.core.type.TypeReference;
import tools.jackson.databind.json.JsonMapper;

/** NPC를 워커 요청 형식으로 바꾼다. 대화·기억처럼 워커를 부르는 기능이 함께 쓴다. */
@Component
@RequiredArgsConstructor
public class WorkerNpcs {

  private static final TypeReference<Map<String, Object>> PROFILE = new TypeReference<>() {};

  private final JsonMapper jsonMapper;

  // persona는 NpcPersona JSON이다(시드 world.sql, 어드민). NPC 식별자는 콘텐츠 코드(npcs.code)를 쓴다.
  // 인격 카드는 Map으로 읽어 보내므로 워커 클라이언트의 snake_case 변환을 받지 않고 칸 이름이 그대로 간다.
  public WorkerNpc of(Npc npc) {
    NpcPersona persona = jsonMapper.readValue(npc.getPersona(), NpcPersona.class);
    return new WorkerNpc(
        npc.getCode(),
        npc.getName(),
        persona.role(),
        persona.facts(),
        persona.scene(),
        npc.getVoiceId(),
        1.0,
        npc.getProfile() == null ? null : jsonMapper.readValue(npc.getProfile(), PROFILE));
  }
}
