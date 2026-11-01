package com.elsehere.vocab.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.conversation.dto.GlossaryResponse;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class SourceWordsTest {

  private static final GlossaryResponse LATTE = new GlossaryResponse("latte", "라떼");
  private static final GlossaryResponse SOY_MILK = new GlossaryResponse("soy milk", "두유");
  private static final List<GlossaryResponse> GLOSSARY = List.of(LATTE, SOY_MILK);

  @Test
  @DisplayName("출처에 있는 단어는 대소문자와 앞뒤 공백이 달라도 찾는다")
  void find_wordInSource_ignoresCaseAndSurroundingSpaces() {
    assertThat(SourceWords.find(GLOSSARY, GlossaryResponse::text, " Latte ")).contains(LATTE);
  }

  @Test
  @DisplayName("단어 하나만 저장하므로 두 단어 이상인 표현은 출처에 있어도 찾지 않는다")
  void find_multiWordExpression_isNotSavable() {
    assertThat(SourceWords.find(GLOSSARY, GlossaryResponse::text, "soy milk")).isEmpty();
  }

  @Test
  @DisplayName("출처에 없는 단어는 찾지 않는다")
  void find_wordNotInSource_isEmpty() {
    assertThat(SourceWords.find(GLOSSARY, GlossaryResponse::text, "coffee")).isEmpty();
  }
}
