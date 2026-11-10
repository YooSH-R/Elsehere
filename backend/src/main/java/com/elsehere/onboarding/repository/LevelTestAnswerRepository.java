package com.elsehere.onboarding.repository;

import com.elsehere.onboarding.entity.LevelTest;
import com.elsehere.onboarding.entity.LevelTestAnswer;
import java.util.List;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;

public interface LevelTestAnswerRepository extends JpaRepository<LevelTestAnswer, Long> {

  Optional<LevelTestAnswer> findByTestAndItemCode(LevelTest test, String itemCode);

  List<LevelTestAnswer> findAllByTest(LevelTest test);
}
