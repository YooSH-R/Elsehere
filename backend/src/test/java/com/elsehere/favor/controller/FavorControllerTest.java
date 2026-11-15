package com.elsehere.favor.controller;

import static org.mockito.Mockito.when;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.favor.dto.FavorCallRequest;
import com.elsehere.favor.dto.FavorResponse;
import com.elsehere.favor.service.FavorService;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

// 돌발 부탁 조회(FAV-01)와 그만두기(FAV-02)
@SecuredWebMvcTest(controllers = FavorController.class)
class FavorControllerTest {

  private static final FavorResponse CROISSANT =
      new FavorResponse(
          42L,
          "bring_food",
          "ACCEPTED",
          "HOTEL_STAFF",
          "Jake의 부탁 · 크루아상 사다 주기",
          List.of(
              new FavorResponse.StepResponse("BAKERY", null, "베이커리에서 크루아상 포장 주문하기", false),
              new FavorResponse.StepResponse("HOTEL", "HOTEL_STAFF", "Jake에게 돌아가기", false)),
          6,
          8,
          null,
          4380L);

  @Autowired private MockMvc mvc;
  @MockitoBean private FavorService service;

  @Test
  @DisplayName("진행 중 부탁을 카드에 보일 모양으로 돌려준다")
  void currentFavor() throws Exception {
    when(service.current(8L)).thenReturn(Optional.of(CROISSANT));

    mvc.perform(get("/api/v1/favors/current").with(authentication(signedIn())))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.favorId").value(42))
        .andExpect(jsonPath("$.titleKo").value("Jake의 부탁 · 크루아상 사다 주기"))
        .andExpect(jsonPath("$.steps[0].placeCode").value("BAKERY"))
        .andExpect(jsonPath("$.expiresAtGameMinute").value(4380));
  }

  @Test
  @DisplayName("진행 중 부탁이 없으면 본문 없이 204")
  void noCurrentFavor() throws Exception {
    when(service.current(8L)).thenReturn(Optional.empty());

    mvc.perform(get("/api/v1/favors/current").with(authentication(signedIn())))
        .andExpect(status().isNoContent());
  }

  @Test
  @DisplayName("그만두면 끝난 부탁을 돌려준다")
  void abandonFavor() throws Exception {
    FavorResponse abandoned =
        new FavorResponse(
            42L,
            "bring_food",
            "ABANDONED",
            "HOTEL_STAFF",
            CROISSANT.titleKo(),
            CROISSANT.steps(),
            6,
            8,
            null,
            4380L);
    when(service.abandon(8L, 42L)).thenReturn(abandoned);

    mvc.perform(
            post("/api/v1/favors/42/abandon")
                .header("X-Requested-With", "XMLHttpRequest")
                .with(authentication(signedIn())))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.status").value("ABANDONED"));
  }

  @Test
  @DisplayName("근처 NPC를 보내 말을 걸게 할 부탁이 있으면 돌려주고, 욕구 칸의 모르는 키는 버린다")
  void callAFavor() throws Exception {
    FavorResponse calling =
        new FavorResponse(
            42L,
            "bring_food",
            "OFFERED",
            "HOTEL_STAFF",
            CROISSANT.titleKo(),
            CROISSANT.steps(),
            6,
            8,
            null,
            null);
    when(service.call(
            8L, List.of(new FavorCallRequest.Candidate("HOTEL_STAFF", Map.of("HUNGER", 20.0)))))
        .thenReturn(Optional.of(calling));

    mvc.perform(
            post("/api/v1/favors/calls")
                .header("X-Requested-With", "XMLHttpRequest")
                .contentType(MediaType.APPLICATION_JSON)
                .content(
                    "{\"candidates\":[{\"npcCode\":\"HOTEL_STAFF\",\"needs\":{\"HUNGER\":20,\"MOOD\":5}}]}")
                .with(authentication(signedIn())))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.requesterNpcCode").value("HOTEL_STAFF"))
        .andExpect(jsonPath("$.status").value("OFFERED"));
  }

  @Test
  @DisplayName("말을 걸 때가 아니면 본문 없이 204")
  void noCallNow() throws Exception {
    when(service.call(8L, List.of())).thenReturn(Optional.empty());

    mvc.perform(
            post("/api/v1/favors/calls")
                .header("X-Requested-With", "XMLHttpRequest")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"candidates\":[]}")
                .with(authentication(signedIn())))
        .andExpect(status().isNoContent());
  }

  @Test
  @DisplayName("말을 걸어 온 NPC의 제안을 거절하면 끝난 부탁을 돌려준다")
  void declineACall() throws Exception {
    FavorResponse declined =
        new FavorResponse(
            42L,
            "bring_food",
            "DECLINED",
            "HOTEL_STAFF",
            CROISSANT.titleKo(),
            CROISSANT.steps(),
            6,
            8,
            null,
            null);
    when(service.decline(8L, 42L)).thenReturn(declined);

    mvc.perform(
            post("/api/v1/favors/42/decline")
                .header("X-Requested-With", "XMLHttpRequest")
                .with(authentication(signedIn())))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.status").value("DECLINED"));
  }

  private static UsernamePasswordAuthenticationToken signedIn() {
    return UsernamePasswordAuthenticationToken.authenticated(new LoginUser(8L), null, List.of());
  }
}
