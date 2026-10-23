package com.elsehere.onboarding.controller;

import static org.hamcrest.Matchers.nullValue;
import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.onboarding.dto.LanguageProfileResponse;
import com.elsehere.onboarding.dto.LanguageSelectionResponse;
import com.elsehere.onboarding.dto.LevelSelectionResponse;
import com.elsehere.onboarding.service.ConversationLevel;
import com.elsehere.onboarding.service.LanguageCatalog;
import com.elsehere.onboarding.service.OnboardingService;
import com.elsehere.user.entity.OnboardingStep;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;

// 초기 설정 규칙은 OnboardingServiceTest에서 DB와 함께 검증하고, 여기서는 요청·응답 연결만 본다.
@SecuredWebMvcTest(controllers = OnboardingController.class)
@Import(LanguageCatalog.class)
class OnboardingControllerTest {

  @Autowired private MockMvc mockMvc;

  @MockitoBean private OnboardingService onboardingService;

  @Test
  @DisplayName("학습 언어 목록(ONB-01)을 돌려준다")
  void getLanguages_returnsLanguageCards() throws Exception {
    mockMvc
        .perform(get("/api/v1/languages").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$[0].languageCode").value("en-US"))
        .andExpect(jsonPath("$[0].name").value("English (US)"))
        .andExpect(jsonPath("$[0].available").value(true));
  }

  @Test
  @DisplayName("학습 언어 선택(ONB-02)은 로그인한 사용자와 경로의 언어로 처리하고 다음 초기 설정 단계를 돌려준다")
  void chooseLanguage_returnsNextStep() throws Exception {
    given(onboardingService.chooseLanguage(1L, "en-US"))
        .willReturn(new LanguageSelectionResponse("en-US", OnboardingStep.LEVEL));

    mockMvc
        .perform(
            put("/api/v1/me/languages/en-US")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.languageCode").value("en-US"))
        .andExpect(jsonPath("$.onboardingStep").value("LEVEL"));
  }

  @Test
  @DisplayName("학습 설정 조회(ONB-08)는 추천·선택 난이도와 난이도 단계 카드를 돌려준다")
  void getLanguageProfile_returnsLevels() throws Exception {
    given(onboardingService.getLanguageProfile(1L, "en-US"))
        .willReturn(
            new LanguageProfileResponse(
                "en-US",
                1,
                null,
                List.of(
                    new ConversationLevel(1, true, "천천히, 쉬운 단어와 짧은 문장으로 말해요."),
                    new ConversationLevel(2, false, null))));

    mockMvc
        .perform(get("/api/v1/me/languages/en-US").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.recommendedLevel").value(1))
        .andExpect(jsonPath("$.selectedLevel").value(nullValue()))
        .andExpect(jsonPath("$.levels[0].level").value(1))
        .andExpect(jsonPath("$.levels[0].available").value(true))
        .andExpect(jsonPath("$.levels[1].available").value(false))
        .andExpect(jsonPath("$.levels[1].description").value(nullValue()));
  }

  @Test
  @DisplayName("난이도 설정(ONB-09)은 본문의 난이도로 처리하고 다음 초기 설정 단계를 돌려준다")
  void selectLevel_returnsNextStep() throws Exception {
    given(onboardingService.selectLevel(1L, "en-US", 1))
        .willReturn(new LevelSelectionResponse("en-US", 1, OnboardingStep.TUTORIAL));

    mockMvc
        .perform(levelRequest("{\"selectedLevel\": 1}"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.selectedLevel").value(1))
        .andExpect(jsonPath("$.onboardingStep").value("TUTORIAL"));
  }

  @Test
  @DisplayName("난이도 설정 요청에 난이도가 없으면 400 VALIDATION_FAILED로 응답한다")
  void selectLevelWithoutLevel_isRejected() throws Exception {
    mockMvc
        .perform(levelRequest("{}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }

  private static MockHttpServletRequestBuilder levelRequest(String body) {
    return put("/api/v1/me/languages/en-US/level")
        .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
        .contentType(MediaType.APPLICATION_JSON)
        .content(body)
        .with(authentication(loggedIn(1L)));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
