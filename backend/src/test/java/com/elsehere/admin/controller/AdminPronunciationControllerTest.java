package com.elsehere.admin.controller;

import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.admin.dto.PronunciationRun;
import com.elsehere.admin.dto.PronunciationStatusResponse;
import com.elsehere.admin.service.AdminLogin;
import com.elsehere.admin.service.AdminPronunciationService;
import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

// 시작·중복 실행 규칙은 AdminPronunciationServiceTest에서 검증하고, 여기서는 관리자 권한과 요청·응답 연결만 본다.
@SecuredWebMvcTest(controllers = AdminPronunciationController.class)
class AdminPronunciationControllerTest {

  private static final String URL = "/api/v1/admin/pronunciations";

  @Autowired private MockMvc mockMvc;

  @MockitoBean private AdminPronunciationService adminPronunciationService;

  @Test
  @DisplayName("관리자가 발음 만들기를 시작하면 202와 만드는 중인 진행 상황을 돌려받는다")
  void start_byAdmin_respondsAccepted() throws Exception {
    given(adminPronunciationService.start())
        .willReturn(new PronunciationStatusResponse(106, 1, 105, true, null));

    mockMvc
        .perform(
            post(URL)
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereAdmin")
                .with(authentication(adminLogin())))
        .andExpect(status().isAccepted())
        .andExpect(jsonPath("$.total").value(106))
        .andExpect(jsonPath("$.ready").value(1))
        .andExpect(jsonPath("$.missing").value(105))
        .andExpect(jsonPath("$.running").value(true));
  }

  @Test
  @DisplayName("게임 사용자로 로그인했어도 401 UNAUTHENTICATED로 거절하고 시작하지 않는다")
  void start_byGameUser_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(
            post(URL)
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereAdmin")
                .with(authentication(gameLogin())))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
    then(adminPronunciationService).should(never()).start();
  }

  @Test
  @DisplayName("관리자는 진행 상황과 마지막 결과(만든 수·실패한 단어)를 조회한다")
  void status_byAdmin_respondsProgressAndLastRun() throws Exception {
    given(adminPronunciationService.status())
        .willReturn(
            new PronunciationStatusResponse(
                106, 105, 1, false, new PronunciationRun(104, List.of("sofa"))));

    mockMvc
        .perform(get(URL).with(authentication(adminLogin())))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.missing").value(1))
        .andExpect(jsonPath("$.running").value(false))
        .andExpect(jsonPath("$.lastRun.created").value(104))
        .andExpect(jsonPath("$.lastRun.failedWordKeys[0]").value("sofa"));
  }

  private static Authentication adminLogin() {
    return UsernamePasswordAuthenticationToken.authenticated(
        new AdminLogin("operator"), null, List.of());
  }

  private static Authentication gameLogin() {
    return UsernamePasswordAuthenticationToken.authenticated(new LoginUser(2L), null, List.of());
  }
}
