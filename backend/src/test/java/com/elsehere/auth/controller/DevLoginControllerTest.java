package com.elsehere.auth.controller;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUserTestController;
import com.elsehere.common.config.SecuredWebMvcTest;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.test.context.ActiveProfiles;
import org.springframework.test.web.servlet.MockMvc;

@SecuredWebMvcTest(controllers = {DevLoginController.class, LoginUserTestController.class})
@ActiveProfiles("local")
class DevLoginControllerTest {

  @Autowired private MockMvc mockMvc;

  @Test
  @DisplayName("개발용 로그인을 하면 같은 세션의 다음 요청에서 그 사용자로 인식된다")
  void devLogin_keepsUserInSession() throws Exception {
    MockHttpSession session = new MockHttpSession();

    mockMvc
        .perform(
            post("/api/v1/dev/login")
                .session(session)
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"userId\": 7}"))
        .andExpect(status().isNoContent());

    mockMvc
        .perform(get("/api/v1/test/me").session(session))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.userId").value(7));
  }

  @Test
  @DisplayName("사용자 ID 없이 개발용 로그인을 요청하면 400 VALIDATION_FAILED로 응답한다")
  void devLoginWithoutUserId_respondsValidationFailed() throws Exception {
    mockMvc
        .perform(
            post("/api/v1/dev/login")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }
}
