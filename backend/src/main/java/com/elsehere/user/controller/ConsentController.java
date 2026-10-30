package com.elsehere.user.controller;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.user.dto.ConsentChangeRequest;
import com.elsehere.user.dto.ConsentResponse;
import com.elsehere.user.dto.ConsentsResponse;
import com.elsehere.user.service.OptionalConsentService;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

/** 선택 동의(음성 처리·AI 개선)를 가입 뒤에도 보고 켜고 끈다(USER-02·03). */
@RestController
@RequiredArgsConstructor
public class ConsentController {

  private static final String PATH = "/api/v1/me/consents";

  private final OptionalConsentService optionalConsentService;

  @GetMapping(PATH)
  public ConsentsResponse findAll(@AuthenticationPrincipal LoginUser loginUser) {
    return optionalConsentService.findAll(loginUser.userId());
  }

  @PutMapping(PATH)
  public ConsentResponse change(
      @AuthenticationPrincipal LoginUser loginUser,
      @Valid @RequestBody ConsentChangeRequest request) {
    return optionalConsentService.change(
        loginUser.userId(), request.consentType(), request.agreed());
  }
}
