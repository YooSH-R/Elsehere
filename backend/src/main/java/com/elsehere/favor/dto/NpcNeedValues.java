package com.elsehere.favor.dto;

import java.util.Collections;
import java.util.Map;
import java.util.Set;
import java.util.TreeMap;

/**
 * Unity가 보내는 NPC 생활 욕구 값(대화 시작 CONV-01, NPC 말 걸기 FAV-03). 키는 공통 규칙의
 * 욕구(ai/npcs/rules/world-rules.json)이고 값은 0~100이다.
 */
public final class NpcNeedValues {

  /** 공통 규칙의 욕구 키 */
  public static final Set<String> KEYS = Set.of("HUNGER", "ENERGY", "SOCIAL", "FUN", "WORK");

  private NpcNeedValues() {}

  /** 모르는 키와 빈 값은 버리고 0~100으로 자른다. 보내지 않았으면(null) null이다. */
  public static Map<String, Double> known(Map<String, Double> needs) {
    if (needs == null) {
      return null;
    }
    Map<String, Double> known = new TreeMap<>();
    needs.forEach(
        (key, value) -> {
          if (KEYS.contains(key) && value != null) {
            known.put(key, Math.clamp(value, 0.0, 100.0));
          }
        });
    return Collections.unmodifiableMap(known);
  }
}
