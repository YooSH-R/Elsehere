package com.elsehere.world.repository;

import com.elsehere.world.entity.Scene;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;

public interface SceneRepository extends JpaRepository<Scene, Long> {

  Optional<Scene> findByCode(String code);
}
