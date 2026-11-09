package com.elsehere.auth.controller;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyBoolean;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.ai.service.GuestConversationLimit;
import com.elsehere.auth.service.GuestLoginService;
import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUserTestController;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.user.dto.UserResponse;
import com.elsehere.user.entity.AvatarType;
import com.elsehere.user.entity.OnboardingStep;
import com.elsehere.user.entity.UiTheme;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.mock.web.MockHttpSession;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;

// 게스트 사용자를 만드는 규칙은 GuestLoginServiceTest에서 DB와 함께 검증하고, 여기서는 요청·응답과 세션 처리만 본다.
@SecuredWebMvcTest(controllers = {GuestLoginController.class, LoginUserTestController.class})
class GuestLoginControllerTest {

  private static final UserResponse GUEST_USER =
      new UserResponse(
          3L,
          "guest-123456",
          AvatarType.M,
          "en-US",
          OnboardingStep.TUTORIAL,
          UiTheme.JELLY,
          false,
          true);

  @Autowired private MockMvc mockMvc;

  @MockitoBean private GuestLoginService guestLoginService;

  @MockitoBean private GuestConversationLimit guestConversationLimit;

  @Test
  @DisplayName("로그인하지 않은 상태에서 캐릭터를 골라 게스트로 시작하면 201과 만든 사용자 정보로 응답한다")
  void startAsGuest_respondsCreatedWithUser() throws Exception {
    given(guestLoginService.startAsGuest(AvatarType.M, false)).willReturn(GUEST_USER);

    mockMvc
        .perform(guestLogin("{\"avatarType\": \"M\"}"))
        .andExpect(status().isCreated())
        .andExpect(jsonPath("$.user.userId").value(3))
        .andExpect(jsonPath("$.user.nickname").value("guest-123456"))
        .andExpect(jsonPath("$.user.avatarType").value("M"))
        .andExpect(jsonPath("$.user.onboardingStep").value("TUTORIAL"))
        .andExpect(jsonPath("$.user.voiceConsent").value(false))
        .andExpect(jsonPath("$.user.guest").value(true));
  }

  @Test
  @DisplayName("음성 처리에 동의하고 게스트로 시작하면 동의를 함께 넘기고 음성 입력을 쓸 수 있다고 응답한다")
  void startAsGuest_withVoiceConsent_passesConsent() throws Exception {
    given(guestLoginService.startAsGuest(AvatarType.M, true))
        .willReturn(
            new UserResponse(
                3L,
                "guest-123456",
                AvatarType.M,
                "en-US",
                OnboardingStep.TUTORIAL,
                UiTheme.JELLY,
                true,
                true));

    mockMvc
        .perform(guestLogin("{\"avatarType\": \"M\", \"voiceConsent\": true}"))
        .andExpect(status().isCreated())
        .andExpect(jsonPath("$.user.voiceConsent").value(true));
  }

  @Test
  @DisplayName("캐릭터를 고르지 않으면 VALIDATION_FAILED로 거절하고 사용자를 만들지 않는다")
  void startAsGuest_withoutAvatar_respondsValidationFailed() throws Exception {
    mockMvc
        .perform(guestLogin("{}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));

    then(guestLoginService).should(never()).startAsGuest(any(), anyBoolean());
  }

  @Test
  @DisplayName("게스트로 시작하면 같은 세션의 다음 요청에서 그 사용자로 인식된다")
  void startAsGuest_keepsUserInSession() throws Exception {
    given(guestLoginService.startAsGuest(AvatarType.M, false)).willReturn(GUEST_USER);
    MockHttpSession session = new MockHttpSession();

    mockMvc
        .perform(guestLogin("{\"avatarType\": \"M\"}").session(session))
        .andExpect(status().isCreated());

    mockMvc
        .perform(get("/api/v1/test/me").session(session))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.userId").value(3));
  }

  @Test
  @DisplayName("게스트 세션은 가입자보다 짧게, 2시간 동안 요청이 없으면 끝나도록 둔다")
  void startAsGuest_shortensSessionTimeout() throws Exception {
    given(guestLoginService.startAsGuest(AvatarType.M, false)).willReturn(GUEST_USER);
    MockHttpSession session = new MockHttpSession();

    mockMvc
        .perform(guestLogin("{\"avatarType\": \"M\"}").session(session))
        .andExpect(status().isCreated());

    assertThat(session.getMaxInactiveInterval()).isEqualTo(2 * 60 * 60);
  }

  @Test
  @DisplayName("로그인하지 않은 상태에서 게스트 안내를 조회하면 게스트가 시작할 수 있는 NPC 대화 수로 응답한다")
  void getGuestGuide_withoutLogin_respondsConversationLimit() throws Exception {
    given(guestConversationLimit.guestLimit()).willReturn(3);

    mockMvc
        .perform(get("/api/v1/auth/guest"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.conversationLimit").value(3));
  }

  private static MockHttpServletRequestBuilder guestLogin(String body) {
    return post("/api/v1/auth/guest")
        .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
        .contentType(MediaType.APPLICATION_JSON)
        .content(body);
  }
}
