package com.elsehere.dialogue.service;

import java.io.IOException;
import java.io.InputStream;
import java.io.UncheckedIOException;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import org.springframework.core.io.ClassPathResource;
import org.springframework.stereotype.Component;
import tools.jackson.databind.json.JsonMapper;

/**
 * NPC가 일하는 장소에서 학습자가 말할 메뉴·상품·서비스 이름. 음성 인식의 장면 단어로 보내 한국식 발음 '아메리카노'가 American으로 적히지 않게 철자를 알려 준다.
 * 장소를 늘릴 때는 코드가 아니라 {@value #SOURCE}에 단어를 더한다.
 */
@Component
public class SpeechWords {

  static final String SOURCE = "content/speech-words.json";

  private final Map<String, List<String>> wordsByNpc = new HashMap<>();

  public SpeechWords() {
    for (Place place : read()) {
      for (String npc : place.npcs()) {
        wordsByNpc.put(npc, List.copyOf(place.words()));
      }
    }
  }

  /** 이 NPC 장소의 단어. 단어를 두지 않은 NPC(행인·택시 등)는 빈 목록이다. */
  List<String> placeWords(String npcCode) {
    return wordsByNpc.getOrDefault(npcCode, List.of());
  }

  Set<String> npcCodes() {
    return Set.copyOf(wordsByNpc.keySet());
  }

  private static Place[] read() {
    try (InputStream input = new ClassPathResource(SOURCE).getInputStream()) {
      return new JsonMapper().readValue(input, Place[].class);
    } catch (IOException exception) {
      throw new UncheckedIOException(SOURCE + "을 읽지 못했습니다.", exception);
    }
  }

  /** 같은 장소에서 일하는 NPC들과 그 장소의 단어. */
  private record Place(List<String> npcs, List<String> words) {}
}
