package com.elsehere.admin.config;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.redirectedUrl;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.admin.controller.AdminLoginTestController;
import com.elsehere.admin.service.AdminLogin;
import com.elsehere.admin.service.AdminLoginSession;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.auth.LoginUserTestController;
import com.elsehere.common.config.SecuredWebMvcTest;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.context.SecurityContextImpl;
import org.springframework.security.web.context.HttpSessionSecurityContextRepository;
import org.springframework.test.web.servlet.MockMvc;

// 게임 로그인과 어드민 로그인은 같은 세션 쿠키를 쓰되 세션의 다른 칸에 있다. 실제 세션 칸에 로그인 정보를 넣어 서로 통하지 않는지 확인한다.
@SecuredWebMvcTest(controllers = {AdminLoginTestController.class, LoginUserTestController.class})
class AdminSecurityConfigTest {

  @Autowired private MockMvc mockMvc;

  @Test
  @DisplayName("어드민으로 로그인한 세션은 어드민 화면과 관리 API에서 어드민 아이디를 받는다")
  void adminSession_reachesAdminPagesAndApi() throws Exception {
    MockHttpSession session = adminSession();

    mockMvc
        .perform(get("/admin/test").session(session))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.loginId").value("operator"));
    mockMvc
        .perform(get("/api/v1/admin/test/me").session(session))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.loginId").value("operator"));
  }

  @Test
  @DisplayName("로그인하지 않고 어드민 화면을 열면 어드민 로그인 화면으로 보낸다")
  void unauthenticatedAdminPage_redirectsToAdminLogin() throws Exception {
    mockMvc.perform(get("/admin/test")).andExpect(redirectedUrl("/admin/login"));
  }

  @Test
  @DisplayName("끝에 / 없는 어드민 첫 주소(/admin)도 어드민 보안 설정이 맡아 로그인 화면으로 보낸다")
  void unauthenticatedAdminRoot_redirectsToAdminLogin() throws Exception {
    mockMvc.perform(get("/admin")).andExpect(redirectedUrl("/admin/login"));
  }

  @Test
  @DisplayName("로그인하지 않고 관리 API를 부르면 401 UNAUTHENTICATED다")
  void unauthenticatedAdminApi_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(get("/api/v1/admin/test/me"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  @Test
  @DisplayName("Google로 게임에 로그인한 세션은 어드민 화면·관리 API에 들어가지 못한다")
  void gameSession_cannotReachAdmin() throws Exception {
    MockHttpSession session = gameSession();

    mockMvc.perform(get("/admin/test").session(session)).andExpect(redirectedUrl("/admin/login"));
    mockMvc
        .perform(get("/api/v1/admin/test/me").session(session))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  @Test
  @DisplayName("어드민으로 로그인한 세션은 게임 API를 쓰지 못한다")
  void adminSession_cannotReachGameApi() throws Exception {
    mockMvc
        .perform(get("/api/v1/test/me").session(adminSession()))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  private static MockHttpSession adminSession() {
    MockHttpSession session = new MockHttpSession();
    session.setAttribute(
        AdminLoginSession.SESSION_ATTRIBUTE,
        new SecurityContextImpl(
            UsernamePasswordAuthenticationToken.authenticated(
                new AdminLogin("operator"), null, List.of())));
    return session;
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
