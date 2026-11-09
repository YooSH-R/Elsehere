package com.elsehere.memory.service;

import com.elsehere.common.config.ClockConfig;
import com.elsehere.conversation.entity.ConversationEndReason;
import com.elsehere.conversation.repository.ConversationRepository;
import com.elsehere.memory.dto.NeighborListResponse;
import com.elsehere.memory.dto.NeighborResponse;
import com.elsehere.memory.repository.UserNpcRelationRepository;
import com.elsehere.memory.repository.UserNpcRelationRepository.NpcWithRelation;
import java.util.Comparator;
import java.util.EnumSet;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import tools.jackson.databind.JsonNode;
import tools.jackson.databind.json.JsonMapper;
import tools.jackson.databind.node.MissingNode;

/**
 * 휴대폰 [이웃]의 NPC 목록(WORLD-08). 마을의 모든 NPC를 보여 주되, 끝까지 마친 대화가 있는 NPC만 이웃으로 열어 이름·인격 카드 정보·관계를 담는다.
 * 열리지 않은 NPC는 역할과 장소만 담는다.
 */
@Service
@RequiredArgsConstructor
public class NeighborService {

  /**
   * 끝까지 마친 대화로 보는 끝난 이유. [대화 종료]·NPC 마무리·30턴 도달이며, 무례 퇴장(RUDE)은 뺀다. 나가 버린 대화는 끝나지 않고 진행 중으로 남으므로
   * 들어오지 않는다.
   */
  private static final Set<ConversationEndReason> FINISHED_ENDINGS =
      EnumSet.of(
          ConversationEndReason.USER_ENDED,
          ConversationEndReason.NPC_ENDED,
          ConversationEndReason.TURN_LIMIT);

  // 이웃이 먼저 마지막 대화가 최근인 순서(시각이 없으면 뒤, 같으면 이름순), 잠긴 NPC는 장소·역할·NPC 키 순서다.
  private static final Comparator<NeighborResponse> ORDER =
      Comparator.comparing((NeighborResponse npc) -> !npc.unlocked())
          .thenComparing(
              NeighborResponse::lastTalkedAt, Comparator.nullsLast(Comparator.reverseOrder()))
          .thenComparing(NeighborResponse::name, Comparator.nullsLast(Comparator.naturalOrder()))
          .thenComparing(NeighborResponse::sceneName)
          .thenComparing(NeighborResponse::roleLabel)
          .thenComparing(NeighborResponse::npcCode);

  private static final JsonMapper JSON = JsonMapper.builder().build();

  private final UserNpcRelationRepository userNpcRelationRepository;
  private final ConversationRepository conversationRepository;

  @Transactional(readOnly = true)
  public NeighborListResponse neighbors(Long userId) {
    Set<Long> finished =
        new HashSet<>(conversationRepository.findNpcIdsEndedWith(userId, FINISHED_ENDINGS));
    List<NeighborResponse> npcs =
        userNpcRelationRepository.findNpcsWithRelation(userId).stream()
            .map(npc -> isNeighbor(npc, finished) ? neighbor(npc) : stranger(npc))
            .sorted(ORDER)
            .toList();
    int neighborCount = (int) npcs.stream().filter(NeighborResponse::unlocked).count();
    return new NeighborListResponse(neighborCount, npcs.size(), npcs);
  }

  // 튜토리얼 길 안내는 서버 대화 없이 방문만 센다(UserNpcRelation.recordTutorialVisit). 이 방문뿐인 관계도 끝까지 마친 대화로 본다.
  private static boolean isNeighbor(NpcWithRelation npc, Set<Long> finished) {
    if (finished.contains(npc.getNpcId())) {
      return true;
    }
    return npc.getTalkCount() != null
        && npc.getTalkCount() > 0
        && npc.getMemoryConversationId() == null;
  }

  private static NeighborResponse neighbor(NpcWithRelation npc) {
    JsonNode profile = profileOf(npc);
    return new NeighborResponse(
        npc.getNpcCode(),
        true,
        npc.getNpcName(),
        npc.getRoleLabel(),
        npc.getSceneCode(),
        npc.getSceneName(),
        profile.path("occupation").isString() ? profile.path("occupation").asString() : null,
        profile
            .path("background")
            .path("interests")
            .valueStream()
            .filter(JsonNode::isString)
            .map(JsonNode::asString)
            .toList(),
        npc.getAffinity(),
        npc.getRecognition(),
        npc.getTalkCount(),
        npc.getLastTalkedAt() == null
            ? null
            : npc.getLastTalkedAt().atZone(ClockConfig.SERVICE_ZONE).toOffsetDateTime());
  }

  private static NeighborResponse stranger(NpcWithRelation npc) {
    return new NeighborResponse(
        npc.getNpcCode(),
        false,
        null,
        npc.getRoleLabel(),
        npc.getSceneCode(),
        npc.getSceneName(),
        null,
        List.of(),
        null,
        null,
        null,
        null);
  }

  private static JsonNode profileOf(NpcWithRelation npc) {
    return npc.getProfile() == null ? MissingNode.getInstance() : JSON.readTree(npc.getProfile());
  }
}
