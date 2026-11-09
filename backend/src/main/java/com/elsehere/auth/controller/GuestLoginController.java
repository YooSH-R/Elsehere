package com.elsehere.auth.controller;

import com.elsehere.ai.service.GuestConversationLimit;
import com.elsehere.auth.dto.GuestGuideResponse;
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
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

/**
 * 게스트 안내(AUTH-08)와 게스트로 시작하기(AUTH-07). 시작하기는 캐릭터만 받아 새 사용자를 만들고 그 사용자로 로그인한다.
 *
 * <p>로그인 전에 부르는 API이므로 보안 설정에서 이 경로를 열어 둔다.
 */
@RestController
@RequestMapping("/api/v1/auth/guest")
@RequiredArgsConstructor
public class GuestLoginController {

  private final GuestLoginService guestLoginService;
  private final LoginSessionService loginSessionService;
  private final GuestConversationLimit guestConversationLimit;

  /** 캐릭터 선택 화면에서 게스트가 NPC와 몇 번 대화할 수 있는지 미리 알린다. 계정을 만들기 전이라 서버 설정값을 그대로 준다. */
  @GetMapping
  public GuestGuideResponse getGuestGuide() {
    return new GuestGuideResponse(guestConversationLimit.guestLimit());
  }

  @PostMapping
  @ResponseStatus(HttpStatus.CREATED)
  public SignupResponse startAsGuest(
      @Valid @RequestBody GuestLoginRequest request,
      HttpServletRequest httpRequest,
      HttpServletResponse httpResponse) {
    UserResponse user =
        guestLoginService.startAsGuest(request.avatarType(), request.agreedToVoice());
    loginSessionService.loginGuest(new LoginUser(user.userId()), httpRequest, httpResponse);
    return new SignupResponse(user);
  }
}
