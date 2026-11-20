package com.elsehere.mission.controller;

import static org.mockito.Mockito.doThrow;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.mission.dto.QuestBoardResponse;
import com.elsehere.mission.service.QuestBoardService;
import com.elsehere.mission.service.QuestCancelService;
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
  @MockitoBean private QuestCancelService cancels;

  private final UsernamePasswordAuthenticationToken auth =
      UsernamePasswordAuthenticationToken.authenticated(new LoginUser(8L), null, List.of());

  @Test
  void signedInUserReadsTheirQuestBoard() throws Exception {
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

  // 퀘스트를 그만두면 처음으로 되돌린 뒤의 퀘스트 목록을 돌려준다.
  @Test
  void cancellingAQuestReturnsTheUpdatedBoard() throws Exception {
    when(service.board(8L))
        .thenReturn(
            new QuestBoardResponse(
                100,
                List.of(
                    new QuestBoardResponse.Quest(
                        "CHECK_IN", "호텔 체크인과 잃어버린 가방", "체크인해요.", 10, "AVAILABLE", List.of()))));

    mvc.perform(
            post("/api/v1/missions/quests/CHECK_IN/cancel")
                .header("X-Requested-With", "ElsehereClient")
                .with(authentication(auth)))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.quests[0].status").value("AVAILABLE"));
    verify(cancels).cancel(8L, "CHECK_IN");
  }

  @Test
  void cancellingAQuestThatIsNotInProgressIsAConflict() throws Exception {
    doThrow(new BusinessException(ErrorCode.QUEST_NOT_IN_PROGRESS))
        .when(cancels)
        .cancel(8L, "CHECK_IN");

    mvc.perform(
            post("/api/v1/missions/quests/CHECK_IN/cancel")
                .header("X-Requested-With", "ElsehereClient")
                .with(authentication(auth)))
        .andExpect(status().isConflict())
        .andExpect(jsonPath("$.error.code").value("QUEST_NOT_IN_PROGRESS"));
  }
}
