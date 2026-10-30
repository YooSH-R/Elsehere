package com.elsehere.mission.controller;

import static org.hamcrest.Matchers.nullValue;
import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.mission.dto.MissionListResponse;
import com.elsehere.mission.dto.MissionResponse;
import com.elsehere.mission.entity.UserMissionStatus;
import com.elsehere.mission.service.MissionService;
import java.time.LocalDate;
import java.time.OffsetDateTime;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

// 미션 시작·날짜 규칙은 MissionServiceTest에서 DB와 함께 검증하고, 여기서는 경로와 응답 형식을 본다.
@SecuredWebMvcTest(controllers = MissionController.class)
class MissionControllerTest {

  @Autowired private MockMvc mockMvc;

  @MockitoBean private MissionService missionService;

  @Test
  @DisplayName("GET /api/v1/missions는 로그인한 사용자의 오늘 미션을 명세의 필드 이름과 날짜·시각 형식으로 돌려준다")
  void getTodayMissions_respondsMissionList() throws Exception {
    given(missionService.getTodayMissions(1L))
        .willReturn(
            new MissionListResponse(
                LocalDate.parse("2026-10-27"),
                OffsetDateTime.parse("2026-10-28T00:00:00+09:00"),
                List.of(
                    new MissionResponse(
                        3L,
                        "CAFE_SOY_LATTE",
                        "나만의 카페라테 주문하기",
                        "우유 대신 두유를 넣은 카페라테를 주문해 보세요.",
                        "CAFE",
                        "CAFE_STAFF",
                        "Mia",
                        12L,
                        UserMissionStatus.DONE,
                        OffsetDateTime.parse("2026-10-27T14:05:00+09:00")),
                    new MissionResponse(
                        4L,
                        "HOTEL_CHECK_IN",
                        "호텔에 체크인하기",
                        "호텔 직원에게 예약을 확인하고 체크인해 보세요.",
                        "HOTEL",
                        "HOTEL_STAFF",
                        "Jake",
                        13L,
                        UserMissionStatus.RUN,
                        null))));

    mockMvc
        .perform(get("/api/v1/missions").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.date").value("2026-10-27"))
        .andExpect(jsonPath("$.resetsAt").value("2026-10-28T00:00:00+09:00"))
        .andExpect(jsonPath("$.missions[0].missionId").value(3))
        .andExpect(jsonPath("$.missions[0].missionCode").value("CAFE_SOY_LATTE"))
        .andExpect(jsonPath("$.missions[0].title").value("나만의 카페라테 주문하기"))
        .andExpect(jsonPath("$.missions[0].description").value("우유 대신 두유를 넣은 카페라테를 주문해 보세요."))
        .andExpect(jsonPath("$.missions[0].sceneCode").value("CAFE"))
        .andExpect(jsonPath("$.missions[0].npcCode").value("CAFE_STAFF"))
        .andExpect(jsonPath("$.missions[0].npcName").value("Mia"))
        .andExpect(jsonPath("$.missions[0].userMissionId").value(12))
        .andExpect(jsonPath("$.missions[0].status").value("DONE"))
        .andExpect(jsonPath("$.missions[0].completedAt").value("2026-10-27T14:05:00+09:00"))
        .andExpect(jsonPath("$.missions[1].status").value("RUN"))
        .andExpect(jsonPath("$.missions[1].completedAt").value(nullValue()));
  }

  @Test
  @DisplayName("로그인하지 않으면 401 UNAUTHENTICATED로 응답한다")
  void notLoggedIn_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(get("/api/v1/missions"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
