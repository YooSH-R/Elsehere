package com.elsehere.world.entity;

/**
 * NPC 페르소나. {@code npcs.persona}(jsonb)에 저장하는 형식이며, 대화할 때 AI 워커 요청의 npc.role·facts·scene으로 그대로
 * 보낸다(docs/api/ai-worker.md). 어드민(/admin/npcs)에서 고친다.
 *
 * @param role NPC가 누구인지 한 문장. 워커가 이름 뒤에 이어 붙인다(예: Mia, the barista at the corner cafe.)
 * @param facts NPC가 알고 있는 사실·지켜야 할 규칙. 없으면 빈 문자열이다
 * @param scene NPC가 있는 장소 설명. 없으면 빈 문자열이다
 */
public record NpcPersona(String role, String facts, String scene) {

  public NpcPersona {
    facts = facts == null ? "" : facts;
    scene = scene == null ? "" : scene;
  }
}
