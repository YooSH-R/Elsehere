package com.elsehere.ai.client;

import java.util.List;
import java.util.Map;

/**
 * 의도 판정(/v1/judge) 결과. 의도는 제안이며 상태를 바꿀지는 Spring이 정한다. 영어 번역·교정문과 다른 표현은 NPC 대답이 판정을 덜 기다리도록 영어
 * 표현(/v1/expressions, {@link ExpressionsResult})으로 옮겼다.
 *
 * @param text 행동 묘사({@code *...*})를 지운 말. 대답·기록에는 이것을 쓴다
 * @param dropped 행동 묘사만 있었으면 true. 턴으로 세지 않고 {@code notice}를 보여 준다
 * @param notice 학습자에게 보여 줄 한국어 안내
 * @param translation 쓰지 않는다. 지금 워커는 칸만 남겨 늘 null로 보내며, 번역·교정문은 {@link
 *     ExpressionsResult#translation()}에서 받는다. 여러 생성자의 자리를 지키려고 남겨 둔다
 * @param events 이 줄에서 학습자가 한 대화 사건(GREETING·APOLOGY 등). 의도 판정이 실패하면 빈 목록, 워커가 보내지 않으면 null
 * @param affinityDelta 이 줄의 사건으로 바로 올릴 호감도. 이번 대화에서 이미 센 사건은 빠진다. 무례로 센 턴에는 쓰지 않으며, 워커가 보내지 않으면(이
 *     칸을 모르는 이전 워커) null
 */
public record JudgeResult(
    String text,
    boolean dropped,
    String notice,
    ExpressionsResult.Translation translation,
    List<WorkerUsage> usage,
    Intent intent,
    List<String> events,
    Integer affinityDelta) {

  public JudgeResult(
      String text,
      boolean dropped,
      String notice,
      ExpressionsResult.Translation translation,
      List<WorkerUsage> usage) {
    this(text, dropped, notice, translation, usage, null, null, null);
  }

  public JudgeResult(
      String text,
      boolean dropped,
      String notice,
      ExpressionsResult.Translation translation,
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
}
