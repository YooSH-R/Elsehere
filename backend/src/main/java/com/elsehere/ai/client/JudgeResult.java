package com.elsehere.ai.client;

import java.util.List;
import java.util.Map;

/**
 * 의도·번역·영어 교정 판정(/v1/judge) 결과. 의도는 제안이며 상태를 바꿀지는 Spring이 정한다.
 *
 * @param text 행동 묘사({@code *...*})를 지운 말. 대답·기록에는 이것을 쓴다
 * @param dropped 행동 묘사만 있었으면 true. 턴으로 세지 않고 {@code notice}를 보여 준다
 * @param notice 학습자에게 보여 줄 한국어 안내
 * @param translation 학습자 말의 영어 번역 또는 오류가 있을 때의 영어 교정문
 * @param events 이 줄에서 학습자가 한 대화 사건(GREETING·APOLOGY 등). 의도 판정이 실패하면 빈 목록, 워커가 보내지 않으면 null
 * @param affinityDelta 이 줄의 사건으로 바로 올릴 호감도. 이번 대화에서 이미 센 사건은 빠진다. 무례로 센 턴에는 쓰지 않으며, 워커가 보내지 않으면(이
 *     칸을 모르는 이전 워커) null
 * @param betterExpressions 틀리진 않았지만 어색하거나 짧은 영어에 보여 줄 같은 뜻의 더 좋은 문장(최대 3개). 없으면 빈 목록
 */
public record JudgeResult(
    String text,
    boolean dropped,
    String notice,
    Translation translation,
    List<WorkerUsage> usage,
    Intent intent,
    List<String> events,
    Integer affinityDelta,
    List<String> betterExpressions) {

  public JudgeResult {
    betterExpressions = betterExpressions == null ? List.of() : List.copyOf(betterExpressions);
  }

  public JudgeResult(
      String text,
      boolean dropped,
      String notice,
      Translation translation,
      List<WorkerUsage> usage,
      Intent intent,
      List<String> events,
      Integer affinityDelta) {
    this(text, dropped, notice, translation, usage, intent, events, affinityDelta, List.of());
  }

  public JudgeResult(
      String text,
      boolean dropped,
      String notice,
      Translation translation,
      List<WorkerUsage> usage) {
    this(text, dropped, notice, translation, usage, null, null, null);
  }

  public JudgeResult(
      String text,
      boolean dropped,
      String notice,
      Translation translation,
      List<WorkerUsage> usage,
      Intent intent) {
    this(text, dropped, notice, translation, usage, intent, null, null);
  }

  /**
   * @param slots 상태가 함께 고르게 한 값(예: 경찰 창구의 {@code place}). 없으면 빈 맵
   */
  public record Intent(String id, List<String> ids, Map<String, Object> slots, String error) {

    public Intent {
      slots = slots == null ? Map.of() : slots;
    }

    public Intent(String id, List<String> ids, String error) {
      this(id, ids, Map.of(), error);
    }
  }

  /** 번역이 실패했으면 {@code english}가 null이고 {@code error}에 이유가 있다. */
  public record Translation(String english, String error) {}

  public String english() {
    return translation == null ? null : translation.english();
  }
}
