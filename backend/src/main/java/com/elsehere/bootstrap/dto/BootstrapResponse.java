package com.elsehere.bootstrap.dto;

import com.elsehere.ai.dto.AiStatusResponse;
import com.elsehere.user.dto.UserResponse;
import com.fasterxml.jackson.annotation.JsonInclude;

/**
 * BOOT-01 응답. 접속 직후 첫 화면을 정하는 데 쓴다.
 *
 * <p>가입 대기 상태면 {@code authState}만 담는다. 진행 중인 대화·진행도는 해당 기능을 만들 때 추가한다.
 *
 * @param lastSceneCode 재접속할 장소. 없으면 응답에 담지 않으며 클라이언트는 마을로 보낸다.
 * @param aiStatus HUD의 "AI 사용 가능" 표시에 쓰는 AI 이용 상태. 형식은 AI-01과 같다.
 */
@JsonInclude(JsonInclude.Include.NON_NULL)
public record BootstrapResponse(
    AuthState authState, UserResponse user, String lastSceneCode, AiStatusResponse aiStatus) {

  public static BootstrapResponse signupRequired() {
    return new BootstrapResponse(AuthState.SIGNUP_REQUIRED, null, null, null);
  }

  public static BootstrapResponse signedIn(
      UserResponse user, String lastSceneCode, AiStatusResponse aiStatus) {
    return new BootstrapResponse(AuthState.SIGNED_IN, user, lastSceneCode, aiStatus);
  }
}
