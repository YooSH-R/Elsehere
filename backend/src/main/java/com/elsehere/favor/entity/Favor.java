package com.elsehere.favor.entity;

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
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;

/**
 * 대화 중 돌발 부탁 한 건(docs/npc-life-design.md 7.13). 계기 틀·해결처는 규칙(content/npc-favors.json)의 id와 키로만 남기고,
 * 고를 때 정한 보상을 함께 저장한다. (사용자, 부탁한 NPC, 게임 날)마다 하나다.
 *
 * <p>사용자(users)와 대화(conversations)는 다른 담당 영역의 테이블이라 연관관계 없이 ID만 보관한다.
 */
@Entity
@Table(
    name = "npc_favors",
    uniqueConstraints =
        @UniqueConstraint(
            name = "uk_npc_favors_user_requester_day",
            columnNames = {"user_id", "requester_npc_code", "game_day"}),
    indexes = @Index(name = "idx_npc_favors_user_status", columnList = "user_id, status"))
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Favor extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @Column(nullable = false)
  private Long userId;

  @Column(nullable = false, length = 64)
  private String requesterNpcCode;

  // 부탁 카드에 쓰는 부탁한 NPC 이름
  @Column(nullable = false, length = 50)
  private String requesterName;

  // 계기 틀 id(ai/npcs/rules/requests.json)
  @Column(nullable = false, length = 64)
  private String templateId;

  // 심부름 해결처 장소 코드와 제공 키. 그 대화 안에서 끝나는 부탁이면 null
  @Column(length = 64)
  private String offerPlace;

  @Column(length = 32)
  private String offerKey;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 10)
  private FavorStatus status;

  // 부탁을 꺼낸 게임 날짜. 같은 NPC는 게임 하루에 한 번만 부탁한다.
  @Column(nullable = false)
  private int gameDay;

  // 심부름 기한(1일차 0시부터 센 게임 분). 수락하기 전과 대화 안 부탁은 null
  private Long expiresGameMinute;

  // 끝난(완료·거절·만료·그만두기) 게임 분. 쉬는 간격을 셀 때 쓴다.
  private Long endedGameMinute;

  @Column(nullable = false)
  private Long offeredConversationId;

  @Column(nullable = false)
  private int rewardCoins;

  @Column(nullable = false)
  private int affinityDelta;

  public static Favor offer(
      Long userId,
      String requesterNpcCode,
      String requesterName,
      String templateId,
      String offerPlace,
      String offerKey,
      int gameDay,
      Long conversationId,
      int rewardCoins,
      int affinityDelta) {
    Favor favor = new Favor();
    favor.userId = userId;
    favor.requesterNpcCode = requesterNpcCode;
    favor.requesterName = requesterName;
    favor.templateId = templateId;
    favor.offerPlace = offerPlace;
    favor.offerKey = offerKey;
    favor.status = FavorStatus.OFFERED;
    favor.gameDay = gameDay;
    favor.offeredConversationId = conversationId;
    favor.rewardCoins = rewardCoins;
    favor.affinityDelta = affinityDelta;
    return favor;
  }

  public boolean errand() {
    return offerPlace != null;
  }

  /** 상태를 바꾼다. 수락하면 기한을 정하고, 끝나면 끝난 시각을 남긴다. */
  public void moveTo(FavorStatus next, long gameMinute, int expiryMinutes) {
    if (next == FavorStatus.ACCEPTED) {
      expiresGameMinute = gameMinute + expiryMinutes;
    }
    if (!next.active()) {
      endedGameMinute = gameMinute;
    }
    status = next;
  }
}
