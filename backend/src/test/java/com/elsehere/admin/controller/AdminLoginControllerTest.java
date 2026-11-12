package com.elsehere.admin.controller;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.admin.service.AdminLogin;
import com.elsehere.admin.service.AdminLoginService;
import com.elsehere.admin.service.AdminLoginSession;
import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.auth.LoginUserTestController;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.context.SecurityContextImpl;
import org.springframework.security.web.context.HttpSessionSecurityContextRepository;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;

// 아이디·비밀번호 판정과 잠금은 AdminLoginServiceTest가 맡는다. 여기서는 판정 결과로 세션에 어드민 로그인을 남기고 지우는지 본다.
@SecuredWebMvcTest(
    controllers = {
      AdminLoginController.class,
      AdminLoginTestController.class,
      LoginUserTestController.class
    })
class AdminLoginControllerTest {

  private static final String LOGIN_URL = "/api/v1/admin/login";
  private static final String LOGOUT_URL = "/api/v1/admin/logout";
  private static final String LOGIN_BODY =
      """
      {"loginId": "operator", "password": "correct-horse-battery"}
      """;

  @Autowired private MockMvc mockMvc;

  @MockitoBean private AdminLoginService adminLoginService;

  @Test
  @DisplayName("아이디·비밀번호가 맞으면 204로 답하고 같은 세션으로 관리 API를 쓴다")
  void login_withAdminAccount_logsInSession() throws Exception {
    given(adminLoginService.login("operator", "correct-horse-battery"))
        .willReturn(new AdminLogin("operator"));
    MockHttpSession session = new MockHttpSession();

    mockMvc.perform(login(LOGIN_BODY).session(session)).andExpect(status().isNoContent());

    mockMvc
        .perform(get("/api/v1/admin/test/me").session(session))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.loginId").value("operator"));
  }

  @Test
  @DisplayName("로그인하면 로그인 전 세션 ID를 바꾼다")
  void login_changesSessionId() throws Exception {
    given(adminLoginService.login("operator", "correct-horse-battery"))
        .willReturn(new AdminLogin("operator"));
    MockHttpSession session = new MockHttpSession();
    String idBeforeLogin = session.getId();

    mockMvc.perform(login(LOGIN_BODY).session(session)).andExpect(status().isNoContent());

    assertThat(session.getId()).isNotEqualTo(idBeforeLogin);
  }

  @Test
  @DisplayName("이미 게임에 로그인한 세션에서 어드민으로 로그인해도 게임 로그인은 그대로다")
  void login_keepsGameLogin() throws Exception {
    given(adminLoginService.login("operator", "correct-horse-battery"))
        .willReturn(new AdminLogin("operator"));
    MockHttpSession session = gameSession();

    mockMvc.perform(login(LOGIN_BODY).session(session)).andExpect(status().isNoContent());

    mockMvc
        .perform(get("/api/v1/test/me").session(session))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.userId").value(7));
  }

  @Test
  @DisplayName("아이디나 비밀번호가 틀리면 401 ADMIN_LOGIN_FAILED이고 로그인되지 않는다")
  void login_withWrongAccount_respondsLoginFailed() throws Exception {
    given(adminLoginService.login("operator", "correct-horse-battery"))
        .willThrow(new BusinessException(ErrorCode.ADMIN_LOGIN_FAILED));
    MockHttpSession session = new MockHttpSession();

    mockMvc
        .perform(login(LOGIN_BODY).session(session))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("ADMIN_LOGIN_FAILED"));

    mockMvc
        .perform(get("/api/v1/admin/test/me").session(session))
        .andExpect(status().isUnauthorized());
  }

  @Test
  @DisplayName("여러 번 틀려 잠긴 동안에는 429 ADMIN_LOGIN_LOCKED다")
  void login_whileLocked_respondsLocked() throws Exception {
    given(adminLoginService.login("operator", "correct-horse-battery"))
        .willThrow(new BusinessException(ErrorCode.ADMIN_LOGIN_LOCKED));

    mockMvc
        .perform(login(LOGIN_BODY))
        .andExpect(status().isTooManyRequests())
        .andExpect(jsonPath("$.error.code").value("ADMIN_LOGIN_LOCKED"));
  }

  @Test
  @DisplayName("아이디나 비밀번호가 비면 400 VALIDATION_FAILED이고 확인하지 않는다")
  void login_withBlankField_isRejected() throws Exception {
    mockMvc
        .perform(login("{\"loginId\": \"operator\", \"password\": \"\"}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));

    then(adminLoginService).should(never()).login(any(), any());
  }

  @Test
  @DisplayName("요청 확인 헤더 없이 로그인하면 403 CSRF_HEADER_REQUIRED이고 확인하지 않는다")
  void login_withoutCsrfHeader_isRejected() throws Exception {
    mockMvc
        .perform(post(LOGIN_URL).contentType(MediaType.APPLICATION_JSON).content(LOGIN_BODY))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("CSRF_HEADER_REQUIRED"));

    then(adminLoginService).should(never()).login(any(), any());
  }

  @Test
  @DisplayName("어드민 로그아웃은 204로 답하고 어드민 로그인만 지워 같은 세션의 게임 로그인은 남긴다")
  void logout_clearsOnlyAdminLogin() throws Exception {
    MockHttpSession session = gameSession();
    session.setAttribute(
        AdminLoginSession.SESSION_ATTRIBUTE,
        new SecurityContextImpl(
            UsernamePasswordAuthenticationToken.authenticated(
                new AdminLogin("operator"), null, List.of())));

    mockMvc
        .perform(
            post(LOGOUT_URL).session(session).header(CsrfHeaderFilter.HEADER_NAME, "ElsehereAdmin"))
        .andExpect(status().isNoContent());

    mockMvc
        .perform(get("/api/v1/admin/test/me").session(session))
        .andExpect(status().isUnauthorized());
    mockMvc.perform(get("/api/v1/test/me").session(session)).andExpect(status().isOk());
  }

  private static MockHttpServletRequestBuilder login(String body) {
    return post(LOGIN_URL)
        .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereAdmin")
        .contentType(MediaType.APPLICATION_JSON)
        .content(body);
  }

  private static MockHttpSession gameSession() {
    MockHttpSession session = new MockHttpSession();
    session.setAttribute(
        HttpSessionSecurityContextRepository.SPRING_SECURITY_CONTEXT_KEY,
        new SecurityContextImpl(
            UsernamePasswordAuthenticationToken.authenticated(new LoginUser(7L), null, List.of())));
    return session;
  }
}
