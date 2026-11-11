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
 * 음성 인식의 장면 단어로 보내 철자를 알려 줄 단어. 마을 이름·가게 이름처럼 어디서나 말할 고유 이름과, NPC가 일하는 장소에서 학습자가 말할 메뉴·상품·서비스 이름이다.
 * 한국식 발음 '아메리카노'가 American으로, Maple Town이 다른 말로 적히지 않게 한다. 단어를 늘릴 때는 코드가 아니라 {@value #SOURCE}에 더한다.
 */
@Component
public class SpeechWords {

  static final String SOURCE = "content/speech-words.json";

  private final List<String> townWords;
  private final Map<String, Place> placeByNpc = new HashMap<>();

  public SpeechWords() {
    Source source = read();
    townWords = List.copyOf(source.town());
    for (Place place : source.places()) {
      for (String npc : place.npcs()) {
        placeByNpc.put(npc, place);
      }
    }
  }

  /** 마을 이름과 3D 마을에 있는 가게 이름. 대화 상대와 상관없이 늘 보낸다. */
  List<String> townWords() {
    return townWords;
  }

  /** 이 NPC 장소의 단어. 단어를 두지 않은 NPC(행인·택시 등)는 빈 목록이다. */
  List<String> placeWords(String npcCode) {
    Place place = placeByNpc.get(npcCode);
    return place == null ? List.of() : List.copyOf(place.words());
  }

  /** 이 NPC와 같은 장소에서 일하는 다른 NPC 코드. 학습자가 동료 이름을 물을 수 있다. */
  List<String> coworkerCodes(String npcCode) {
    Place place = placeByNpc.get(npcCode);
    return place == null
        ? List.of()
        : place.npcs().stream().filter(code -> !code.equals(npcCode)).toList();
  }

  Set<String> npcCodes() {
    return Set.copyOf(placeByNpc.keySet());
  }

  private static Source read() {
    try (InputStream input = new ClassPathResource(SOURCE).getInputStream()) {
      return new JsonMapper().readValue(input, Source.class);
    } catch (IOException exception) {
      throw new UncheckedIOException(SOURCE + "을 읽지 못했습니다.", exception);
    }
  }

  private record Source(List<String> town, List<Place> places) {}

  /** 같은 장소에서 일하는 NPC들과 그 장소의 단어. */
  private record Place(List<String> npcs, List<String> words) {}
}
