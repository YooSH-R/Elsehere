package com.elsehere.mission.controller;

import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.mission.service.ShowcaseQuestService;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

@SecuredWebMvcTest(controllers = ShowcaseQuestController.class)
class ShowcaseQuestControllerTest {
  @Autowired private MockMvc mvc;
  @MockitoBean private ShowcaseQuestService service;

  @Test
  void anonymousCannotReadOrStartDemo() throws Exception {
    mvc.perform(get("/api/v1/missions/showcase")).andExpect(status().isUnauthorized());
    mvc.perform(
            post("/api/v1/missions/showcase/start").header("X-Requested-With", "ElsehereClient"))
        .andExpect(status().isUnauthorized());
    verifyNoInteractions(service);
  }

  @Test
  void demoWritesRequireCsrfAndUseSessionUser() throws Exception {
    var auth =
        UsernamePasswordAuthenticationToken.authenticated(new LoginUser(8L), null, List.of());
    mvc.perform(post("/api/v1/missions/showcase/start").with(authentication(auth)))
        .andExpect(status().isForbidden());
    mvc.perform(
            post("/api/v1/missions/showcase/start")
                .with(authentication(auth))
                .header("X-Requested-With", "ElsehereClient"))
        .andExpect(status().isOk());
    verify(service).start(8L);
  }

  @Test
  void taxiArrivalRejectsBlankDestinationAndIgnoresClientUserId() throws Exception {
    var auth =
        UsernamePasswordAuthenticationToken.authenticated(new LoginUser(8L), null, List.of());
    mvc.perform(
            post("/api/v1/missions/showcase/taxi-arrival")
                .with(authentication(auth))
                .header("X-Requested-With", "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"destination\":\"\"}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
    mvc.perform(
            post("/api/v1/missions/showcase/taxi-arrival")
                .with(authentication(auth))
                .header("X-Requested-With", "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"destination\":\"DINER\",\"userId\":999}"))
        .andExpect(status().isOk());
    verify(service).taxiArrival(8L, "DINER");
  }
}
