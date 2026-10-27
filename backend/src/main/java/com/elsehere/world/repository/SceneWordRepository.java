package com.elsehere.world.repository;

import com.elsehere.world.entity.Scene;
import com.elsehere.world.entity.SceneWord;
import java.util.List;
import org.springframework.data.jpa.repository.EntityGraph;
import org.springframework.data.jpa.repository.JpaRepository;

public interface SceneWordRepository extends JpaRepository<SceneWord, Long> {

  /** 장소의 단어. 카드에 쓰는 어휘를 함께 읽어 단어마다 따로 조회하지 않는다. */
  @EntityGraph(attributePaths = "vocabulary")
  List<SceneWord> findAllBySceneOrderByIdAsc(Scene scene);
}
