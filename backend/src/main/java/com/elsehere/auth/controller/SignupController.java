package com.elsehere.auth.controller;

import com.elsehere.auth.dto.SignupFormResponse;
import com.elsehere.auth.dto.SignupRequest;
import com.elsehere.auth.dto.SignupResponse;
import com.elsehere.auth.service.ConsentCatalog;
import com.elsehere.auth.service.PendingSignup;
import com.elsehere.auth.service.SignupService;
import com.elsehere.common.auth.LoginSessionService;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.user.dto.UserResponse;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.bind.annotation.SessionAttribute;

/**
 * 처음 로그인한 사용자의 가입(AUTH-03·04). Google 로그인(AUTH-02)이 세션에 남긴 가입 대기 정보를 쓴다.
 *
 * <p>가입 전에는 로그인 상태가 아니므로 보안 설정에서 이 경로를 열어 두고, 가입 대기 여부는 여기서 확인한다.
 */
@RestController
@RequestMapping("/api/v1/auth/signup")
@RequiredArgsConstructor
public class SignupController {

  private final ConsentCatalog consentCatalog;
  private final SignupService signupService;
  private final LoginSessionService loginSessionService;

  @GetMapping
  public SignupFormResponse getSignupForm(
      @SessionAttribute(name = PendingSignup.SESSION_ATTRIBUTE, required = false)
          PendingSignup pendingSignup) {
    requirePendingSignup(pendingSignup);
    return new SignupFormResponse(
        pendingSignup.email(),
        consentCatalog.currentTerms(),
        signupService.findConvertingGuest(pendingSignup).orElse(null));
  }

  @PostMapping
  @ResponseStatus(HttpStatus.CREATED)
  public SignupResponse signUp(
      @AuthenticationPrincipal LoginUser loginUser,
      @SessionAttribute(name = PendingSignup.SESSION_ATTRIBUTE, required = false)
          PendingSignup pendingSignup,
      @Valid @RequestBody SignupRequest request,
      HttpServletRequest httpRequest,
      HttpServletResponse httpResponse) {
    if (loginUser != null) {
      throw new BusinessException(ErrorCode.ALREADY_SIGNED_UP);
    }
    requirePendingSignup(pendingSignup);
    UserResponse user = signupService.signUp(pendingSignup, request);

    loginSessionService.login(new LoginUser(user.userId()), httpRequest, httpResponse);
    httpRequest.getSession().removeAttribute(PendingSignup.SESSION_ATTRIBUTE);
    return new SignupResponse(user);
  }

  /** Google 로그인을 거치지 않아 가입 대기 정보가 없으면 로그인부터 해야 한다. */
  private static void requirePendingSignup(PendingSignup pendingSignup) {
    if (pendingSignup == null) {
      throw new BusinessException(ErrorCode.UNAUTHENTICATED);
    }
  }
}
