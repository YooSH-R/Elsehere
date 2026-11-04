package com.elsehere.followup.controller;

import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.conversation.dto.NpcSummaryResponse;
import com.elsehere.followup.dto.FollowUpResponse;
import com.elsehere.followup.service.FollowUpService;
import java.time.OffsetDateTime;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

// 판정·저장은 FollowUpServiceTest에서 DB와 함께 검증하고, 여기서는 요청·응답 연결만 본다.
@SecuredWebMvcTest(controllers = FollowUpController.class)
class FollowUpControllerTest {

  private static final String PATH = "/api/v1/follow-ups";

  @Autowired private MockMvc mockMvc;

  @MockitoBean private FollowUpService followUpService;

  @Test
  @DisplayName("GET은 로그인한 사용자가 아직 받지 않은 것을 준비 시각 순서로 돌려준다")
  void waiting_respondsItems() throws Exception {
    given(followUpService.waiting(1L))
        .willReturn(
            List.of(
                new FollowUpResponse(
                    7L,
                    new NpcSummaryResponse("CAFE_STAFF", "Mia", "카페 직원"),
                    "CAFE",
                    "iced latte",
                    "아이스 라테",
                    OffsetDateTime.parse("2026-11-04T14:05:45+09:00"),
                    45,
                    false)));

    mockMvc
        .perform(get(PATH).with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.items[0].followUpId").value(7))
        .andExpect(jsonPath("$.items[0].npc.npcCode").value("CAFE_STAFF"))
        .andExpect(jsonPath("$.items[0].sceneCode").value("CAFE"))
        .andExpect(jsonPath("$.items[0].item").value("iced latte"))
        .andExpect(jsonPath("$.items[0].itemKo").value("아이스 라테"))
        .andExpect(jsonPath("$.items[0].readyAt").value("2026-11-04T14:05:45+09:00"))
        .andExpect(jsonPath("$.items[0].remainingSeconds").value(45))
        .andExpect(jsonPath("$.items[0].ready").value(false));
  }

  @Test
  @DisplayName("로그인하지 않으면 401 UNAUTHENTICATED로 응답한다")
  void notLoggedIn_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(get(PATH))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
