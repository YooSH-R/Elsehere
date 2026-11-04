package com.elsehere.memory.repository;

import com.elsehere.memory.entity.UserNpcRelation;
import com.elsehere.memory.entity.UserNpcRelationId;
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
}
