package com.elsehere.memory.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.EmbeddedId;
import jakarta.persistence.Entity;
import jakarta.persistence.Table;
import java.time.Instant;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.ColumnDefault;
import org.hibernate.annotations.ColumnTransformer;
import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.type.SqlTypes;

/**
 * 사용자와 NPC 사이의 관계. 방문 수(talk_count)와 NPC 기억 카드를 둔다. 사용자(users)와 NPC(npcs)는 ID만 보관한다.
 *
 * <p>ERD에 없는 칸: 기억 카드(memory_card)와 마지막으로 반영한 대화(memory_conversation_id). 카드는 AI 워커 /v1/memory가 돌려준
 * 전체 카드이며 성공할 때마다 통째로 바꾼다. 같은 대화의 종료를 두 번 반영하지 않도록 마지막 대화 ID를 남긴다.
 */
@Entity
@Table(name = "user_npc_relations")
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserNpcRelation extends BaseEntity {

  @EmbeddedId private UserNpcRelationId id;

  // 호감도(0~5). 아직 바꾸는 기능이 없다.
  @ColumnDefault("0")
  @Column(nullable = false, columnDefinition = "smallint")
  @JdbcTypeCode(SqlTypes.SMALLINT)
  private Integer affinity;

  // 사용자 발화가 있었던 대화를 끝낸 횟수. NPC 대답 요청의 memory.visits로 쓴다.
  @ColumnDefault("0")
  @Column(nullable = false)
  private int talkCount;

  private Instant lastTalkedAt;

  @Column(columnDefinition = "jsonb")
  @ColumnTransformer(write = "?::jsonb")
  private String memoryCard;

  private Long memoryConversationId;

  public static UserNpcRelation start(Long userId, Long npcId) {
    UserNpcRelation relation = new UserNpcRelation();
    relation.id = new UserNpcRelationId(userId, npcId);
    relation.affinity = 0;
    return relation;
  }

  /**
   * 대화 하나가 끝난 것을 방문으로 센다.
   *
   * @return 처음 반영했으면 true. 이미 반영한 대화면 false
   */
  public boolean recordVisit(Long conversationId, Instant endedAt) {
    if (conversationId.equals(memoryConversationId)) {
      return false;
    }
    talkCount++;
    lastTalkedAt = endedAt;
    memoryConversationId = conversationId;
    return true;
  }

  /** AI가 새로 만든 카드로 통째로 바꾼다. 이전 카드와 합치지 않는다(철회한 내용이 되살아나지 않도록). */
  public void replaceMemoryCard(String memoryCard) {
    this.memoryCard = memoryCard;
  }
}
