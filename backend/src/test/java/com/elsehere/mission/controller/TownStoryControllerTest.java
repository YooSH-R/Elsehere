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
import com.elsehere.mission.entity.TownStory;
import com.elsehere.mission.service.TownStoryService;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

@SecuredWebMvcTest(controllers = TownStoryController.class)
class TownStoryControllerTest {
  @Test
  void mealReceiptRequiresLoginAndCsrfHeader() throws Exception {
    mvc.perform(
            post("/api/v1/missions/town-story/diner-meal")
                .header("X-Requested-With", "ElsehereClient"))
        .andExpect(status().isUnauthorized());
    var auth =
        UsernamePasswordAuthenticationToken.authenticated(new LoginUser(8L), null, List.of());
    mvc.perform(post("/api/v1/missions/town-story/diner-meal").with(authentication(auth)))
        .andExpect(status().isForbidden());
    mvc.perform(
            post("/api/v1/missions/town-story/diner-meal")
                .with(authentication(auth))
                .header("X-Requested-With", "ElsehereClient"))
        .andExpect(status().isOk());
    org.mockito.Mockito.verify(service).receiveDinerMeal(8L);
  }

  @Autowired private MockMvc mvc;
  @MockitoBean private TownStoryService service;

  @Test
  void authenticatedUserCanReadTheirStoryProgress() throws Exception {
    var auth =
        UsernamePasswordAuthenticationToken.authenticated(new LoginUser(8L), null, List.of());
    when(service.status(8L))
        .thenReturn(
            new FirstDayMissionResponse(
                true, 1, null, false, "TOWN", false, false, "CLEAR", 0, null, "빨래방", "세탁 문의",
                List.of()));
    mvc.perform(get("/api/v1/missions/town-story").with(authentication(auth)))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.stage").value(1));
  }

  @Test
  void startRequiresRequestHeaderAndReturnsProgress() throws Exception {
    var auth =
        UsernamePasswordAuthenticationToken.authenticated(new LoginUser(8L), null, List.of());
    mvc.perform(post("/api/v1/missions/town-story/start").with(authentication(auth)))
        .andExpect(status().isForbidden());
    when(service.start(8L)).thenReturn(TownStory.start(8L));
    when(service.status(8L))
        .thenReturn(
            new FirstDayMissionResponse(
                true, 0, null, false, "TOWN", false, false, "CLEAR", 0, null, "다이너", "주문",
                List.of()));
    mvc.perform(
            post("/api/v1/missions/town-story/start")
                .with(authentication(auth))
                .header("X-Requested-With", "ElsehereClient"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.started").value(true));
  }
}
