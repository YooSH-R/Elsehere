package com.elsehere.mission.repository;

import com.elsehere.mission.entity.ShowcaseJourney;
import jakarta.persistence.LockModeType;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;

public interface ShowcaseJourneyRepository extends JpaRepository<ShowcaseJourney, Long> {
  @Lock(LockModeType.PESSIMISTIC_WRITE)
  @Query("select journey from ShowcaseJourney journey where journey.userId = :userId")
  Optional<ShowcaseJourney> findForUpdate(Long userId);
}
