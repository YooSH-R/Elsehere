package com.elsehere.conversation.service;

import com.elsehere.conversation.entity.Speaker;
import com.elsehere.conversation.entity.Turn;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import lombok.extern.slf4j.Slf4j;
import tools.jackson.databind.json.JsonMapper;

/** 대화 중 실제로 보여 주고 저장한 추천. 다시 읽을 때 모델을 부르거나 새로운 문장을 만들지 않는다. */
@Slf4j
public final class TurnRecommendations {
  private TurnRecommendations() {}

  public static List<String> sentences(Turn turn, JsonMapper mapper) {
    if (turn.getSpeaker() != Speaker.USER || turn.isRude() || turn.getBetterExpressions() == null) {
      return List.of();
    }
    try {
      String[] stored = mapper.readValue(turn.getBetterExpressions(), String[].class);
      var distinct = new LinkedHashMap<String, String>();
      if (stored != null) {
        for (String sentence : stored) {
          if (sentence != null
              && !sentence.isBlank()
              && !key(sentence).equals(key(turn.getText()))) {
            distinct.putIfAbsent(key(sentence), sentence.strip());
          }
          if (distinct.size() == 3) {
            break;
          }
        }
      }
      return List.copyOf(distinct.values());
    } catch (tools.jackson.core.JacksonException exception) {
      log.warn("turn_recommendations_read_failed turn_id={}", turn.getId());
      return List.of();
    }
  }

  /** 같은 문장을 대소문자·띄어쓰기·구두점만 바꿔 중복으로 보이지 않게 한다. */
  public static String key(String sentence) {
    return sentence.toLowerCase(Locale.ROOT).replaceAll("[^\\p{L}\\p{N}]+", " ").strip();
  }
}
