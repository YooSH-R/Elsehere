package com.elsehere.auth.controller;

import com.elsehere.auth.dto.GuestLoginRequest;
import com.elsehere.auth.dto.SignupResponse;
import com.elsehere.auth.service.GuestLoginService;
import com.elsehere.common.auth.LoginSessionService;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.user.dto.UserResponse;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

/**
 * 게스트로 시작하기(AUTH-07). 캐릭터만 받아 새 사용자를 만들고 그 사용자로 로그인한다.
 *
 * <p>로그인 전에 부르는 API이므로 보안 설정에서 이 경로를 열어 둔다.
 */
@RestController
@RequestMapping("/api/v1/auth/guest")
@RequiredArgsConstructor
public class GuestLoginController {

  private final GuestLoginService guestLoginService;
  private final LoginSessionService loginSessionService;

  @PostMapping
  @ResponseStatus(HttpStatus.CREATED)
  public SignupResponse startAsGuest(
      @Valid @RequestBody GuestLoginRequest request,
      HttpServletRequest httpRequest,
      HttpServletResponse httpResponse) {
    UserResponse user = guestLoginService.startAsGuest(request.avatarType());
    loginSessionService.login(new LoginUser(user.userId()), httpRequest, httpResponse);
    return new SignupResponse(user);
  }
}
