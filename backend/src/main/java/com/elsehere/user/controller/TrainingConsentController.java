package com.elsehere.user.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.user.dto.TrainingConsentRequest;
import com.elsehere.user.dto.TrainingConsentResponse;
import com.elsehere.user.dto.TrainingConsentsResponse;
import com.elsehere.user.service.TrainingConsentService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

/** AI 개선(학습·평가) 선택 동의를 가입 뒤에도 보고 켜고 끈다(USER-02·03). */
@RestController
@RequiredArgsConstructor
public class TrainingConsentController {

  private static final String PATH = "/api/v1/me/training-consents";

  private final TrainingConsentService trainingConsentService;

  @GetMapping(PATH)
  public TrainingConsentsResponse findAll(@AuthenticationPrincipal LoginUser loginUser) {
    return trainingConsentService.findAll(loginUser.userId());
  }

  @PutMapping(PATH)
  public TrainingConsentResponse change(
      @AuthenticationPrincipal LoginUser loginUser,
      @Valid @RequestBody TrainingConsentRequest request) {
    return trainingConsentService.change(
        loginUser.userId(), request.consentType(), request.agreed());
  }
}
