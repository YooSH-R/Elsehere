package com.elsehere.world.controller;

import static com.elsehere.vocab.entity.VocabularyType.WORD;
import static org.hamcrest.Matchers.nullValue;
import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.world.dto.NpcResponse;
import com.elsehere.world.dto.ObjectCardResponse;
import com.elsehere.world.dto.ObjectVocabResponse;
import com.elsehere.world.dto.SceneDetailResponse;
import com.elsehere.world.dto.SceneSummaryResponse;
import com.elsehere.world.entity.NpcType;
import com.elsehere.world.service.SceneService;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

// 조회 내용은 SceneServiceTest에서 DB와 함께 검증하고, 여기서는 경로와 응답 필드 이름을 본다.
@SecuredWebMvcTest(controllers = SceneController.class)
class SceneControllerTest {

  @Autowired private MockMvc mockMvc;

  @MockitoBean private SceneService sceneService;

  @Test
  @DisplayName("GET /api/v1/scenes는 장소 목록을 명세의 필드 이름으로 돌려준다")
  void getScenes_respondsSceneList() throws Exception {
    given(sceneService.getScenes())
        .willReturn(List.of(new SceneSummaryResponse("HOTEL", "호텔", "HotelScene", false)));

    mockMvc
        .perform(get("/api/v1/scenes").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$[0].sceneCode").value("HOTEL"))
        .andExpect(jsonPath("$[0].name").value("호텔"))
        .andExpect(jsonPath("$[0].unityKey").value("HotelScene"))
        .andExpect(jsonPath("$[0].isOpen").value(false));
  }

  @Test
  @DisplayName("GET /api/v1/scenes/{sceneCode}는 로그인한 사용자 기준의 NPC·사물 카드를 명세의 필드 이름으로 돌려준다")
  void getScene_respondsNpcsAndObjectCards() throws Exception {
    given(sceneService.getScene("CAFE", 1L))
        .willReturn(
            new SceneDetailResponse(
                "CAFE",
                "모퉁이 카페",
                "CafeScene",
                true,
                List.of(new NpcResponse("CAFE_STAFF", "Mia", NpcType.STAFF, "카페 직원", false, null)),
                List.of(
                    new ObjectCardResponse(
                        "menu",
                        new ObjectVocabResponse(
                            31L,
                            WORD,
                            "menu",
                            "메뉴",
                            "Can I see the menu, please?",
                            "메뉴 좀 볼 수 있을까요?"),
                        true,
                        true,
                        false))));

    mockMvc
        .perform(get("/api/v1/scenes/CAFE").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.sceneCode").value("CAFE"))
        .andExpect(jsonPath("$.name").value("모퉁이 카페"))
        .andExpect(jsonPath("$.unityKey").value("CafeScene"))
        .andExpect(jsonPath("$.isOpen").value(true))
        .andExpect(jsonPath("$.npcs[0].npcCode").value("CAFE_STAFF"))
        .andExpect(jsonPath("$.npcs[0].name").value("Mia"))
        .andExpect(jsonPath("$.npcs[0].npcType").value("STAFF"))
        .andExpect(jsonPath("$.npcs[0].roleLabel").value("카페 직원"))
        .andExpect(jsonPath("$.npcs[0].canInitiate").value(false))
        .andExpect(jsonPath("$.npcs[0].approachLine").value(nullValue()))
        .andExpect(jsonPath("$.objects[0].objectKey").value("menu"))
        .andExpect(jsonPath("$.objects[0].vocab.vocabId").value(31))
        .andExpect(jsonPath("$.objects[0].vocab.type").value("WORD"))
        .andExpect(jsonPath("$.objects[0].vocab.text").value("menu"))
        .andExpect(jsonPath("$.objects[0].vocab.meaningKo").value("메뉴"))
        .andExpect(jsonPath("$.objects[0].vocab.example").value("Can I see the menu, please?"))
        .andExpect(jsonPath("$.objects[0].vocab.exampleKo").value("메뉴 좀 볼 수 있을까요?"))
        .andExpect(jsonPath("$.objects[0].hasPronunciation").value(true))
        .andExpect(jsonPath("$.objects[0].saved").value(true))
        .andExpect(jsonPath("$.objects[0].hoverHidden").value(false));
  }

  @Test
  @DisplayName("없는 장소 코드면 404 NOT_FOUND 오류 응답을 돌려준다")
  void getScene_unknownScene_respondsNotFound() throws Exception {
    given(sceneService.getScene("MOON", 1L)).willThrow(new BusinessException(ErrorCode.NOT_FOUND));

    mockMvc
        .perform(get("/api/v1/scenes/MOON").with(authentication(loggedIn(1L))))
        .andExpect(status().isNotFound())
        .andExpect(jsonPath("$.error.code").value("NOT_FOUND"));
  }

  @Test
  @DisplayName("로그인하지 않으면 장소 목록과 상세 모두 401 UNAUTHENTICATED로 응답한다")
  void notLoggedIn_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(get("/api/v1/scenes"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
    mockMvc
        .perform(get("/api/v1/scenes/HOME"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
