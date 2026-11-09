package com.elsehere.dialogue.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.world.repository.NpcRepository;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;

// 장소별 음성 인식 단어(content/speech-words.json)가 시드의 NPC와 워커 장면 단어 상한에 맞는지 확인한다.
@DialogueServiceTest
class SpeechWordsTest {

  // 워커가 프롬프트에 붙이는 장면 단어 상한(ai/worker/app/stt.py)에서 학습자 닉네임·NPC 이름 두 칸을 뺀 수
  private static final int PLACE_WORD_LIMIT = 28;

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
  @DisplayName("NPC마다 장소 단어는 이름 두 칸을 남기고 워커 상한 안에 든다")
  void placeWords_fitWorkerLimit() {
    assertThat(speechWords.npcCodes())
        .allSatisfy(
            code ->
                assertThat(speechWords.placeWords(code))
                    .as(code)
                    .isNotEmpty()
                    .hasSizeLessThanOrEqualTo(PLACE_WORD_LIMIT));
  }

  @Test
  @DisplayName("단어를 두지 않은 NPC는 장소 단어가 없다")
  void unknownNpc_hasNoPlaceWords() {
    assertThat(speechWords.placeWords("TOWN_PASSERBY")).isEmpty();
  }
}
