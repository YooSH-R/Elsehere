package com.elsehere.world.controller;

import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.world.dto.GameClockResponse;
import com.elsehere.world.service.GameClockService;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

// 시간 계산은 GameClockServiceTest에서 DB와 함께 검증하고, 여기서는 요청·응답 연결만 본다.
@SecuredWebMvcTest(controllers = GameClockController.class)
class GameClockControllerTest {

  private static final String PATH = "/api/v1/me/game-clock";

  @Autowired private MockMvc mockMvc;

  @MockitoBean private GameClockService gameClockService;

  @Test
  @DisplayName("GET은 로그인한 사용자의 게임 날짜·하루 중 분·멈춤 여부를 돌려준다")
  void now_respondsGameClock() throws Exception {
    given(gameClockService.now(1L)).willReturn(new GameClockResponse(3, 615, false));

    mockMvc
        .perform(get(PATH).with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.day").value(3))
        .andExpect(jsonPath("$.minuteOfDay").value(615))
        .andExpect(jsonPath("$.paused").value(false));
  }

  @Test
  @DisplayName("신호(POST beats)는 로그인한 사용자의 시계를 흐르게 하고 지금 게임 시각을 돌려준다")
  void beat_respondsGameClock() throws Exception {
    given(gameClockService.beat(1L)).willReturn(new GameClockResponse(1, 552, true));

    mockMvc
        .perform(
            post(PATH + "/beats")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.day").value(1))
        .andExpect(jsonPath("$.minuteOfDay").value(552))
        .andExpect(jsonPath("$.paused").value(true));
  }

  @Test
  @DisplayName("로그인하지 않으면 401 UNAUTHENTICATED로 응답한다")
  void notLoggedIn_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(post(PATH + "/beats").header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  @Test
  @DisplayName("신호에 X-Requested-With 헤더가 없으면 403 CSRF_HEADER_REQUIRED로 거절한다")
  void beatWithoutCsrfHeader_isRejected() throws Exception {
    mockMvc
        .perform(post(PATH + "/beats").with(authentication(loggedIn(1L))))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("CSRF_HEADER_REQUIRED"));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
