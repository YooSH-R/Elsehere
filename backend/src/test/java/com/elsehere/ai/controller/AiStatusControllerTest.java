package com.elsehere.ai.controller;

import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.ai.dto.AiStatus;
import com.elsehere.ai.dto.AiStatusResponse;
import com.elsehere.ai.dto.AiStatusResponse.Capabilities;
import com.elsehere.ai.service.AiStatusService;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

// 상태 판단은 AiStatusServiceTest에서 DB와 함께 검증하고, 여기서는 응답 형식과 로그인 요구만 본다.
@SecuredWebMvcTest(controllers = AiStatusController.class)
class AiStatusControllerTest {

  @Autowired private MockMvc mockMvc;

  @MockitoBean private AiStatusService aiStatusService;

  @Test
  @DisplayName("로그인한 사용자의 AI 이용 상태와 기능별 가능 여부를 돌려준다")
  void loggedIn_getsStatusAndCapabilities() throws Exception {
    given(aiStatusService.status(1L))
        .willReturn(
            new AiStatusResponse(AiStatus.AI_EXHAUSTED, new Capabilities(false, true, true)));

    mockMvc
        .perform(get("/api/v1/ai/status").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.status").value("AI_EXHAUSTED"))
        .andExpect(jsonPath("$.capabilities.canStartConversation").value(false))
        .andExpect(jsonPath("$.capabilities.canUseSpeech").value(true))
        .andExpect(jsonPath("$.capabilities.canGenerateFeedback").value(true))
        .andExpect(jsonPath("$.quota").doesNotExist());
  }

  @Test
  @DisplayName("로그인하지 않았으면 401 UNAUTHENTICATED로 응답한다")
  void notLoggedIn_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(get("/api/v1/ai/status"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
