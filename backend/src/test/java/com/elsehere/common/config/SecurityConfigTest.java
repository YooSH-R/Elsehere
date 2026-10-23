package com.elsehere.common.config;

import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.auth.LoginUserTestController;
import com.elsehere.common.error.ErrorCode;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.webmvc.test.autoconfigure.WebMvcTest;
import org.springframework.context.annotation.Import;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.web.servlet.MockMvc;

@WebMvcTest(controllers = LoginUserTestController.class)
@Import(SecurityConfig.class)
class SecurityConfigTest {

  @Autowired private MockMvc mockMvc;

  @Test
  @DisplayName("로그인하지 않고 API를 호출하면 401 UNAUTHENTICATED로 응답한다")
  void unauthenticatedRequest_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(get("/api/v1/test/me"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"))
        .andExpect(jsonPath("$.error.message").value(ErrorCode.UNAUTHENTICATED.getMessage()));
  }

  @Test
  @DisplayName("로그인한 사용자는 컨트롤러에서 자기 사용자 ID를 받는다")
  void authenticatedRequest_injectsLoginUser() throws Exception {
    mockMvc
        .perform(get("/api/v1/test/me").with(authentication(loggedIn(7L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.userId").value(7));
  }

  @Test
  @DisplayName("상태를 바꾸는 요청에 X-Requested-With 헤더가 없으면 403 CSRF_HEADER_REQUIRED로 거절한다")
  void stateChangingRequestWithoutHeader_isRejected() throws Exception {
    mockMvc
        .perform(post("/api/v1/test/me").with(authentication(loggedIn(7L))))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("CSRF_HEADER_REQUIRED"));
  }

  @Test
  @DisplayName("X-Requested-With 헤더가 있는 상태 변경 요청은 처리한다")
  void stateChangingRequestWithHeader_isProcessed() throws Exception {
    mockMvc
        .perform(
            post("/api/v1/test/me")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .with(authentication(loggedIn(7L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.userId").value(7));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
