package com.elsehere.admin.controller;

import static org.hamcrest.Matchers.containsString;
import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.redirectedUrl;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.admin.dto.AdminNpcDetail;
import com.elsehere.admin.dto.AdminNpcSummary;
import com.elsehere.admin.service.AdminAccess;
import com.elsehere.admin.service.AdminNpcService;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

@SecuredWebMvcTest(controllers = AdminPageController.class)
class AdminPageControllerTest {

  @Autowired private MockMvc mockMvc;

  @Autowired private AdminAccess adminAccess;

  @MockitoBean private AdminNpcService adminNpcService;

  @Test
  @DisplayName("로그인하지 않고 어드민 화면을 열면 Google 로그인으로 보낸다")
  void npcs_withoutLogin_redirectsToGoogleLogin() throws Exception {
    mockMvc
        .perform(get("/admin/npcs"))
        .andExpect(status().is3xxRedirection())
        .andExpect(redirectedUrl("/api/v1/auth/login/google"));
  }

  @Test
  @DisplayName("관리자가 아닌 사용자가 어드민 화면을 열면 403 ADMIN_REQUIRED다")
  void npcs_byUser_respondsAdminRequired() throws Exception {
    mockMvc
        .perform(get("/admin/npcs").with(authentication(loggedIn(2L))))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("ADMIN_REQUIRED"));
  }

  @Test
  @DisplayName("어드민 첫 화면은 NPC 목록으로 보낸다")
  void home_redirectsToNpcs() throws Exception {
    given(adminAccess.isAdmin(1L)).willReturn(true);

    mockMvc
        .perform(get("/admin").with(authentication(loggedIn(1L))))
        .andExpect(status().is3xxRedirection())
        .andExpect(redirectedUrl("/admin/npcs"));
  }

  @Test
  @DisplayName("관리자는 NPC 목록에서 이름·역할 이름·장소와 페르소나 화면 링크를 본다")
  void npcs_byAdmin_listsNpcs() throws Exception {
    given(adminAccess.isAdmin(1L)).willReturn(true);
    given(adminNpcService.npcs())
        .willReturn(List.of(new AdminNpcSummary("CAFE_STAFF", "Mia", "카페 직원", "모퉁이 카페")));

    mockMvc
        .perform(get("/admin/npcs").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(content().string(containsString("Mia")))
        .andExpect(content().string(containsString("카페 직원")))
        .andExpect(content().string(containsString("모퉁이 카페")))
        .andExpect(content().string(containsString("href=\"/admin/npcs/CAFE_STAFF\"")));
  }

  @Test
  @DisplayName("관리자는 NPC 페르소나 화면에서 지금 값과 저장할 API 주소를 본다")
  void npc_byAdmin_showsCurrentPersona() throws Exception {
    given(adminAccess.isAdmin(1L)).willReturn(true);
    given(adminNpcService.npc("CAFE_STAFF"))
        .willReturn(
            new AdminNpcDetail(
                "CAFE_STAFF",
                "Mia",
                "카페 직원",
                "모퉁이 카페",
                "af_heart",
                "the barista at the corner cafe.",
                "Soy milk is free.",
                "The counter of the corner cafe."));

    mockMvc
        .perform(get("/admin/npcs/CAFE_STAFF").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(content().string(containsString("the barista at the corner cafe.")))
        .andExpect(content().string(containsString("Soy milk is free.")))
        .andExpect(content().string(containsString("The counter of the corner cafe.")))
        .andExpect(content().string(containsString("/api/v1/admin/npcs/CAFE_STAFF/persona")));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
