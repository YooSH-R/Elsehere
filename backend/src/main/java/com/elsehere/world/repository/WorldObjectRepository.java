package com.elsehere.world.repository;

import com.elsehere.world.entity.Scene;
import com.elsehere.world.entity.WorldObject;
import java.util.List;
import org.springframework.data.jpa.repository.EntityGraph;
import org.springframework.data.jpa.repository.JpaRepository;

public interface WorldObjectRepository extends JpaRepository<WorldObject, Long> {

  /** 장소의 사물을 넣은 순서대로. 사물 카드에 쓰는 어휘를 함께 읽어 사물마다 따로 조회하지 않는다. */
  @EntityGraph(attributePaths = "vocabulary")
  List<WorldObject> findAllBySceneOrderByIdAsc(Scene scene);
}
