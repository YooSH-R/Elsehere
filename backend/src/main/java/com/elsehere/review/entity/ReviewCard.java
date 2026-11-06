package com.elsehere.review.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Index;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import java.time.Instant;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.ColumnDefault;
import org.hibernate.annotations.ColumnTransformer;

/** 원본 대화의 상황과 피드백 표현을 보관하는 사용자별 복습 카드. */
@Entity
@Table(
    name = "review_cards",
    uniqueConstraints =
        @UniqueConstraint(
            name = "uk_review_cards_user_source",
            columnNames = {"user_id", "source_conversation_id"}),
    indexes = @Index(name = "idx_review_cards_user_id", columnList = "user_id, id DESC"))
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class ReviewCard extends BaseEntity {
  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @Column(nullable = false)
  private Long userId;

  @Column(nullable = false)
  private Long sourceConversationId;

  @Column(nullable = false)
  private Long npcId;

  @Column(nullable = false, length = 30)
  private String sceneCode;

  @Column(nullable = false, columnDefinition = "text")
  private String situationSummary;

  @Column(nullable = false, columnDefinition = "jsonb")
  @ColumnTransformer(write = "?::jsonb")
  private String targets;

  @ColumnDefault("0")
  @Column(nullable = false)
  private int practiceCount;

  private Instant lastPracticedAt;

  @Column(nullable = false, columnDefinition = "jsonb")
  @ColumnTransformer(write = "?::jsonb")
  private String usedExpressions;

  public void recordPractice(Instant endedAt, String used) {
    practiceCount++;
    if (lastPracticedAt == null || !endedAt.isBefore(lastPracticedAt)) {
      lastPracticedAt = endedAt;
      usedExpressions = used;
    }
  }

  public static ReviewCard of(
      Long userId,
      Long sourceConversationId,
      Long npcId,
      String sceneCode,
      String situationSummary,
      String targets) {
    var card = new ReviewCard();
    card.userId = userId;
    card.sourceConversationId = sourceConversationId;
    card.npcId = npcId;
    card.sceneCode = sceneCode;
    card.situationSummary = situationSummary;
    card.targets = targets;
    card.usedExpressions = "[]";
    return card;
  }
}
