package com.elsehere.ai.client;

/** 워커 요청 형식의 글자 수 상한에 맞춰 글을 자른다. 상한을 넘기면 워커가 400으로 거절한다. */
public final class WorkerText {

  private WorkerText() {}

  /** {@code maxLength}자를 넘으면 그 안에서 자르고 끝에 "…"을 붙인다. null이거나 짧으면 그대로 둔다. */
  public static String clip(String text, int maxLength) {
    if (text == null || text.length() <= maxLength) {
      return text;
    }
    int end = maxLength - 1;
    // 이모지처럼 두 칸인 글자를 반으로 자르면 깨진 문자열이 되므로 그 글자 앞에서 자른다.
    if (Character.isHighSurrogate(text.charAt(end - 1))) {
      end--;
    }
    return text.substring(0, end).strip() + "…";
  }
}
