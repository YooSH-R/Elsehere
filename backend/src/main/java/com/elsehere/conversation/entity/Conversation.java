package com.elsehere.conversation.entity;

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
import java.time.Instant;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.ColumnDefault;
import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.type.SqlTypes;

/**
 * NPC 자유 대화 세션. 튜토리얼 대본 대화는 기록하지 않는다.
 *
 * <p>사용자(users)와 NPC(npcs)는 다른 담당 영역의 테이블이라 연관관계 없이 ID만 보관한다.
 */
@Entity
@Table(
    name = "conversations",
    indexes =
        @Index(
            name = "idx_conversations_user_npc_started",
            columnList = "user_id, npc_id, started_at DESC"))
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Conversation extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @Column(nullable = false)
  private Long userId;

  @Column(nullable = false)
  private Long npcId;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 10)
  private InitiatedBy initiatedBy;

  @JdbcTypeCode(SqlTypes.SMALLINT)
  @Column(nullable = false)
  private Integer levelApplied;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 20)
  private ConversationStatus status;

  @Enumerated(EnumType.STRING)
  @Column(length = 20)
  private ConversationEndReason endReason;

  @ColumnDefault("0")
  @JdbcTypeCode(SqlTypes.SMALLINT)
  @Column(nullable = false)
  private Integer rudeCount;

  @ColumnDefault("now()")
  @Column(nullable = false)
  private Instant startedAt;

  private Instant endedAt;

  public static Conversation start(
      Long userId, Long npcId, InitiatedBy initiatedBy, int level, Instant now) {
    Conversation conversation = new Conversation();
    conversation.userId = userId;
    conversation.npcId = npcId;
    conversation.initiatedBy = initiatedBy;
    conversation.levelApplied = level;
    conversation.status = ConversationStatus.ACTIVE;
    conversation.rudeCount = 0;
    conversation.startedAt = now;
    return conversation;
  }

  public boolean isActive() {
    return status == ConversationStatus.ACTIVE;
  }

  /** 사용자가 [대화 종료]를 눌렀다. NPC가 이미 끝낸 대화는 상태를 바꾸지 않는다. */
  public void endByUser(Instant now) {
    if (!isActive()) {
      return;
    }
    status = ConversationStatus.ENDED_BY_USER;
    endReason = ConversationEndReason.USER_ENDED;
    endedAt = now;
  }
}
