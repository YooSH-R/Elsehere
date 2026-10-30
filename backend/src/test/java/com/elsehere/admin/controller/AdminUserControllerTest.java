package com.elsehere.admin.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.BDDMockito.willThrow;
import static org.mockito.Mockito.never;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.admin.dto.AdminUser;
import com.elsehere.admin.service.AdminAccess;
import com.elsehere.admin.service.AdminUserService;
import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

// 지정·해제 규칙은 AdminUserServiceTest에서 검증하고, 여기서는 관리자 권한·요청 검증·응답 연결만 본다.
@SecuredWebMvcTest(controllers = AdminUserController.class)
class AdminUserControllerTest {

  private static final String ADMINS_URL = "/api/v1/admin/admins";

  @Autowired private MockMvc mockMvc;

  @Autowired private AdminAccess adminAccess;

  @MockitoBean private AdminUserService adminUserService;

  @Test
  @DisplayName("관리자는 Google 이메일로 관리자를 추가하고 추가된 사람을 돌려받는다")
  void grant_byAdmin_respondsGrantedAdmin() throws Exception {
    given(adminAccess.isAdmin(1L)).willReturn(true);
    given(adminUserService.grant(1L, "moon@example.com"))
        .willReturn(new AdminUser(2L, "Moon", "moon@example.com"));

    mockMvc
        .perform(
            post(ADMINS_URL)
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereAdmin")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"email\": \"moon@example.com\"}")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.userId").value(2))
        .andExpect(jsonPath("$.nickname").value("Moon"))
        .andExpect(jsonPath("$.email").value("moon@example.com"));
  }

  @ParameterizedTest
  @ValueSource(strings = {"", "moon", "moon@"})
  @DisplayName("이메일 형식이 아니면 400 VALIDATION_FAILED로 거절하고 추가하지 않는다")
  void grant_withInvalidEmail_respondsValidationFailed(String email) throws Exception {
    given(adminAccess.isAdmin(1L)).willReturn(true);

    mockMvc
        .perform(
            post(ADMINS_URL)
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereAdmin")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"email\": \"" + email + "\"}")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
    then(adminUserService).should(never()).grant(any(), any());
  }

  @Test
  @DisplayName("가입하지 않은 이메일이면 404 ADMIN_CANDIDATE_NOT_FOUND와 이유를 돌려준다")
  void grant_unknownEmail_respondsCandidateNotFound() throws Exception {
    given(adminAccess.isAdmin(1L)).willReturn(true);
    given(adminUserService.grant(1L, "nobody@example.com"))
        .willThrow(new BusinessException(ErrorCode.ADMIN_CANDIDATE_NOT_FOUND));

    mockMvc
        .perform(
            post(ADMINS_URL)
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereAdmin")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"email\": \"nobody@example.com\"}")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isNotFound())
        .andExpect(jsonPath("$.error.code").value("ADMIN_CANDIDATE_NOT_FOUND"))
        .andExpect(
            jsonPath("$.error.message").value(ErrorCode.ADMIN_CANDIDATE_NOT_FOUND.getMessage()));
  }

  @Test
  @DisplayName("관리자가 아닌 사용자는 403 ADMIN_REQUIRED로 거절하고 추가하지 않는다")
  void grant_byUser_respondsAdminRequired() throws Exception {
    given(adminAccess.isAdmin(2L)).willReturn(false);

    mockMvc
        .perform(
            post(ADMINS_URL)
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereAdmin")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"email\": \"user@example.com\"}")
                .with(authentication(loggedIn(2L))))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("ADMIN_REQUIRED"));
    then(adminUserService).should(never()).grant(any(), any());
  }

  @Test
  @DisplayName("관리자는 다른 관리자를 해제하고 204를 받는다")
  void revoke_byAdmin_respondsNoContent() throws Exception {
    given(adminAccess.isAdmin(1L)).willReturn(true);

    mockMvc
        .perform(
            delete(ADMINS_URL + "/2")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereAdmin")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isNoContent());
    then(adminUserService).should().revoke(1L, 2L);
  }

  @Test
  @DisplayName("자기 자신을 해제하면 409 ADMIN_SELF_REVOKE를 돌려준다")
  void revoke_self_respondsSelfRevoke() throws Exception {
    given(adminAccess.isAdmin(1L)).willReturn(true);
    willThrow(new BusinessException(ErrorCode.ADMIN_SELF_REVOKE))
        .given(adminUserService)
        .revoke(1L, 1L);

    mockMvc
        .perform(
            delete(ADMINS_URL + "/1")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereAdmin")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isConflict())
        .andExpect(jsonPath("$.error.code").value("ADMIN_SELF_REVOKE"));
  }

  @Test
  @DisplayName("로그인하지 않으면 401 UNAUTHENTICATED로 거절하고 해제하지 않는다")
  void revoke_withoutLogin_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(delete(ADMINS_URL + "/2").header(CsrfHeaderFilter.HEADER_NAME, "ElsehereAdmin"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
    then(adminUserService).should(never()).revoke(any(), any());
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
