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
import org.hibernate.annotations.DynamicUpdate;
import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.type.SqlTypes;

/**
 * 사용자와 NPC 사이의 관계. 방문 수(talk_count)와 NPC 기억 카드를 둔다. 사용자(users)와 NPC(npcs)는 ID만 보관한다.
 *
 * <p>ERD에 없는 칸: 기억 카드(memory_card)와 마지막으로 반영한 대화(memory_conversation_id). 카드는 AI 워커 /v1/memory가 돌려준
 * 전체 카드이며 성공할 때마다 통째로 바꾼다. 같은 대화의 종료를 두 번 반영하지 않도록 마지막 대화 ID를 남긴다.
 *
 * <p>호감도는 발화 저장과 같은 트랜잭션에서 SQL로 바로 바꾸므로({@code UserNpcRelationRepository.addAffinity}), 엔티티를 저장할 때는
 * 바꾼 칸만 써서 그 사이 바뀐 호감도를 덮어쓰지 않는다.
 */
@Entity
@Table(name = "user_npc_relations")
@DynamicUpdate
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class UserNpcRelation extends BaseEntity {

  @EmbeddedId private UserNpcRelationId id;

  // 호감도(-100~100). 대화 중 발화마다 바꾸고(NpcAffinityService), 대화가 끝나면 좋은 대화 몫을 더한다(MemoryService).
  @ColumnDefault("0")
  @JdbcTypeCode(SqlTypes.SMALLINT)
  @Column(nullable = false)
  private Integer affinity;

  // 알아봄(0~100). 얼굴을 아는 정도이며 대화가 끝나면 관계 갱신 변화량을 더한다.
  @ColumnDefault("0")
  @JdbcTypeCode(SqlTypes.SMALLINT)
  @Column(nullable = false)
  private Integer recognition;

  // 사용자 발화가 있었던 대화를 끝낸 횟수. NPC 대답 요청의 memory.visits로 쓴다.
  @ColumnDefault("0")
  @Column(nullable = false)
  private int talkCount;

  private Instant lastTalkedAt;

  // 마지막 대화를 끝낸 게임 날짜(사용자별 게임 시계). 다음 요청의 days_since_last_talk를 센다. 게임 시계 도입 전 대화는 null이다.
  private Integer lastTalkGameDay;

  @Column(columnDefinition = "jsonb")
  @ColumnTransformer(write = "?::jsonb")
  private String memoryCard;

  private Long memoryConversationId;

  public static UserNpcRelation start(Long userId, Long npcId) {
    UserNpcRelation relation = new UserNpcRelation();
    relation.id = new UserNpcRelationId(userId, npcId);
    relation.affinity = 0;
    relation.recognition = 0;
    return relation;
  }

  /**
   * 대화 하나가 끝난 것을 방문으로 센다.
   *
   * @param gameDay 대화를 끝낸 게임 날짜(사용자별 게임 시계)
   * @return 처음 반영했으면 true. 이미 반영한 대화면 false
   */
  public boolean recordVisit(Long conversationId, Instant endedAt, int gameDay) {
    if (conversationId.equals(memoryConversationId)) {
      return false;
    }
    talkCount++;
    lastTalkedAt = endedAt;
    lastTalkGameDay = gameDay;
    memoryConversationId = conversationId;
    return true;
  }

  /** 서버 대화를 만들지 않는 첫 길 안내도 방문으로 센다. 이미 대화 기록이 있으면 다시 올리지 않는다. */
  public boolean recordTutorialVisit(
      Instant endedAt, int gameDay, int recognitionGain, int affinityGain, String card) {
    if (talkCount > 0) {
      return false;
    }
    talkCount = 1;
    lastTalkedAt = endedAt;
    lastTalkGameDay = gameDay;
    recognition = Math.max(0, Math.min(100, recognition + recognitionGain));
    affinity = Math.max(-100, Math.min(100, affinity + affinityGain));
    memoryCard = card;
    return true;
  }

  /** 마지막 대화 뒤 지난 게임 날 수. 게임 시계 도입 전 대화만 있으면 0이다. */
  public int daysSinceLastTalk(int today) {
    return lastTalkGameDay == null ? 0 : Math.max(0, today - lastTalkGameDay);
  }

  /** AI가 새로 만든 카드로 통째로 바꾼다. 이전 카드와 합치지 않는다(철회한 내용이 되살아나지 않도록). */
  public void replaceMemoryCard(String memoryCard) {
    this.memoryCard = memoryCard;
  }
}
