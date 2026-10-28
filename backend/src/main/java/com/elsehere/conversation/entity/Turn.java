package com.elsehere.conversation.entity;

import com.elsehere.common.entity.BaseEntity;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import java.time.Instant;
import java.util.UUID;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.NoArgsConstructor;
import org.hibernate.annotations.ColumnTransformer;

/**
 * 대화의 발화 한 턴. NPC 한 턴은 대답 한 줄(난이도에 따라 1~3문장)이며 문장으로 쪼개 저장하지 않는다.
 *
 * <p>무례 판정(guard_*)은 아직 부르지 않아 비워 둔다.
 */
@Entity
@Table(
    name = "turns",
    uniqueConstraints = @UniqueConstraint(columnNames = {"conversation_id", "seq"}))
@Getter
@NoArgsConstructor(access = AccessLevel.PROTECTED)
public class Turn extends BaseEntity {

  @Id
  @GeneratedValue(strategy = GenerationType.IDENTITY)
  private Long id;

  @ManyToOne(fetch = FetchType.LAZY, optional = false)
  @JoinColumn(name = "conversation_id", nullable = false)
  private Conversation conversation;

  @Column(nullable = false)
  private Integer seq;

  // 사용자 턴 전송 ID. 같은 ID로 다시 보내면 턴을 새로 만들지 않는다. NPC 턴은 null.
  @Column(unique = true)
  private UUID requestId;

  @Enumerated(EnumType.STRING)
  @Column(nullable = false, length = 10)
  private Speaker speaker;

  @Enumerated(EnumType.STRING)
  @Column(length = 10)
  private InputType inputType;

  @Column(columnDefinition = "text")
  private String sttRawText;

  @Column(nullable = false, columnDefinition = "text")
  private String text;

  @Column(columnDefinition = "text")
  private String translationEn;

  private Boolean guardAbusive;

  @Column(length = 30)
  private String guardErrorCode;

  @Column(columnDefinition = "text")
  private String meaningKo;

  @Column(columnDefinition = "jsonb")
  @ColumnTransformer(write = "?::jsonb")
  private String glossary;

  @Column(columnDefinition = "jsonb")
  @ColumnTransformer(write = "?::jsonb")
  private String suggestWords;

  @Column(columnDefinition = "text")
  private String hintText;

  private Instant hintOpenedAt;

  @Column(length = 255)
  private String audioPath;

  @Column(length = 100)
  private String npcAudioKey;

  // 보관 정책(기간 만료·용량 정리)으로 음성을 지운 시각. 처음 합성에 실패해 음성이 없는 턴과 구분해 다시 만들지 않는다.
  private Instant npcAudioDeletedAt;

  public static Turn user(
      Conversation conversation,
      int seq,
      UUID requestId,
      InputType inputType,
      String text,
      String sttRawText,
      String translationEn) {
    Turn turn = new Turn();
    turn.conversation = conversation;
    turn.seq = seq;
    turn.requestId = requestId;
    turn.speaker = Speaker.USER;
    turn.inputType = inputType;
    turn.text = text;
    turn.sttRawText = sttRawText;
    turn.translationEn = translationEn;
    return turn;
  }

  /**
   * @param suggestWords 추천 단어 JSON 배열. 만들지 않았으면 null
   * @param npcAudioKey 턴 전체 음성 파일 키. 합성에 실패했으면 null
   */
  public static Turn npc(
      Conversation conversation, int seq, String text, String suggestWords, String npcAudioKey) {
    Turn turn = new Turn();
    turn.conversation = conversation;
    turn.seq = seq;
    turn.speaker = Speaker.NPC;
    turn.text = text;
    turn.suggestWords = suggestWords;
    turn.npcAudioKey = npcAudioKey;
    return turn;
  }

  public void attachNpcAudio(String npcAudioKey) {
    this.npcAudioKey = npcAudioKey;
  }

  /** 보관 정책으로 음성 파일을 지웠다. 키를 비우고 지운 시각을 남긴다. */
  public void deleteNpcAudio(Instant deletedAt) {
    this.npcAudioKey = null;
    this.npcAudioDeletedAt = deletedAt;
  }

  /**
   * @param glossary 단어 풀이 JSON 배열
   */
  public void attachMeaning(String meaningKo, String glossary) {
    this.meaningKo = meaningKo;
    this.glossary = glossary;
  }

  /** 처음 연 힌트를 저장한다. 연 시각은 힌트 사용 기록이다. */
  public void openHint(String hintText, Instant openedAt) {
    this.hintText = hintText;
    this.hintOpenedAt = openedAt;
  }
}
