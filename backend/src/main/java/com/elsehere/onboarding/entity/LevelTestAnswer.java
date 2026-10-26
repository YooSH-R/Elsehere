package com.elsehere.onboarding.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.ColumnDefault;

/** 레벨 테스트 문항 하나에 대한 응답. 문항은 {@code itemCode}로 서버 설정의 문항을 가리킨다. */
@Entity
@Table(name = "level_test_answers")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class LevelTestAnswer extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @ManyToOne(fetch = FetchType.LAZY, optional = false)
  @JoinColumn(name = "test_id")
  private LevelTest test;

  @Column(nullable = false, length = 50)
  private String itemCode;

  @Column(nullable = false, columnDefinition = "text")
  private String answer;

  @ColumnDefault("false")
  @Column(name = "is_correct", nullable = false)
  private boolean correct;

  public static LevelTestAnswer record(
      LevelTest test, String itemCode, String answer, boolean correct) {
    LevelTestAnswer levelTestAnswer = new LevelTestAnswer();
    levelTestAnswer.test = test;
    levelTestAnswer.itemCode = itemCode;
    levelTestAnswer.answer = answer;
    levelTestAnswer.correct = correct;
    return levelTestAnswer;
  }

  /** 같은 문항에 다시 답하면 이전 응답을 덮어쓴다. */
  public void change(String answer, boolean correct) {
    this.answer = answer;
    this.correct = correct;
  }
}
