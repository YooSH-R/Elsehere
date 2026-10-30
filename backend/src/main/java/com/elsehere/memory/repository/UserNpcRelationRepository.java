package com.elsehere.memory.repository;

import com.elsehere.memory.entity.UserNpcRelation;
import com.elsehere.memory.entity.UserNpcRelationId;
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
}
