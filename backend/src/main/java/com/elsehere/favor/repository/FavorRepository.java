package com.elsehere.favor.repository;

import com.elsehere.favor.entity.Favor;
import com.elsehere.favor.entity.FavorStatus;
import java.util.Collection;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;

public interface FavorRepository extends JpaRepository<Favor, Long> {

  /** 학습자의 진행 중 부탁. 한 번에 하나만 둔다. */
  Optional<Favor> findFirstByUserIdAndStatusInOrderByIdDesc(
      Long userId, Collection<FavorStatus> statuses);

  Optional<Favor> findByIdAndUserId(Long id, Long userId);

  /** 그 NPC가 그 게임 날에 이미 부탁했는지. */
  boolean existsByUserIdAndRequesterNpcCodeAndGameDay(
      Long userId, String requesterNpcCode, int gameDay);

  /** 학습자가 그 게임 날 이미 겪은 계기인지(상황 계기는 하루 한 번). */
  boolean existsByUserIdAndTemplateIdAndGameDay(Long userId, String templateId, int gameDay);

  /** 가장 최근에 끝난 부탁. 쉬는 간격을 센다. */
  Optional<Favor> findFirstByUserIdAndEndedGameMinuteIsNotNullOrderByEndedGameMinuteDesc(
      Long userId);

  /** 완료한 부탁 보상 코인의 합. 코인 잔액 계산에 더한다. */
  @Query(
      "select coalesce(sum(f.rewardCoins), 0) from Favor f"
          + " where f.userId = :userId and f.status = com.elsehere.favor.entity.FavorStatus.DONE")
  long sumDoneRewardCoins(Long userId);
}
