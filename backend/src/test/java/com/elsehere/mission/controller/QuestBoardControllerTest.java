package com.elsehere.mission.controller;

import static org.mockito.Mockito.when;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.mission.dto.QuestBoardResponse;
import com.elsehere.mission.service.QuestBoardService;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

@SecuredWebMvcTest(controllers = QuestBoardController.class)
class QuestBoardControllerTest {
  @Autowired private MockMvc mvc;
  @MockitoBean private QuestBoardService service;

  @Test
  void signedInUserReadsTheirQuestBoard() throws Exception {
    var auth =
        UsernamePasswordAuthenticationToken.authenticated(new LoginUser(8L), null, List.of());
    when(service.board(8L))
        .thenReturn(
            new QuestBoardResponse(
                22,
                List.of(
                    new QuestBoardResponse.Quest(
                        "CHECK_IN", "호텔 체크인과 잃어버린 가방", "체크인해요.", 10, "DONE", List.of()))));

    mvc.perform(get("/api/v1/missions/quests").with(authentication(auth)))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.balance").value(22))
        .andExpect(jsonPath("$.quests[0].status").value("DONE"));
  }
}
