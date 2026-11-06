package com.elsehere.review.repository;

import com.elsehere.review.entity.ReviewCard;
import java.util.List;
import java.util.Optional;
import org.springframework.data.domain.Limit;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface ReviewCardRepository extends JpaRepository<ReviewCard, Long> {
  Optional<ReviewCard> findByUserIdAndSourceConversationId(Long userId, Long sourceConversationId);

  Optional<ReviewCard> findByIdAndUserId(Long id, Long userId);

  @Query(
      "select r from ReviewCard r where r.userId = :userId"
          + " and (:sceneCode is null or r.sceneCode = :sceneCode)"
          + " and (:cursor is null or r.id < :cursor) order by r.id desc")
  List<ReviewCard> findPage(
      @Param("userId") Long userId,
      @Param("sceneCode") String sceneCode,
      @Param("cursor") Long cursor,
      Limit limit);
}
