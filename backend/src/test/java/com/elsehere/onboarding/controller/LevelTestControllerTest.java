package com.elsehere.onboarding.controller;

import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.onboarding.dto.LevelTestItemResponse;
import com.elsehere.onboarding.dto.LevelTestResultResponse;
import com.elsehere.onboarding.dto.LevelTestStartResponse;
import com.elsehere.onboarding.entity.LevelTestStatus;
import com.elsehere.onboarding.service.LevelTestCompletionService;
import com.elsehere.onboarding.service.LevelTestService;
import com.elsehere.onboarding.service.LevelTestSpeechService;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;

// 레벨 테스트 규칙은 LevelTestServiceTest에서 DB와 함께 검증하고, 여기서는 요청·응답 연결만 본다.
@SecuredWebMvcTest(controllers = LevelTestController.class)
class LevelTestControllerTest {

  @Autowired private MockMvc mockMvc;

  @MockitoBean private LevelTestService levelTestService;

  @MockitoBean private LevelTestCompletionService levelTestCompletionService;

  @MockitoBean private LevelTestSpeechService levelTestSpeechService;

  @Test
  @DisplayName("레벨 테스트 시작(ONB-03)은 201과 정답을 뺀 문항을 돌려준다")
  void start_respondsCreatedWithItemsWithoutAnswer() throws Exception {
    given(levelTestService.start(1L, "en-US"))
        .willReturn(
            new LevelTestStartResponse(
                9L,
                LevelTestStatus.IN_PROGRESS,
                1,
                List.of(
                    new LevelTestItemResponse(
                        "EN_LT_L2",
                        LevelTestItemResponse.Type.LISTEN,
                        "잘 듣고 무슨 뜻인지 고르세요.",
                        "무슨 뜻일까요?",
                        List.of("왼쪽 기계는 고장이다", "창가 기계는 고장이다", "세탁물을 4분의 1만 넣는다", "25분 뒤에 쓸 수 있다"),
                        "Gus · 빨래방 주인",
                        List.of()))));

    mockMvc
        .perform(
            post("/api/v1/level-tests")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"languageCode\": \"en-US\"}")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isCreated())
        .andExpect(jsonPath("$.testId").value(9))
        .andExpect(jsonPath("$.status").value("IN_PROGRESS"))
        .andExpect(jsonPath("$.totalItems").value(1))
        .andExpect(jsonPath("$.items[0].itemCode").value("EN_LT_L2"))
        .andExpect(jsonPath("$.items[0].type").value("LISTEN"))
        .andExpect(jsonPath("$.items[0].speaker").value("Gus · 빨래방 주인"))
        .andExpect(jsonPath("$.items[0].choices[0]").value("왼쪽 기계는 고장이다"))
        .andExpect(jsonPath("$.items[0].answer").doesNotExist())
        .andExpect(jsonPath("$.items[0].script").doesNotExist());
  }

  @Test
  @DisplayName("문항 응답(ONB-04)은 경로의 테스트·문항과 본문의 답으로 저장하고 204로 응답한다")
  void answer_savesAndRespondsNoContent() throws Exception {
    mockMvc
        .perform(
            put("/api/v1/level-tests/9/answers/EN_LT_L1")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"answer\": \"have\"}")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isNoContent());

    then(levelTestService).should().answer(1L, 9L, "EN_LT_L1", "have");
  }

  @Test
  @DisplayName("레벨 테스트 완료(ONB-05)는 상태·점수·결과 레벨과 듣기 점수·말하기 통과 수·한 줄 피드백을 돌려준다")
  void complete_returnsScoreAndResultLevel() throws Exception {
    given(levelTestCompletionService.complete(1L, 9L))
        .willReturn(new LevelTestResultResponse(9L, LevelTestStatus.DONE, 5, 2, 3, 2, "잘했어요."));

    mockMvc
        .perform(postWithLogin("/api/v1/level-tests/9/complete"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.testId").value(9))
        .andExpect(jsonPath("$.status").value("DONE"))
        .andExpect(jsonPath("$.score").value(5))
        .andExpect(jsonPath("$.resultLevel").value(2))
        .andExpect(jsonPath("$.listeningScore").value(3))
        .andExpect(jsonPath("$.speakingPassed").value(2))
        .andExpect(jsonPath("$.feedback").value("잘했어요."));
  }

  @Test
  @DisplayName("듣기 문항 음성은 WAV로 돌려준다")
  void speech_returnsWav() throws Exception {
    given(levelTestSpeechService.speech(1L, "EN_LT_L2")).willReturn(new byte[] {1, 2, 3});

    mockMvc
        .perform(
            get("/api/v1/level-tests/items/EN_LT_L2/speech").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(content().contentType("audio/wav"))
        .andExpect(content().bytes(new byte[] {1, 2, 3}));
  }

  @Test
  @DisplayName("그만하기(ONB-06)는 점수 없이 테스트 ID와 상태만 돌려준다")
  void quit_returnsStatusWithoutScore() throws Exception {
    given(levelTestService.quit(1L, 9L))
        .willReturn(LevelTestResultResponse.ended(9L, LevelTestStatus.QUIT));

    mockMvc
        .perform(postWithLogin("/api/v1/level-tests/9/quit"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.testId").value(9))
        .andExpect(jsonPath("$.status").value("QUIT"))
        .andExpect(jsonPath("$.score").doesNotExist())
        .andExpect(jsonPath("$.resultLevel").doesNotExist());
  }

  @Test
  @DisplayName("건너뛰기(ONB-07)는 테스트 ID 없이 본문의 언어로 건너뛴 기록을 만들고 201로 응답한다")
  void skip_respondsCreatedWithSkippedTest() throws Exception {
    given(levelTestService.skip(1L, "en-US"))
        .willReturn(LevelTestResultResponse.ended(10L, LevelTestStatus.SKIPPED));

    mockMvc
        .perform(
            postWithLogin("/api/v1/level-tests/skip")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"languageCode\": \"en-US\"}"))
        .andExpect(status().isCreated())
        .andExpect(jsonPath("$.testId").value(10))
        .andExpect(jsonPath("$.status").value("SKIPPED"));
  }

  @Test
  @DisplayName("시작·건너뛰기 요청에 학습 언어가 없으면 400 VALIDATION_FAILED로 응답한다")
  void requestWithoutLanguage_isRejected() throws Exception {
    mockMvc
        .perform(
            postWithLogin("/api/v1/level-tests")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }

  @Test
  @DisplayName("문항 응답에 답이 없으면 400 VALIDATION_FAILED로 응답한다")
  void answerWithoutAnswer_isRejected() throws Exception {
    mockMvc
        .perform(
            put("/api/v1/level-tests/9/answers/EN_LT_L1")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"answer\": \"\"}")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }

  private static MockHttpServletRequestBuilder postWithLogin(String path) {
    return post(path)
        .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
        .with(authentication(loggedIn(1L)));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
