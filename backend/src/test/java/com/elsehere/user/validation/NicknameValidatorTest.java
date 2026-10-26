package com.elsehere.user.validation;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

class NicknameValidatorTest {

  private final NicknameValidator validator = new NicknameValidator();

  @ParameterizedTest
  @ValueSource(
      strings = {"Sun", "선종", "Sun선종12", "ab", "가나다라마바사아", "abcdefghijklmnop", "가나다라마바사ab"})
  @DisplayName("한글 완성형·영문·숫자로 2자 이상이고 한글 2, 영문·숫자 1로 센 합계가 16 이하면 통과한다")
  void validNickname_passes(String nickname) {
    assertThat(validator.isValid(nickname, null)).isTrue();
  }

  @ParameterizedTest
  @ValueSource(strings = {"Sun Lee", "Sun!", "선_종", "ㅋㅋ", "ㅏㅏ", "😀😀", "Ｓｕｎ", "Café"})
  @DisplayName("공백·특수문자·이모지, 자음·모음만 있는 글자, 전각·악센트 문자가 있으면 거절한다")
  void nicknameWithDisallowedCharacter_isRejected(String nickname) {
    assertThat(validator.isValid(nickname, null)).isFalse();
  }

  @ParameterizedTest
  @ValueSource(strings = {"", "a", "7", "선"})
  @DisplayName("2자보다 짧으면 한글 한 자(2로 셈)여도 거절한다")
  void nicknameShorterThanTwoCharacters_isRejected(String nickname) {
    assertThat(validator.isValid(nickname, null)).isFalse();
  }

  @ParameterizedTest
  @ValueSource(strings = {"가나다라마바사아자", "abcdefghijklmnopq", "가나다라마바사abc", "1234567890123456a"})
  @DisplayName("한글 2, 영문·숫자 1로 센 합계가 16을 넘으면 거절한다")
  void nicknameWiderThanSixteen_isRejected(String nickname) {
    assertThat(validator.isValid(nickname, null)).isFalse();
  }

  @Test
  @DisplayName("값이 없으면 필수 여부는 @NotNull에 맡기고 통과시킨다")
  void nullNickname_isLeftToNotNull() {
    assertThat(validator.isValid(null, null)).isTrue();
  }
}
