package com.elsehere.memory.controller;

import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.memory.dto.NeighborListResponse;
import com.elsehere.memory.dto.NeighborResponse;
import com.elsehere.memory.service.NeighborService;
import java.time.OffsetDateTime;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

// 어떤 NPC가 열리고 어떤 순서로 오는지는 NeighborServiceTest에서 DB와 함께 검증하고, 여기서는 요청·응답 연결만 본다.
@SecuredWebMvcTest(controllers = NeighborController.class)
class NeighborControllerTest {

  private static final String PATH = "/api/v1/me/npcs";

  @Autowired private MockMvc mockMvc;

  @MockitoBean private NeighborService neighborService;

  @Test
  @DisplayName("열린 이웃은 정보와 관계를, 잠긴 NPC는 역할과 장소만 npcs 배열에 담고 이웃 수와 전체 수를 함께 돌려준다")
  void neighbors_respondsNeighborsAndLockedNpcs() throws Exception {
    given(neighborService.neighbors(7L))
        .willReturn(
            new NeighborListResponse(
                1,
                2,
                List.of(
                    new NeighborResponse(
                        "ARCADE_STAFF",
                        true,
                        "Max",
                        "오락실 직원",
                        "TOWN",
                        "마을",
                        "attendant at Little Arcade on the main street",
                        List.of("video games"),
                        12,
                        30,
                        3,
                        OffsetDateTime.parse("2026-11-09T14:12:00+09:00")),
                    new NeighborResponse(
                        "CAFE_STAFF",
                        false,
                        null,
                        "카페 직원",
                        "CAFE",
                        "모퉁이 카페",
                        null,
                        List.of(),
                        null,
                        null,
                        null,
                        null))));

    mockMvc
        .perform(get(PATH).with(authentication(loggedIn(7L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.neighborCount").value(1))
        .andExpect(jsonPath("$.totalCount").value(2))
        .andExpect(jsonPath("$.npcs.length()").value(2))
        .andExpect(jsonPath("$.npcs[0].npcCode").value("ARCADE_STAFF"))
        .andExpect(jsonPath("$.npcs[0].unlocked").value(true))
        .andExpect(jsonPath("$.npcs[0].name").value("Max"))
        .andExpect(jsonPath("$.npcs[0].roleLabel").value("오락실 직원"))
        .andExpect(jsonPath("$.npcs[0].sceneCode").value("TOWN"))
        .andExpect(jsonPath("$.npcs[0].sceneName").value("마을"))
        .andExpect(
            jsonPath("$.npcs[0].occupation").value("attendant at Little Arcade on the main street"))
        .andExpect(jsonPath("$.npcs[0].interests[0]").value("video games"))
        .andExpect(jsonPath("$.npcs[0].affinity").value(12))
        .andExpect(jsonPath("$.npcs[0].recognition").value(30))
        .andExpect(jsonPath("$.npcs[0].talkCount").value(3))
        .andExpect(jsonPath("$.npcs[0].lastTalkedAt").value("2026-11-09T14:12:00+09:00"))
        .andExpect(jsonPath("$.npcs[1].unlocked").value(false))
        .andExpect(jsonPath("$.npcs[1].name").isEmpty())
        .andExpect(jsonPath("$.npcs[1].roleLabel").value("카페 직원"))
        .andExpect(jsonPath("$.npcs[1].sceneName").value("모퉁이 카페"))
        .andExpect(jsonPath("$.npcs[1].interests").isEmpty())
        .andExpect(jsonPath("$.npcs[1].affinity").isEmpty());
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
