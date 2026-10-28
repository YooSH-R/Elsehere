package com.elsehere.onboarding.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import java.time.Instant;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.ColumnDefault;
import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.type.SqlTypes;

/** 레벨 테스트 한 번. 점수와 결과 레벨은 완료(DONE)했을 때만 있다. */
@Entity
@Table(name = "level_tests")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class LevelTest extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @Column(nullable = false)
  private Long userId;

  @Column(nullable = false, length = 10)
  private String languageCode;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 20)
  private LevelTestStatus status;

  @Column(columnDefinition = "smallint")
  @JdbcTypeCode(SqlTypes.SMALLINT)
  private Integer score;

  @Column(columnDefinition = "smallint")
  @JdbcTypeCode(SqlTypes.SMALLINT)
  private Integer resultLevel;

  @ColumnDefault("now()")
  @Column(nullable = false)
  private Instant startedAt;

  private Instant completedAt;

  public static LevelTest start(Long userId, String languageCode, Instant now) {
    LevelTest test = new LevelTest();
    test.userId = userId;
    test.languageCode = languageCode;
    test.status = LevelTestStatus.IN_PROGRESS;
    test.startedAt = now;
    return test;
  }

  /** 문항을 풀지 않고 넘어간 기록. 시작과 동시에 끝난다. */
  public static LevelTest skip(Long userId, String languageCode, Instant now) {
    LevelTest test = new LevelTest();
    test.userId = userId;
    test.languageCode = languageCode;
    test.status = LevelTestStatus.SKIPPED;
    test.startedAt = now;
    test.completedAt = now;
    return test;
  }

  public void quit(Instant now) {
    this.status = LevelTestStatus.QUIT;
    this.completedAt = now;
  }

  public void complete(int score, int resultLevel, Instant now) {
    this.status = LevelTestStatus.DONE;
    this.score = score;
    this.resultLevel = resultLevel;
    this.completedAt = now;
  }
}
