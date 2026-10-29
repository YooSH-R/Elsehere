package com.elsehere.user.controller;

import com.elsehere.auth.service.PendingSignup;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.user.dto.NicknameAvailabilityResponse;
import com.elsehere.user.dto.NicknameChangeRequest;
import com.elsehere.user.dto.NicknameChangeResponse;
import com.elsehere.user.service.NicknameService;
import com.elsehere.user.validation.Nickname;
import jakarta.validation.Valid;
import lombok.RequiredArgsConstructor;
import org.springframework.security.core.annotation.AuthenticationPrincipal;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.bind.annotation.SessionAttribute;

/**
 * 닉네임 중복 확인(AUTH-06)과 닉네임 변경(USER-04). 중복 확인은 가입 정보 입력(화면 02)과 마이페이지에서 쓴다.
 *
 * <p>가입 대기 사용자는 로그인 상태가 아니므로 보안 설정에서 중복 확인 경로를 열어 두고, 호출 자격은 여기서 확인한다.
 */
@RestController
@RequiredArgsConstructor
public class NicknameController {

  private final NicknameService nicknameService;

  @GetMapping("/api/v1/nicknames/availability")
  public NicknameAvailabilityResponse checkAvailability(
      @AuthenticationPrincipal LoginUser loginUser,
      @SessionAttribute(name = PendingSignup.SESSION_ATTRIBUTE, required = false)
          PendingSignup pendingSignup,
      @RequestParam @Nickname String nickname) {
    if (loginUser == null && pendingSignup == null) {
      throw new BusinessException(ErrorCode.UNAUTHENTICATED);
    }
    return new NicknameAvailabilityResponse(nickname, nicknameService.isAvailable(nickname));
  }

  @PutMapping("/api/v1/me/nickname")
  public NicknameChangeResponse changeNickname(
      @AuthenticationPrincipal LoginUser loginUser,
      @Valid @RequestBody NicknameChangeRequest request) {
    return nicknameService.changeNickname(loginUser.userId(), request.nickname());
  }
}
