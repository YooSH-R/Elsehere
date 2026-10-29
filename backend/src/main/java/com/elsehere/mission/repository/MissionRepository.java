package com.elsehere.mission.repository;

import com.elsehere.mission.entity.Mission;
import java.util.List;
import org.springframework.data.jpa.repository.EntityGraph;
import org.springframework.data.jpa.repository.JpaRepository;

public interface MissionRepository extends JpaRepository<Mission, Long> {

  /** 열린 장소의 미션. 목록에 대상 NPC와 장소를 함께 보여주므로 같이 조회한다. */
  @EntityGraph(attributePaths = {"npc", "npc.scene"})
  List<Mission> findAllByNpcSceneOpenTrueOrderByIdAsc();
}
