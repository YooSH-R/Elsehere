package com.elsehere.auth.controller;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.config.SecuredWebMvcTest;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;

// local 프로필을 켜지 않은 환경(개발 서버·운영)을 흉내 낸다.
@SecuredWebMvcTest(controllers = DevLoginController.class)
class DevLoginControllerProfileTest {

  @Autowired private MockMvc mockMvc;

  @Test
  @DisplayName("local 프로필이 아니면 개발용 로그인 API가 없어 404 NOT_FOUND로 응답한다")
  void devLoginOutsideLocalProfile_respondsNotFound() throws Exception {
    mockMvc
        .perform(
            post("/api/v1/dev/login")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"userId\": 7}"))
        .andExpect(status().isNotFound())
        .andExpect(jsonPath("$.error.code").value("NOT_FOUND"));
  }
}
