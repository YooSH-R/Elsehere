package com.elsehere.admin.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.admin.dto.PersonaRequest;
import com.elsehere.admin.dto.PersonaResponse;
import com.elsehere.admin.service.AdminLogin;
import com.elsehere.admin.service.AdminNpcService;
import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

// 페르소나 저장 형식은 AdminNpcServiceTest에서 검증하고, 여기서는 관리자 권한·요청 검증·응답 연결만 본다.
@SecuredWebMvcTest(controllers = AdminNpcController.class)
class AdminNpcControllerTest {

  private static final String PERSONA_URL = "/api/v1/admin/npcs/CAFE_STAFF/persona";
  private static final String PERSONA_BODY =
      """
      {"role": "the barista at the corner cafe.", "facts": "Soy milk is free.", "scene": "The counter."}
      """;

  @Autowired private MockMvc mockMvc;

  @MockitoBean private AdminNpcService adminNpcService;

  @Test
  @DisplayName("관리자는 NPC 페르소나를 고치고 고친 값을 돌려받는다")
  void updatePersona_byAdmin_respondsUpdatedPersona() throws Exception {
    given(
            adminNpcService.updatePersona(
                "CAFE_STAFF",
                new PersonaRequest(
                    "the barista at the corner cafe.", "Soy milk is free.", "The counter.")))
        .willReturn(
            new PersonaResponse(
                "CAFE_STAFF",
                "the barista at the corner cafe.",
                "Soy milk is free.",
                "The counter."));

    mockMvc
        .perform(
            put(PERSONA_URL)
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereAdmin")
                .contentType(MediaType.APPLICATION_JSON)
                .content(PERSONA_BODY)
                .with(authentication(adminLogin())))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.npcCode").value("CAFE_STAFF"))
        .andExpect(jsonPath("$.role").value("the barista at the corner cafe."))
        .andExpect(jsonPath("$.facts").value("Soy milk is free."))
        .andExpect(jsonPath("$.scene").value("The counter."));
  }

  @Test
  @DisplayName("로그인하지 않으면 401 UNAUTHENTICATED로 거절하고 고치지 않는다")
  void updatePersona_withoutLogin_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(
            put(PERSONA_URL)
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereAdmin")
                .contentType(MediaType.APPLICATION_JSON)
                .content(PERSONA_BODY))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
    then(adminNpcService).should(never()).updatePersona(any(), any());
  }

  @Test
  @DisplayName("게임 사용자로 로그인했어도 401 UNAUTHENTICATED로 거절하고 고치지 않는다")
  void updatePersona_byGameUser_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(
            put(PERSONA_URL)
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereAdmin")
                .contentType(MediaType.APPLICATION_JSON)
                .content(PERSONA_BODY)
                .with(authentication(gameLogin())))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
    then(adminNpcService).should(never()).updatePersona(any(), any());
  }

  @Test
  @DisplayName("요청 확인 헤더가 없으면 관리자여도 403 CSRF_HEADER_REQUIRED로 거절한다")
  void updatePersona_withoutCsrfHeader_respondsCsrfHeaderRequired() throws Exception {
    mockMvc
        .perform(
            put(PERSONA_URL)
                .contentType(MediaType.APPLICATION_JSON)
                .content(PERSONA_BODY)
                .with(authentication(adminLogin())))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("CSRF_HEADER_REQUIRED"));
    then(adminNpcService).should(never()).updatePersona(any(), any());
  }

  @Test
  @DisplayName("역할(role)이 비어 있으면 400 VALIDATION_FAILED로 거절하고 고치지 않는다")
  void updatePersona_withBlankRole_respondsValidationFailed() throws Exception {
    mockMvc
        .perform(
            put(PERSONA_URL)
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereAdmin")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"role\": \" \", \"facts\": \"\", \"scene\": \"\"}")
                .with(authentication(adminLogin())))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
    then(adminNpcService).should(never()).updatePersona(any(), any());
  }

  private static Authentication adminLogin() {
    return UsernamePasswordAuthenticationToken.authenticated(
        new AdminLogin("operator"), null, List.of());
  }

  private static Authentication gameLogin() {
    return UsernamePasswordAuthenticationToken.authenticated(new LoginUser(2L), null, List.of());
  }
}
