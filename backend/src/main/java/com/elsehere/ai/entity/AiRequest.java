package com.elsehere.ai.entity;

import com.elsehere.ai.client.WorkerUsage;
import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Index;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import java.time.Instant;
import java.util.List;
import java.util.Objects;
import java.util.UUID;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.ColumnDefault;

/**
 * AI 호출 기록. 호출 한 번이 한 행이며, 워커가 돌려준 사용량을 비용 근거로 남긴다.
 *
 * <p>사용자 한도 예약·차감(reserved·charged)은 아직 하지 않아 0으로 둔다. 사용자(users)와 대화(conversations)는 ID만 보관한다.
 */
@Entity
@Table(
    name = "ai_requests",
    uniqueConstraints = @UniqueConstraint(columnNames = {"request_id", "kind"}),
    indexes =
        @Index(name = "idx_ai_requests_user_created", columnList = "user_id, created_at DESC"))
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class AiRequest extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @Column(nullable = false)
  private UUID requestId;

  @Column(nullable = false)
  private Long userId;

  private Long conversationId;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 20)
  private AiRequestKind kind;

  @Column(length = 50)
  private String model;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 20)
  private AiRequestStatus status;

  @ColumnDefault("0")
  @Column(nullable = false)
  private int inputTokens;

  @ColumnDefault("0")
  @Column(nullable = false)
  private int outputTokens;

  private Integer audioMs;

  @ColumnDefault("0")
  @Column(nullable = false)
  private int reservedTokens;

  @ColumnDefault("0")
  @Column(nullable = false)
  private int chargedTokens;

  private Instant settledAt;

  public static AiRequest start(
      UUID requestId, Long userId, Long conversationId, AiRequestKind kind) {
    AiRequest request = new AiRequest();
    request.requestId = requestId;
    request.userId = userId;
    request.conversationId = conversationId;
    request.kind = kind;
    request.status = AiRequestStatus.OK;
    return request;
  }

  /** 워커가 돌려준 사용량으로 채운다. 같은 요청을 다시 보내 워커를 또 불렀으면 마지막 호출의 사용량으로 바꾼다. */
  public void settle(List<WorkerUsage> usage, AiRequestStatus status, Instant now) {
    this.status = status;
    this.model =
        usage.stream().map(WorkerUsage::model).filter(Objects::nonNull).findFirst().orElse(null);
    this.inputTokens = usage.stream().mapToInt(item -> zeroIfNull(item.inputTokens())).sum();
    this.outputTokens = usage.stream().mapToInt(item -> zeroIfNull(item.outputTokens())).sum();
    double audioSeconds =
        usage.stream().map(WorkerUsage::audioS).filter(Objects::nonNull).mapToDouble(s -> s).sum();
    this.audioMs = audioSeconds > 0 ? (int) Math.round(audioSeconds * 1000) : null;
    this.settledAt = now;
  }

  private static int zeroIfNull(Integer value) {
    return value == null ? 0 : value;
  }
}
