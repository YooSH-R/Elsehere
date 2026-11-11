package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.world.repository.NpcRepository;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;

// 음성 인식 단어(content/speech-words.json)가 시드의 NPC와 워커 장면 단어 상한에 맞는지 확인한다.
@DialogueServiceTest
class SpeechWordsTest {

  // 워커가 프롬프트에 붙이는 장면 단어 상한(ai/worker/app/stt.py)
  private static final int WORKER_WORD_LIMIT = 40;

  @Autowired private SpeechWords speechWords;

  @Autowired private NpcRepository npcRepository;

  @Test
  @DisplayName("단어를 둔 NPC 코드는 모두 시드에 있는 NPC다")
  void everyNpcCode_existsInSeed() {
    assertThat(speechWords.npcCodes())
        .isNotEmpty()
        .allSatisfy(code -> assertThat(npcRepository.findByCode(code)).as(code).isPresent());
  }

  @Test
  @DisplayName("학습자 닉네임·NPC 이름·같은 장소 NPC 이름·장소 단어·마을 단어를 모두 더해도 워커 상한 안에 든다")
  void allSceneWords_fitWorkerLimit() {
    int nameSlots = 2;
    assertThat(speechWords.npcCodes())
        .allSatisfy(
            code ->
                assertThat(
                        nameSlots
                            + speechWords.coworkerCodes(code).size()
                            + speechWords.placeWords(code).size()
                            + speechWords.townWords().size())
                    .as(code)
                    .isLessThanOrEqualTo(WORKER_WORD_LIMIT));
  }

  @Test
  @DisplayName("단어를 둔 NPC마다 장소 단어가 있다")
  void everyNpc_hasPlaceWords() {
    assertThat(speechWords.npcCodes())
        .allSatisfy(code -> assertThat(speechWords.placeWords(code)).as(code).isNotEmpty());
  }

  @Test
  @DisplayName("단어를 두지 않은 NPC는 장소 단어와 같은 장소 NPC가 없다")
  void unknownNpc_hasNoPlaceWordsOrCoworkers() {
    assertThat(speechWords.placeWords("TOWN_PASSERBY")).isEmpty();
    assertThat(speechWords.coworkerCodes("TOWN_PASSERBY")).isEmpty();
  }

  @Test
  @DisplayName("마을 단어는 마을 이름과 가게 이름이다")
  void townWords_haveTownAndShopNames() {
    assertThat(speechWords.townWords())
        .startsWith("Maple Town")
        .contains("Maple Bakery", "Maple Diner", "Little Arcade", "Suds Laundromat");
  }

  @Test
  @DisplayName("같은 장소 NPC는 자기 자신을 뺀 그 장소의 NPC다")
  void coworkers_areOtherNpcsAtSamePlace() {
    assertThat(speechWords.coworkerCodes("CAFE_STAFF"))
        .containsExactly("CAFE_BARISTA", "CAFE_SERVER");
    assertThat(speechWords.coworkerCodes("BAKERY_STAFF")).isEmpty();
  }
}
