package com.elsehere.feedback.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.ColumnTransformer;

/**
 * 대화 종료 뒤의 짧은 피드백. 대화 하나에 하나이며, 대화(conversations)는 ID로 가리킨다.
 *
 * <p>ERD에 없는 칸: 실패 원인 코드(error_code)와 워커가 돌려준 검증된 결과 원본(result). 원본은 상세 평가를 AI 재호출 없이 다시 보여 주고 평가
 * 근거를 잃지 않으려고 보관한다.
 */
@Entity
@Table(name = "conversation_feedback")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class ConversationFeedback extends BaseEntity {

  @Id private Long conversationId;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 20)
  private FeedbackStatus status;

  @Column(columnDefinition = "text")
  private String summary;

  // { good: [...], improve: [...], suggestion: { userText, betterText } | null }
  @Column(columnDefinition = "jsonb")
  @ColumnTransformer(write = "?::jsonb")
  private String reviews;

  @Column(length = 50)
  private String model;

  @Column(length = 40)
  private String errorCode;

  @Column(columnDefinition = "jsonb")
  @ColumnTransformer(write = "?::jsonb")
  private String result;

  public static ConversationFeedback of(Long conversationId) {
    ConversationFeedback feedback = new ConversationFeedback();
    feedback.conversationId = conversationId;
    return feedback;
  }

  public void complete(String summary, String reviews, String model, String result) {
    this.status = FeedbackStatus.DONE;
    this.summary = summary;
    this.reviews = reviews;
    this.model = model;
    this.errorCode = null;
    this.result = result;
  }

  public void fail(String errorCode, String model) {
    this.status = FeedbackStatus.FAILED;
    this.errorCode = errorCode;
    this.model = model;
  }

  /**
   * @param reason 건너뛴 이유. 사용자가 건너뛰었으면 null
   */
  public void skip(String reason) {
    this.status = FeedbackStatus.SKIPPED;
    this.errorCode = reason;
  }
}
