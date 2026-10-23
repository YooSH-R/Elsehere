package com.elsehere.onboarding.service;

import java.util.List;
import org.springframework.stereotype.Component;

/**
 * 학습 언어 목록(ONB-01). MVP에서 고를 수 있는 언어는 en-US뿐이다.
 *
 * <p>언어 범위와 표시 이름은 정해지지 않아, 정해지면 이 목록을 바꾼다.
 */
@Component
public class LanguageCatalog {

  private static final List<Language> LANGUAGES =
      List.of(new Language("en-US", "English (US)", "영어 · 미국식 발음", true));

  public List<Language> languages() {
    return LANGUAGES;
  }

  /** 목록에 있고 지금 고를 수 있는 언어인지. */
  public boolean isSelectable(String languageCode) {
    return LANGUAGES.stream()
        .anyMatch(language -> language.available() && language.languageCode().equals(languageCode));
  }
}
