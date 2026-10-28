package com.elsehere.feedback.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.feedback.dto.ModelSpeechResponse;
import com.elsehere.feedback.service.ModelSpeechService;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

// 모범 음성 생성·재사용 규칙은 ModelSpeechServiceTest에서 검증하고, 여기서는 요청·응답 연결만 본다.
@SecuredWebMvcTest(controllers = ModelSpeechController.class)
class ModelSpeechControllerTest {

  private static final UUID REQUEST_ID = UUID.fromString("00000000-0000-0000-0000-000000000001");
  private static final byte[] WAV = {82, 73, 70, 70};

  @Autowired private MockMvc mockMvc;

  @MockitoBean private ModelSpeechService modelSpeechService;

  @Test
  @DisplayName("POST /model-speech는 모범 음성을 받을 주소를 돌려준다")
  void create_respondsUrl() throws Exception {
    given(modelSpeechService.create(1L, 33L, REQUEST_ID)).willReturn(ModelSpeechResponse.of(33L));

    mockMvc
        .perform(
            post("/api/v1/turn-feedback/33/model-speech")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"requestId\": \"" + REQUEST_ID + "\"}")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.url").value("/api/v1/turn-feedback/33/model-speech"));
  }

  @Test
  @DisplayName("요청 ID가 없으면 VALIDATION_FAILED로 거절하고 음성을 만들지 않는다")
  void create_withoutRequestId_respondsValidationFailed() throws Exception {
    mockMvc
        .perform(
            post("/api/v1/turn-feedback/33/model-speech")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{}")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
    then(modelSpeechService).should(never()).create(any(), any(), any());
  }

  @Test
  @DisplayName("GET /model-speech는 저장한 모범 음성을 audio/wav로 돌려준다")
  void load_respondsWav() throws Exception {
    given(modelSpeechService.load(1L, 33L)).willReturn(WAV);

    mockMvc
        .perform(get("/api/v1/turn-feedback/33/model-speech").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(content().contentType("audio/wav"))
        .andExpect(content().bytes(WAV));
  }

  @Test
  @DisplayName("로그인하지 않았으면 401 UNAUTHENTICATED로 응답한다")
  void notLoggedIn_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(get("/api/v1/turn-feedback/33/model-speech"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
