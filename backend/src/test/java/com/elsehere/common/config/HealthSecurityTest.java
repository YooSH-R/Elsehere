package com.elsehere.common.config;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import java.util.Map;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.context.annotation.Import;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

@SecuredWebMvcTest(controllers = HealthSecurityTest.HealthProbe.class)
@Import(HealthSecurityTest.HealthProbe.class)
class HealthSecurityTest {

  @Autowired private MockMvc mockMvc;

  @Test
  void healthCanBeCheckedWithoutLogin() throws Exception {
    mockMvc.perform(get("/api/health")).andExpect(status().isOk());
  }

  @Test
  void healthSubpathsStillRequireLogin() throws Exception {
    mockMvc.perform(get("/api/health/private")).andExpect(status().isUnauthorized());
  }

  // The MVC slice isolates path authorization; HealthEndpointTest checks real DB health.
  @RestController
  static class HealthProbe {

    @GetMapping("/api/health")
    Map<String, String> health() {
      return Map.of("status", "UP");
    }
  }
}
