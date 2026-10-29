package com.elsehere.user.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyBoolean;
import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.Mockito.never;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.user.dto.TrainingConsentResponse;
import com.elsehere.user.dto.TrainingConsentsResponse;
import com.elsehere.user.entity.ConsentType;
import com.elsehere.user.service.TrainingConsentService;
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
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;

// 동의 규칙은 TrainingConsentServiceTest에서 DB와 함께 검증하고, 여기서는 요청 검증과 응답 형식만 본다.
@SecuredWebMvcTest(controllers = TrainingConsentController.class)
class TrainingConsentControllerTest {

  private static final String PATH = "/api/v1/me/training-consents";

  @Autowired private MockMvc mockMvc;

  @MockitoBean private TrainingConsentService trainingConsentService;

  @Test
  @DisplayName("로그인한 사용자의 학습 활용 동의 목록을 돌려준다")
  void findAll_respondsConsents() throws Exception {
    given(trainingConsentService.findAll(1L))
        .willReturn(
            new TrainingConsentsResponse(
                List.of(
                    consent(ConsentType.TRAINING_TEXT, true),
                    consent(ConsentType.TRAINING_VOICE, false))));

    mockMvc
        .perform(get(PATH).with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.consents[0].consentType").value("TRAINING_TEXT"))
        .andExpect(jsonPath("$.consents[0].agreed").value(true))
        .andExpect(jsonPath("$.consents[0].version").value("2026-10"))
        .andExpect(jsonPath("$.consents[1].consentType").value("TRAINING_VOICE"))
        .andExpect(jsonPath("$.consents[1].agreed").value(false));
  }

  @Test
  @DisplayName("로그인한 사용자의 동의를 바꾸고 바뀐 상태를 돌려준다")
  void change_respondsChangedConsent() throws Exception {
    given(trainingConsentService.change(1L, ConsentType.TRAINING_VOICE, false))
        .willReturn(consent(ConsentType.TRAINING_VOICE, false));

    mockMvc
        .perform(changeRequest("{\"consentType\": \"TRAINING_VOICE\", \"agreed\": false}"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.consentType").value("TRAINING_VOICE"))
        .andExpect(jsonPath("$.agreed").value(false));
  }

  @ParameterizedTest
  @ValueSource(
      strings = {
        "{}",
        "{\"consentType\": \"TRAINING_TEXT\"}",
        "{\"agreed\": true}",
        "{\"consentType\": \"TRAINING_TEXT\", \"agreed\": null}",
        "{\"consentType\": \"MARKETING\", \"agreed\": true}"
      })
  @DisplayName("동의 항목이나 동의 여부가 없거나 모르는 항목이면 바꾸지 않고 400 VALIDATION_FAILED로 응답한다")
  void missingOrUnknownValue_isRejected(String body) throws Exception {
    mockMvc
        .perform(changeRequest(body))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));

    then(trainingConsentService).should(never()).change(anyLong(), any(), anyBoolean());
  }

  @Test
  @DisplayName("로그인하지 않으면 401 UNAUTHENTICATED로 응답한다")
  void notLoggedIn_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(get(PATH))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  private static TrainingConsentResponse consent(ConsentType consentType, boolean agreed) {
    return new TrainingConsentResponse(
        consentType, "2026-10", "AI 개선 동의", "/ai-training/2026-10", agreed);
  }

  private static MockHttpServletRequestBuilder changeRequest(String body) {
    return put(PATH)
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
