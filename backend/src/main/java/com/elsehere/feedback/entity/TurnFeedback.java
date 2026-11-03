package com.elsehere.feedback.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/**
 * 사용자 발화 하나의 상세 평가(화면 17). 사용자 턴(turns)과 1:1이며 ID로 가리킨다.
 *
 * <p>ERD는 better_text를 NOT NULL로 두지만, 적절한 발화는 고칠 표현이 없어 null을 허용한다(ERD 조정 필요).
 */
@Entity
@Table(name = "turn_feedback")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class TurnFeedback extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @Column(nullable = false, unique = true)
  private Long turnId;

  @Column(columnDefinition = "text")
  private String betterText;

  @Column(nullable = false, columnDefinition = "text")
  private String reason;

  @Column(columnDefinition = "text")
  private String idiom;

  @Column(columnDefinition = "text")
  private String idiomMeaningKo;

  @Column(length = 100)
  private String modelAudioKey;

  public static TurnFeedback of(
      Long turnId, String betterText, String reason, String idiom, String idiomMeaningKo) {
    TurnFeedback feedback = new TurnFeedback();
    feedback.turnId = turnId;
    feedback.betterText = betterText;
    feedback.reason = reason;
    feedback.idiom = idiom;
    feedback.idiomMeaningKo = idiomMeaningKo;
    return feedback;
  }
}
