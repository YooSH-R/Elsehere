package com.elsehere.world.controller;

import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.world.service.PronunciationService;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

// 파일을 찾는 규칙은 PronunciationServiceTest에서 검증하고, 여기서는 경로와 응답 형식을 본다.
@SecuredWebMvcTest(controllers = PronunciationController.class)
class PronunciationControllerTest {

  private static final byte[] WAV = {'R', 'I', 'F', 'F', 0, 0, 0, 0, 'W', 'A', 'V', 'E'};

  @Autowired private MockMvc mockMvc;

  @MockitoBean private PronunciationService pronunciationService;

  @Test
  @DisplayName("GET /api/v1/vocab/{vocabId}/pronunciation은 발음 음성을 audio/wav로 돌려준다")
  void getPronunciation_respondsWav() throws Exception {
    given(pronunciationService.getPronunciation(31L)).willReturn(WAV);

    mockMvc
        .perform(get("/api/v1/vocab/31/pronunciation").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(content().contentType("audio/wav"))
        .andExpect(content().bytes(WAV));
  }

  @Test
  @DisplayName("발음 음성이 없는 단어면 404 NOT_FOUND 오류 응답을 돌려준다")
  void getPronunciation_withoutAudio_respondsNotFound() throws Exception {
    given(pronunciationService.getPronunciation(31L))
        .willThrow(new BusinessException(ErrorCode.NOT_FOUND));

    mockMvc
        .perform(get("/api/v1/vocab/31/pronunciation").with(authentication(loggedIn(1L))))
        .andExpect(status().isNotFound())
        .andExpect(jsonPath("$.error.code").value("NOT_FOUND"));
  }

  @Test
  @DisplayName("로그인하지 않으면 401 UNAUTHENTICATED로 응답한다")
  void notLoggedIn_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(get("/api/v1/vocab/31/pronunciation"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
