package com.elsehere.world.controller;

import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.world.dto.LastSceneResponse;
import com.elsehere.world.service.LastSceneService;
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

// 저장 규칙은 LastSceneServiceTest에서 DB와 함께 검증하고, 여기서는 요청·응답 연결만 본다.
@SecuredWebMvcTest(controllers = LastSceneController.class)
class LastSceneControllerTest {

  @Autowired private MockMvc mockMvc;

  @MockitoBean private LastSceneService lastSceneService;

  @Test
  @DisplayName("PUT /api/v1/me/last-scene은 로그인한 사용자와 본문의 장소로 처리하고 저장한 장소 코드를 돌려준다")
  void saveLastScene_respondsSceneCode() throws Exception {
    given(lastSceneService.saveLastScene(1L, "CAFE")).willReturn(new LastSceneResponse("CAFE"));

    mockMvc
        .perform(lastSceneRequest("{\"sceneCode\": \"CAFE\"}"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.sceneCode").value("CAFE"));
  }

  @Test
  @DisplayName("요청 본문에 sceneCode가 없거나 비어 있으면 400 VALIDATION_FAILED로 응답한다")
  void saveLastSceneWithoutSceneCode_isRejected() throws Exception {
    mockMvc
        .perform(lastSceneRequest("{}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
    mockMvc
        .perform(lastSceneRequest("{\"sceneCode\": \" \"}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }

  @Test
  @DisplayName("없는 장소 코드면 404 NOT_FOUND 오류 응답을 돌려준다")
  void saveLastScene_unknownScene_respondsNotFound() throws Exception {
    given(lastSceneService.saveLastScene(1L, "MOON"))
        .willThrow(new BusinessException(ErrorCode.NOT_FOUND));

    mockMvc
        .perform(lastSceneRequest("{\"sceneCode\": \"MOON\"}"))
        .andExpect(status().isNotFound())
        .andExpect(jsonPath("$.error.code").value("NOT_FOUND"));
  }

  @Test
  @DisplayName("열리지 않은 장소면 409 SCENE_CLOSED 오류 응답을 돌려준다")
  void saveLastScene_closedScene_respondsSceneClosed() throws Exception {
    given(lastSceneService.saveLastScene(1L, "LIBRARY"))
        .willThrow(new BusinessException(ErrorCode.SCENE_CLOSED));

    mockMvc
        .perform(lastSceneRequest("{\"sceneCode\": \"LIBRARY\"}"))
        .andExpect(status().isConflict())
        .andExpect(jsonPath("$.error.code").value("SCENE_CLOSED"));
  }

  @Test
  @DisplayName("로그인하지 않으면 401 UNAUTHENTICATED로 응답한다")
  void notLoggedIn_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(
            put("/api/v1/me/last-scene")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"sceneCode\": \"CAFE\"}"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  @Test
  @DisplayName("X-Requested-With 헤더가 없으면 403 CSRF_HEADER_REQUIRED로 거절한다")
  void withoutCsrfHeader_isRejected() throws Exception {
    mockMvc
        .perform(
            put("/api/v1/me/last-scene")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"sceneCode\": \"CAFE\"}")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("CSRF_HEADER_REQUIRED"));
  }

  private static MockHttpServletRequestBuilder lastSceneRequest(String body) {
    return put("/api/v1/me/last-scene")
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
