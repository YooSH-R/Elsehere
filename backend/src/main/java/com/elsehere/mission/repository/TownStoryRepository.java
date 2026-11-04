package com.elsehere.mission.repository;

import com.elsehere.mission.entity.TownStory;
import jakarta.persistence.LockModeType;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;

public interface TownStoryRepository extends JpaRepository<TownStory, Long> {
  @Lock(LockModeType.PESSIMISTIC_WRITE)
  @Query("select story from TownStory story where story.userId = :userId")
  Optional<TownStory> findForUpdate(Long userId);
}
