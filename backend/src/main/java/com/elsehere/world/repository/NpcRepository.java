package com.elsehere.world.repository;

import com.elsehere.world.entity.Npc;
import com.elsehere.world.entity.Scene;
import java.util.List;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;

public interface NpcRepository extends JpaRepository<Npc, Long> {

  List<Npc> findAllBySceneOrderByIdAsc(Scene scene);

  Optional<Npc> findByCode(String code);

  /** 모든 사용자가 함께 쓰는 말걸기 음성 키. NPC 음성 보관 정리에서 지우지 않는다. */
  @Query("select n.approachAudioKey from Npc n where n.approachAudioKey is not null")
  List<String> findAllApproachAudioKeys();
}
