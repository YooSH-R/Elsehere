package com.elsehere.vocab.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Index;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import java.time.Instant;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.ColumnDefault;

/**
 * 사용자 단어장 항목.
 *
 * <p>사용자(users)와 대화 턴(turns)은 다른 담당 영역의 테이블이라 연관관계 없이 ID만 보관한다.
 */
@Entity
@Table(
    name = "user_vocab",
    uniqueConstraints = @UniqueConstraint(columnNames = {"user_id", "vocab_id"}),
    indexes = @Index(name = "idx_user_vocab_user_saved_at", columnList = "user_id, saved_at DESC"))
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserVocab extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @Column(nullable = false)
  private Long userId;

  @ManyToOne(fetch = FetchType.LAZY, optional = false)
  @JoinColumn(name = "vocab_id", nullable = false)
  private Vocabulary vocabulary;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 20)
  private UserVocabSourceType sourceType;

  private Long sourceTurnId;

  @Column(columnDefinition = "text")
  private String memo;

  private Instant memorizedAt;

  // 단어장 목록의 정렬 기준인 업무 시각이다. 기록용 created_at과 별도로 저장하는 쪽에서 Clock 기준으로 넣는다.
  @ColumnDefault("now()")
  @Column(nullable = false, updatable = false)
  private Instant savedAt;
}
