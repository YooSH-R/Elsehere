package com.elsehere.mission.controller;

import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.mission.dto.PracticePlaceListResponse;
import com.elsehere.mission.dto.PracticeProgressResponse;
import com.elsehere.mission.service.PlacePracticeService;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

@SecuredWebMvcTest(controllers = PlacePracticeController.class)
class PlacePracticeControllerTest {
  @Autowired private MockMvc mvc;
  @MockitoBean private PlacePracticeService service;

  private static final UsernamePasswordAuthenticationToken USER =
      UsernamePasswordAuthenticationToken.authenticated(new LoginUser(8L), null, List.of());

  @Test
  void signedInUserReadsTheirPracticePlaces() throws Exception {
    given(service.places(8L))
        .willReturn(
            new PracticePlaceListResponse(
                List.of(
                    new PracticePlaceListResponse.Place(
                        "DINER",
                        "식당",
                        "자리 잡기 · 못 먹는 재료 말하기",
                        "DINER_STAFF",
                        "Rosa",
                        "식당 종업원",
                        "TOWN",
                        1,
                        3,
                        List.of(
                            new PracticePlaceListResponse.Scenario(
                                "DINER_TABLE",
                                1,
                                "자리 잡기",
                                "몇 명인지 말하고 창가 자리를 부탁해요.",
                                true,
                                List.of(
                                    new PracticePlaceListResponse.Goal(
                                        "say_party_window", "인원 말하고 창가 자리 부탁하기", "힌트"))))))));

    mvc.perform(get("/api/v1/practice-places").with(authentication(USER)))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.places[0].placeCode").value("DINER"))
        .andExpect(jsonPath("$.places[0].npcName").value("Rosa"))
        .andExpect(jsonPath("$.places[0].doneCount").value(1))
        .andExpect(jsonPath("$.places[0].totalCount").value(3))
        .andExpect(jsonPath("$.places[0].scenarios[0].scenarioId").value("DINER_TABLE"))
        .andExpect(jsonPath("$.places[0].scenarios[0].done").value(true))
        .andExpect(jsonPath("$.places[0].scenarios[0].goals[0].goalId").value("say_party_window"))
        .andExpect(jsonPath("$.places[0].scenarios[0].goals[0].hint").value("힌트"));
  }

  @Test
  void progressRouteUsesTheAuthenticatedOwner() throws Exception {
    given(service.progress(8L, "DINER_TABLE", 77L))
        .willReturn(
            new PracticeProgressResponse(
                "DINER_TABLE",
                List.of(
                    new PracticeProgressResponse.Goal(
                        "say_party_window", "인원 말하고 창가 자리 부탁하기", "힌트", true)),
                false));

    mvc.perform(
            get("/api/v1/practice-scenarios/DINER_TABLE/conversations/77/progress")
                .with(authentication(USER)))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.scenarioId").value("DINER_TABLE"))
        .andExpect(jsonPath("$.goals[0].completed").value(true))
        .andExpect(jsonPath("$.done").value(false));
  }
}
