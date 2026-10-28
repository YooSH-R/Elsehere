package com.elsehere.onboarding.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.onboarding.dto.LanguageProfileResponse;
import com.elsehere.onboarding.dto.LanguageSelectionResponse;
import com.elsehere.onboarding.dto.LevelSelectionRequest;
import com.elsehere.onboarding.dto.LevelSelectionResponse;
import com.elsehere.onboarding.service.Language;
import com.elsehere.onboarding.service.LanguageCatalog;
import com.elsehere.onboarding.service.OnboardingService;
import jakarta.validation.Valid;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

/** 가입 후 초기 설정(ONB-01·02·08·09). */
@RestController
@RequiredArgsConstructor
public class OnboardingController {

  private final LanguageCatalog languageCatalog;
  private final OnboardingService onboardingService;

  @GetMapping("/api/v1/languages")
  public List<Language> getLanguages() {
    return languageCatalog.languages();
  }

  @PutMapping("/api/v1/me/languages/{languageCode}")
  public LanguageSelectionResponse chooseLanguage(
      @AuthenticationPrincipal LoginUser loginUser, @PathVariable String languageCode) {
    return onboardingService.chooseLanguage(loginUser.userId(), languageCode);
  }

  @GetMapping("/api/v1/me/languages/{languageCode}")
  public LanguageProfileResponse getLanguageProfile(
      @AuthenticationPrincipal LoginUser loginUser, @PathVariable String languageCode) {
    return onboardingService.getLanguageProfile(loginUser.userId(), languageCode);
  }

  @PutMapping("/api/v1/me/languages/{languageCode}/level")
  public LevelSelectionResponse selectLevel(
      @AuthenticationPrincipal LoginUser loginUser,
      @PathVariable String languageCode,
      @Valid @RequestBody LevelSelectionRequest request) {
    return onboardingService.selectLevel(loginUser.userId(), languageCode, request.selectedLevel());
  }
}
