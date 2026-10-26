package com.elsehere.world.repository;

import com.elsehere.world.entity.Npc;
import com.elsehere.world.entity.Scene;
import java.util.List;
import org.springframework.data.jpa.repository.JpaRepository;

public interface NpcRepository extends JpaRepository<Npc, Long> {

  List<Npc> findAllBySceneOrderByIdAsc(Scene scene);
}
