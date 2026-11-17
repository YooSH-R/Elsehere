package com.elsehere.mission.controller;

import static org.mockito.Mockito.when;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.mission.dto.FirstDayMissionResponse;
import com.elsehere.mission.service.FirstDayMissionService;
import java.util.List;
import java.util.Optional;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

@SecuredWebMvcTest(controllers = FirstDayMissionController.class)
class FirstDayMissionControllerTest {
  @Autowired private MockMvc mvc;
  @MockitoBean private FirstDayMissionService service;
  @MockitoBean private com.elsehere.mission.service.ShowcaseQuestService showcase;

  @Test
  void unauthenticatedUserCannotReadMission() throws Exception {
    mvc.perform(get("/api/v1/missions/first-day"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  @Test
  void writesRequireCsrfHeaderAndUseAuthenticatedUser() throws Exception {
    var auth =
        UsernamePasswordAuthenticationToken.authenticated(new LoginUser(8L), null, List.of());
    mvc.perform(post("/api/v1/missions/first-day/start").with(authentication(auth)))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("CSRF_HEADER_REQUIRED"));
    when(service.start(8L))
        .thenReturn(
            new FirstDayMissionResponse(
                true, 0, null, false, "HOME", false, false, "CLEAR", 20, null, "호텔로 가세요", "예약 확인",
                List.of()));
    mvc.perform(
            post("/api/v1/missions/first-day/start")
                .with(authentication(auth))
                .header("X-Requested-With", "ElsehereClient"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.started").value(true))
        .andExpect(jsonPath("$.stage").value(0))
        .andExpect(jsonPath("$.raining").value(false))
        .andExpect(jsonPath("$.weather").value("CLEAR"));
  }

  // 시연 서버에서는 시작 전에도 시연 퀘스트를 보여, 다른 퀘스트가 진행 중으로 보이지 않는다.
  @Test
  void showcaseServerShowsTheDemoInsteadOfOtherQuests() throws Exception {
    var auth =
        UsernamePasswordAuthenticationToken.authenticated(new LoginUser(8L), null, List.of());
    var demo =
        new FirstDayMissionResponse(
            false,
            0,
            "SHOWCASE_RAINY_EVENING",
            false,
            "HOTEL",
            false,
            true,
            "RAIN",
            100,
            null,
            "",
            "",
            List.of());
    when(showcase.shownStatus(8L)).thenReturn(Optional.of(demo));
    when(showcase.visit(8L, "HOTEL")).thenReturn(Optional.of(demo));

    mvc.perform(get("/api/v1/missions/first-day").with(authentication(auth)))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.quest").value("SHOWCASE_RAINY_EVENING"))
        .andExpect(jsonPath("$.started").value(false));
    mvc.perform(
            post("/api/v1/missions/first-day/visit")
                .with(authentication(auth))
                .header("X-Requested-With", "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"scene\":\"HOTEL\"}"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.quest").value("SHOWCASE_RAINY_EVENING"));
  }

  @Test
  void emptySceneIsValidationFailure() throws Exception {
    var auth =
        UsernamePasswordAuthenticationToken.authenticated(new LoginUser(8L), null, List.of());
    mvc.perform(
            post("/api/v1/missions/first-day/visit")
                .with(authentication(auth))
                .header("X-Requested-With", "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"scene\":\"\"}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }
}
