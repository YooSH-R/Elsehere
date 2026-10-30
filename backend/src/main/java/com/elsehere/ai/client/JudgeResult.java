package com.elsehere.ai.client;

import java.util.List;

/**
 * 의도·번역 판정(/v1/judge) 결과. 의도는 제안이며 상태를 바꿀지는 Spring이 정한다.
 *
 * @param text 행동 묘사({@code *...*})를 지운 말. 대답·기록에는 이것을 쓴다
 * @param dropped 행동 묘사만 있었으면 true. 턴으로 세지 않고 {@code notice}를 보여 준다
 * @param notice 학습자에게 보여 줄 한국어 안내
 * @param translation 학습자 말에 한글이 섞였을 때만 있는 영어 번역
 * @param events 이 줄에서 학습자가 한 대화 사건(GREETING·APOLOGY 등). 의도 판정이 실패하면 빈 목록, 워커가 보내지 않으면 null
 */
public record JudgeResult(
    String text,
    boolean dropped,
    String notice,
    Translation translation,
    List<WorkerUsage> usage,
    Intent intent,
    List<String> events) {

  public JudgeResult(
      String text,
      boolean dropped,
      String notice,
      Translation translation,
      List<WorkerUsage> usage) {
    this(text, dropped, notice, translation, usage, null, null);
  }

  public JudgeResult(
      String text,
      boolean dropped,
      String notice,
      Translation translation,
      List<WorkerUsage> usage,
      Intent intent) {
    this(text, dropped, notice, translation, usage, intent, null);
  }

  public record Intent(String id, List<String> ids, String error) {}

  /** 번역이 실패했으면 {@code english}가 null이고 {@code error}에 이유가 있다. */
  public record Translation(String english, String error) {}

  public String english() {
    return translation == null ? null : translation.english();
  }
}
