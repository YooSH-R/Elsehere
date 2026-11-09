package com.elsehere.memory.repository;

import com.elsehere.memory.entity.UserNpcRelation;
import com.elsehere.memory.entity.UserNpcRelationId;
import java.time.Instant;
import java.util.List;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;

public interface UserNpcRelationRepository
    extends JpaRepository<UserNpcRelation, UserNpcRelationId> {

  /**
   * 호감도에 변화량을 더하고 -100~100으로 자른다. 관계 행이 없으면(처음 만난 NPC) 만들면서 더한다. 읽고 쓰는 사이에 다른 변경이 끼지 않도록 SQL 한 번으로
   * 바꾼다.
   */
  @Modifying
  @Query(
      value =
          "insert into user_npc_relations (user_id, npc_id, affinity)"
              + " values (:userId, :npcId, greatest(-100, least(100, :delta)))"
              + " on conflict (npc_id, user_id) do update"
              + " set affinity = greatest(-100, least(100, user_npc_relations.affinity + :delta)),"
              + " updated_at = now()",
      nativeQuery = true)
  void addAffinity(Long userId, Long npcId, int delta);

  /** 호감도만 읽는다. 같은 트랜잭션에서 SQL로 바꾼 값도 보이도록 엔티티가 아니라 값을 조회한다. */
  @Query(
      "select relation.affinity from UserNpcRelation relation"
          + " where relation.id.userId = :userId and relation.id.npcId = :npcId")
  Optional<Integer> findAffinity(Long userId, Long npcId);

  /** 대화 뒤 관계 갱신의 알아봄 변화량을 더하고 0~100으로 자른다. 관계 행은 대화 종료 처리에서 이미 만들어져 있다. */
  @Modifying
  @Query(
      value =
          "update user_npc_relations"
              + " set recognition = greatest(0, least(100, recognition + :delta)),"
              + " updated_at = now()"
              + " where user_id = :userId and npc_id = :npcId",
      nativeQuery = true)
  void addRecognition(Long userId, Long npcId, int delta);

  /** 마을의 모든 NPC와 장소, 그리고 이 사용자와의 관계 값. 관계 행이 없는 NPC(처음 보는 NPC)는 관계 값이 null이다. */
  @Query(
      "select npc.id as npcId, npc.code as npcCode, npc.name as npcName,"
          + " npc.roleLabel as roleLabel, npc.profile as profile,"
          + " scene.code as sceneCode, scene.name as sceneName,"
          + " relation.affinity as affinity, relation.recognition as recognition,"
          + " relation.talkCount as talkCount, relation.lastTalkedAt as lastTalkedAt,"
          + " relation.memoryConversationId as memoryConversationId"
          + " from Npc npc join npc.scene scene"
          + " left join UserNpcRelation relation"
          + " on relation.id.npcId = npc.id and relation.id.userId = :userId")
  List<NpcWithRelation> findNpcsWithRelation(Long userId);

  /** NPC 하나와 이 사용자와의 관계. */
  interface NpcWithRelation {
    Long getNpcId();

    String getNpcCode();

    String getNpcName();

    String getRoleLabel();

    /** 인격 카드 JSON. 없을 수 있다. */
    String getProfile();

    String getSceneCode();

    String getSceneName();

    Integer getAffinity();

    Integer getRecognition();

    Integer getTalkCount();

    Instant getLastTalkedAt();

    /** 방문으로 센 마지막 서버 대화. 튜토리얼 길 안내처럼 서버 대화 없이 센 방문뿐이면 null이다. */
    Long getMemoryConversationId();
  }
}
