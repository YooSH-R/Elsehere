package com.elsehere.user.controller;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.user.service.WithdrawalService;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

// 무엇을 지우는지는 WithdrawalServiceTest에서 DB와 함께 검증하고, 여기서는 요청 검증과 세션 종료만 본다.
@SecuredWebMvcTest(controllers = WithdrawalController.class)
class WithdrawalControllerTest {

  private static final String PATH = "/api/v1/me";

  @Autowired private MockMvc mockMvc;

  @MockitoBean private WithdrawalService withdrawalService;

  @Test
  @DisplayName("탈퇴하면 그 사용자를 탈퇴 처리하고 204로 응답하며 세션을 끝낸다")
  void withdraw_respondsNoContentAndEndsSession() throws Exception {
    MockHttpSession session = new MockHttpSession();

    mockMvc
        .perform(
            delete(PATH)
                .session(session)
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isNoContent());

    then(withdrawalService).should().withdraw(1L);
    assertThat(session.isInvalid()).isTrue();
  }

  @Test
  @DisplayName("요청 확인 헤더가 없으면 탈퇴하지 않고 403 CSRF_HEADER_REQUIRED로 응답한다")
  void withdraw_withoutCsrfHeader_isRejected() throws Exception {
    mockMvc
        .perform(delete(PATH).with(authentication(loggedIn(1L))))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("CSRF_HEADER_REQUIRED"));

    then(withdrawalService).should(never()).withdraw(anyLong());
  }

  @Test
  @DisplayName("로그인하지 않으면 401 UNAUTHENTICATED로 응답한다")
  void withdraw_notLoggedIn_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(delete(PATH).header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));

    then(withdrawalService).should(never()).withdraw(anyLong());
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
