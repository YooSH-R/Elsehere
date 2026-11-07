package com.elsehere.onboarding.controller;

import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.onboarding.dto.TutorialResponse;
import com.elsehere.onboarding.service.TutorialService;
import com.elsehere.user.entity.OnboardingStep;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;

// 단계 규칙은 TutorialServiceTest에서 DB와 함께 검증하고, 여기서는 요청·응답 연결만 본다.
@SecuredWebMvcTest(controllers = TutorialController.class)
class TutorialControllerTest {

  @Autowired private MockMvc mockMvc;

  @MockitoBean private TutorialService tutorialService;

  @Test
  void completeBenDialogue_respondsWithoutFinishingTheTutorial() throws Exception {
    given(tutorialService.completeBenDialogue(1L))
        .willReturn(new TutorialResponse(OnboardingStep.TUTORIAL));
    mockMvc
        .perform(tutorialRequest("/api/v1/me/tutorial/ben-dialogue/complete"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.onboardingStep").value("TUTORIAL"));
  }

  @Test
  @DisplayName("튜토리얼 완료(TUT-01)는 로그인한 사용자로 처리하고 초기 설정 단계를 돌려준다")
  void complete_respondsOnboardingStep() throws Exception {
    given(tutorialService.complete(1L)).willReturn(new TutorialResponse(OnboardingStep.DONE));

    mockMvc
        .perform(tutorialRequest("/api/v1/me/tutorial/complete"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.onboardingStep").value("DONE"));
  }

  @Test
  @DisplayName("튜토리얼 건너뛰기(TUT-02)는 로그인한 사용자로 처리하고 초기 설정 단계를 돌려준다")
  void skip_respondsOnboardingStep() throws Exception {
    given(tutorialService.skip(1L)).willReturn(new TutorialResponse(OnboardingStep.DONE));

    mockMvc
        .perform(tutorialRequest("/api/v1/me/tutorial/skip"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.onboardingStep").value("DONE"));
  }

  @Test
  @DisplayName("튜토리얼 단계가 아니면 완료·건너뛰기 모두 409 TUTORIAL_NOT_IN_PROGRESS로 응답한다")
  void notInTutorial_respondsTutorialNotInProgress() throws Exception {
    given(tutorialService.complete(1L))
        .willThrow(new BusinessException(ErrorCode.TUTORIAL_NOT_IN_PROGRESS));
    given(tutorialService.skip(1L))
        .willThrow(new BusinessException(ErrorCode.TUTORIAL_NOT_IN_PROGRESS));

    mockMvc
        .perform(tutorialRequest("/api/v1/me/tutorial/complete"))
        .andExpect(status().isConflict())
        .andExpect(jsonPath("$.error.code").value("TUTORIAL_NOT_IN_PROGRESS"));
    mockMvc
        .perform(tutorialRequest("/api/v1/me/tutorial/skip"))
        .andExpect(status().isConflict())
        .andExpect(jsonPath("$.error.code").value("TUTORIAL_NOT_IN_PROGRESS"));
  }

  @Test
  @DisplayName("로그인하지 않으면 완료·건너뛰기 모두 401 UNAUTHENTICATED로 응답한다")
  void notLoggedIn_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(
            post("/api/v1/me/tutorial/complete")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
    mockMvc
        .perform(
            post("/api/v1/me/tutorial/skip").header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  @Test
  @DisplayName("X-Requested-With 헤더가 없으면 완료·건너뛰기 모두 403 CSRF_HEADER_REQUIRED로 거절한다")
  void withoutCsrfHeader_isRejected() throws Exception {
    mockMvc
        .perform(post("/api/v1/me/tutorial/complete").with(authentication(loggedIn(1L))))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("CSRF_HEADER_REQUIRED"));
    mockMvc
        .perform(post("/api/v1/me/tutorial/skip").with(authentication(loggedIn(1L))))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("CSRF_HEADER_REQUIRED"));
  }

  private static MockHttpServletRequestBuilder tutorialRequest(String path) {
    return post(path)
        .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
        .with(authentication(loggedIn(1L)));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
