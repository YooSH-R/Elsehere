package com.elsehere.onboarding.repository;

import com.elsehere.onboarding.entity.LevelTest;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;

public interface LevelTestRepository extends JpaRepository<LevelTest, Long> {

  Optional<LevelTest> findByIdAndUserId(Long id, Long userId);
}
