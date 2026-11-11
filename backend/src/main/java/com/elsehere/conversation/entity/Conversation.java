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
import java.util.List;
import java.util.UUID;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.ColumnDefault;
import org.hibernate.annotations.ColumnTransformer;
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
    indexes = {
      @Index(
          name = "idx_conversations_user_npc_started",
          columnList = "user_id, npc_id, started_at DESC"),
      @Index(name = "idx_conversations_review_card_status", columnList = "review_card_id, status")
    })
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Conversation extends BaseEntity {

  /** 대화 하나에서 무례로 셀 수 있는 발화 수(서버 설정값). 여기에 닿으면 NPC가 대화를 끝낸다. */
  public static final int RUDE_LIMIT = 3;

  public static final int TURN_LIMIT = 30;

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

  private Integer firstDayStage;

  // 같은 단계에 다시 말을 걸어도 이전 대화에서 끝낸 목표를 다시 요구하지 않는다.
  @JdbcTypeCode(SqlTypes.JSON)
  @Column(columnDefinition = "jsonb")
  private List<String> firstDayGoalIds;

  private Integer townStoryStage;

  // 같은 식당 단계 안에서도 주문과 식사 후 사건은 별개 대화다. null은 이 정보를 저장하기 전의 기록이다.
  @JdbcTypeCode(SqlTypes.JSON)
  @Column(columnDefinition = "jsonb")
  private List<String> townStoryGoalIds;

  private Long reviewCardId;

  @Column(columnDefinition = "jsonb")
  @ColumnTransformer(write = "?::jsonb")
  private String reviewResult;

  public void recordReviewResult(String result) {
    if (reviewResult == null) {
      reviewResult = result;
    }
  }

  public boolean isReview() {
    return reviewCardId != null;
  }

  public void attachReviewCard(Long cardId) {
    this.reviewCardId = cardId;
  }

  // 장소 연습(PRC-02)으로 시작한 대화의 상황 ID(mission의 PracticeScenario 이름). 장소 연습이 아니면 null이다.
  @Column(length = 40)
  private String practiceScenario;

  // 이 장소 연습 대화에서 이룬 목표 ID. 다시 하면 새 대화라 빈 목록부터 시작한다.
  @JdbcTypeCode(SqlTypes.JSON)
  @Column(columnDefinition = "jsonb")
  private List<String> practiceCompletedGoals;

  // 이 대화에서 상황의 목표를 모두 이룬 시각. 끝내지 못한 연습과 장소 연습이 아닌 대화는 null이다.
  private Instant practiceDoneAt;

  public boolean isPractice() {
    return practiceScenario != null;
  }

  public void attachPracticeScenario(String scenarioId) {
    this.practiceScenario = scenarioId;
    this.practiceCompletedGoals = List.of();
  }

  public void recordPracticeGoals(List<String> goalIds) {
    practiceCompletedGoals = List.copyOf(goalIds);
  }

  /** 상황의 목표를 모두 이뤘다. 처음 이룬 시각만 남긴다. */
  public void finishPractice(Instant now) {
    if (practiceDoneAt == null) {
      practiceDoneAt = now;
    }
  }

  // 대화를 시작한 요청 ID(CONV-01). 같은 ID로 다시 보내면 대화를 새로 만들지 않는다. 이 칸을 넣기 전의 대화는 null이다.
  @Column(unique = true)
  private UUID requestId;

  // 마지막 발화를 선점한 요청. DB 호환을 위해 기존 컬럼 이름을 유지한다.
  @Column private UUID timeLimitFinalRequestId;

  // 근무 시간이 아닌 NPC와의 대화(CONV-01 npcOffDuty). 워커에 응대 상황 대신 일상 상황을 보낸다. 이 칸을 넣기 전의 대화는 false다.
  @ColumnDefault("false")
  @Column(nullable = false)
  private boolean npcOffDuty;

  // 대화를 시작할 때 Unity가 알린 NPC의 이동 사실과 지금 있는 곳(CONV-01 npcTravelContext·placeCode). 대화 동안 워커에 덧붙인다.
  @Column(length = 1600)
  private String npcTravelSituation;

  public void recordNpcTravelSituation(String situation) {
    if (npcTravelSituation == null) {
      npcTravelSituation = situation;
    }
  }

  public void attachFirstDayStage(Integer stage) {
    this.firstDayStage = stage;
  }

  public void recordFirstDayGoals(List<String> goalIds) {
    if (firstDayGoalIds == null) {
      firstDayGoalIds = List.copyOf(goalIds);
    }
  }

  public void attachTownStoryStage(Integer stage) {
    this.townStoryStage = stage;
  }

  public void recordTownStoryGoals(List<String> goalIds) {
    if (townStoryGoalIds == null) {
      townStoryGoalIds = List.copyOf(goalIds);
    }
  }

  public void attachStartRequest(UUID requestId) {
    this.requestId = requestId;
  }

  public void markNpcOffDuty() {
    this.npcOffDuty = true;
  }

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

  public boolean claimFinalTurn(UUID requestId) {
    if (timeLimitFinalRequestId == null) {
      timeLimitFinalRequestId = requestId;
      return true;
    }
    return timeLimitFinalRequestId.equals(requestId);
  }

  public void releaseFinalTurn(UUID requestId) {
    if (isActive() && requestId.equals(timeLimitFinalRequestId)) {
      timeLimitFinalRequestId = null;
    }
  }

  /** 30턴에 닿아 서버가 대화를 끝낸다. 이미 끝난 대화는 상태를 바꾸지 않는다. */
  public void endByTurnLimit(Instant now) {
    if (!isActive()) {
      return;
    }
    status = ConversationStatus.ENDED_BY_NPC;
    endReason = ConversationEndReason.TURN_LIMIT;
    endedAt = now;
  }

  /** 무례한 말을 하나 센다. 한도에 닿으면 NPC가 대화를 끝낸다. */
  public void countRude(int limit, Instant now) {
    rudeCount++;
    if (rudeCount >= limit) {
      status = ConversationStatus.ENDED_BY_NPC;
      endReason = ConversationEndReason.RUDE;
      endedAt = now;
    }
  }

  /** 용건이 끝났거나 학습자가 작별해 NPC가 마무리 대사로 대화를 끝냈다. 이미 끝난 대화는 상태를 바꾸지 않는다. */
  public void endByNpc(Instant now) {
    if (!isActive()) {
      return;
    }
    status = ConversationStatus.ENDED_BY_NPC;
    endReason = ConversationEndReason.NPC_ENDED;
    endedAt = now;
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
