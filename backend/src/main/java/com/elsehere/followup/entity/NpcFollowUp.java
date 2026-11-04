package com.elsehere.followup.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Index;
import jakarta.persistence.Table;
import java.time.Duration;
import java.time.Instant;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/**
 * NPC가 대화에서 곧 건네기로 한 것(음료·음식·빨래 등). 준비 시각이 지나면 그 NPC가 학습자를 다시 부르고, 다음 대화의 첫마디로 건넨다.
 *
 * <p>사용자·대화는 다른 담당 영역의 테이블이라 연관관계 없이 ID만 보관한다. 대화 하나에서 하나만 만든다.
 */
@Entity
@Table(
    name = "npc_follow_ups",
    indexes = @Index(name = "idx_npc_follow_ups_user_npc", columnList = "user_id, npc_id"))
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class NpcFollowUp extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @Column(nullable = false)
  private Long userId;

  @Column(nullable = false)
  private Long npcId;

  /** 건넬 것을 약속한 대화 */
  @Column(nullable = false, unique = true)
  private Long conversationId;

  @Column(nullable = false, length = 40)
  private String item;

  @Column(nullable = false, length = 20)
  private String itemKo;

  @Column(nullable = false)
  private Instant readyAt;

  private Instant deliveredAt;

  /** 건넨 대화. 같은 요청 ID로 대화 시작을 다시 보내면 이것으로 건넨 것을 다시 알린다 */
  private Long deliveredConversationId;

  /** 끝난 대화에서 NPC가 곧 건네기로 한 것. 기다림 정도만큼 지나면 준비된다. */
  public static NpcFollowUp promise(
      Long userId,
      Long npcId,
      Long conversationId,
      String item,
      String itemKo,
      FollowUpWait wait,
      Instant now) {
    NpcFollowUp followUp = new NpcFollowUp();
    followUp.userId = userId;
    followUp.npcId = npcId;
    followUp.conversationId = conversationId;
    followUp.item = item;
    followUp.itemKo = itemKo;
    followUp.readyAt = now.plusSeconds(wait.seconds());
    return followUp;
  }

  /** 준비까지 남은 초. 1초가 안 남아도 1로 세고, 준비됐으면 0이다. */
  public long remainingSeconds(Instant now) {
    long millis = Duration.between(now, readyAt).toMillis();
    return millis <= 0 ? 0 : (millis + 999) / 1000;
  }

  public boolean isReady(Instant now) {
    return !now.isBefore(readyAt);
  }

  public boolean isDelivered() {
    return deliveredAt != null;
  }

  public void deliver(Long conversationId, Instant now) {
    deliveredConversationId = conversationId;
    deliveredAt = now;
  }
}
