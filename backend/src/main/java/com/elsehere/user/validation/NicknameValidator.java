package com.elsehere.user.validation;

import jakarta.validation.ConstraintValidator;
import jakarta.validation.ConstraintValidatorContext;

/** {@link Nickname} 규칙 검사. 한글은 화면에서 영문보다 넓게 보여 2로 센다. */
public class NicknameValidator implements ConstraintValidator<Nickname, String> {

  private static final int MIN_LENGTH = 2;
  private static final int MAX_WIDTH = 16;
  private static final int HANGUL_WIDTH = 2;
  private static final int ASCII_WIDTH = 1;

  @Override
  public boolean isValid(String nickname, ConstraintValidatorContext context) {
    if (nickname == null) {
      return true;
    }
    if (nickname.length() < MIN_LENGTH) {
      return false;
    }
    int width = 0;
    for (char letter : nickname.toCharArray()) {
      if (isHangulSyllable(letter)) {
        width += HANGUL_WIDTH;
      } else if (isAsciiLetterOrDigit(letter)) {
        width += ASCII_WIDTH;
      } else {
        return false;
      }
    }
    return width <= MAX_WIDTH;
  }

  /** 완성형 한글 음절(가~힣). 자음·모음만 있는 글자(ㅋ, ㅏ)는 이 범위 밖이다. */
  private static boolean isHangulSyllable(char letter) {
    return letter >= '가' && letter <= '힣';
  }

  private static boolean isAsciiLetterOrDigit(char letter) {
    return (letter >= 'a' && letter <= 'z')
        || (letter >= 'A' && letter <= 'Z')
        || (letter >= '0' && letter <= '9');
  }
}
