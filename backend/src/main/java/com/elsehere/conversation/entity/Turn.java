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
import org.hibernate.annotations.ColumnDefault;
import org.hibernate.annotations.ColumnTransformer;

/**
 * 대화의 발화 한 턴. NPC 한 턴은 대답 한 줄(난이도에 따라 1~3문장)이며 문장으로 쪼개 저장하지 않는다.
 *
 * <p>무례 판정(guard_*)은 사용자 턴에만 받은 그대로 남긴다. 판정하지 못했으면 guard_abusive가 null이고 guard_error_code에 이유가 있다.
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

  // 의도 판정이 고른 대화 사건(JSON 문자열 배열). 워커가 보내지 않았거나 NPC 턴이면 null이다.
  @Column(columnDefinition = "jsonb")
  @ColumnTransformer(write = "?::jsonb")
  private String events;

  // 출동 장소와 접수 여부를 함께 저장해 첫 응답이 유실되어도 같은 요청에 같은 출동 정보를 돌려준다.
  @Column(nullable = false)
  @ColumnDefault("false")
  private boolean policeDispatch;

  @Column(length = 40)
  private String policeDispatchPlaceCode;

  // 택시 기사가 이 턴에서 알아들은 목적지. 첫 응답이 유실되어도 같은 요청에 같은 목적지를 돌려준다.
  @Column(length = 40)
  private String taxiDestinationCode;

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

  // 구절·감정은 음성 성공 여부와 별개로 보존한다. 재합성한 PCM의 길이에 맞춰 시간을 다시 붙인다.
  @Column(columnDefinition = "jsonb")
  @ColumnTransformer(write = "?::jsonb")
  private String speechPhrases;

  public void attachSpeechPhrases(String phrases) {
    this.speechPhrases = phrases;
  }

  private Instant npcAudioCreatedAt;

  // 보관 정책으로 재생을 차단한 시각. 파일 삭제 실패 시에는 키를 남겨 다음 정리에서 재시도한다.
  private Instant npcAudioDeletedAt;

  public static Turn user(
      Conversation conversation,
      int seq,
      UUID requestId,
      InputType inputType,
      String text,
      String sttRawText,
      String translationEn,
      Boolean guardAbusive,
      String guardErrorCode,
      String events) {
    Turn turn = new Turn();
    turn.conversation = conversation;
    turn.seq = seq;
    turn.requestId = requestId;
    turn.speaker = Speaker.USER;
    turn.inputType = inputType;
    turn.text = text;
    turn.sttRawText = sttRawText;
    turn.translationEn = translationEn;
    turn.guardAbusive = guardAbusive;
    turn.guardErrorCode = guardErrorCode;
    turn.events = events;
    return turn;
  }

  /** 무례로 센 사용자 턴인지. 판정하지 못한 턴은 세지 않는다. */
  public boolean isRude() {
    return Boolean.TRUE.equals(guardAbusive);
  }

  /** 경찰 신고를 접수한 사용자 턴. 장소가 null이면 본길 순찰이다. */
  public void recordPoliceDispatch(String placeCode) {
    policeDispatch = true;
    policeDispatchPlaceCode = placeCode;
  }

  /** 택시 기사가 목적지를 알아들은 사용자 턴. */
  public void recordTaxiDestination(String placeCode) {
    taxiDestinationCode = placeCode;
  }

  /** 생성된 영어 번역·교정 표현은 무례 판정과 관계없이 보여 준다. */
  public String shownTranslation() {
    return translationEn;
  }

  /**
   * @param npcAudioKey 턴 전체 음성 파일 키. 합성에 실패했으면 null
   */
  public static Turn npc(
      Conversation conversation,
      int seq,
      String text,
      String npcAudioKey,
      Instant npcAudioCreatedAt) {
    Turn turn = new Turn();
    turn.conversation = conversation;
    turn.seq = seq;
    turn.speaker = Speaker.NPC;
    turn.text = text;
    turn.attachNpcAudio(npcAudioKey, npcAudioCreatedAt);
    return turn;
  }

  public void attachNpcAudio(String npcAudioKey, Instant createdAt) {
    this.npcAudioKey = npcAudioKey;
    this.npcAudioCreatedAt = npcAudioKey == null ? null : createdAt;
  }

  /** 보관 정책으로 음성 파일을 지웠다. 키를 비우고 지운 시각을 남긴다. */
  public void deleteNpcAudio(Instant deletedAt) {
    this.npcAudioKey = null;
    markNpcAudioUnavailable(deletedAt);
  }

  /** 파일을 지우지 못해도 재생·재생성을 먼저 차단하고, 삭제를 재시도할 키는 유지한다. */
  public void markNpcAudioUnavailable(Instant deletedAt) {
    if (this.npcAudioDeletedAt == null) {
      this.npcAudioDeletedAt = deletedAt;
    }
  }

  /**
   * @param glossary 단어 풀이 JSON 배열
   */
  public void attachMeaning(String meaningKo, String glossary) {
    this.meaningKo = meaningKo;
    this.glossary = glossary;
  }

  public void attachSuggestWords(String suggestWords) {
    this.suggestWords = suggestWords;
  }

  /** 처음 연 힌트를 저장한다. 연 시각은 힌트 사용 기록이다. */
  public void openHint(String hintText, Instant openedAt) {
    this.hintText = hintText;
    this.hintOpenedAt = openedAt;
  }
}
