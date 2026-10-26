package com.elsehere.common.health;

import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.config.SecuredWebMvcTest;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.health.actuate.endpoint.HealthDescriptor;
import org.springframework.boot.health.actuate.endpoint.HealthEndpoint;
import org.springframework.boot.health.actuate.endpoint.IndicatedHealthDescriptor;
import org.springframework.boot.health.contributor.Status;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

@SecuredWebMvcTest(controllers = ApplicationHealthController.class)
class ApplicationHealthControllerTest {

  @Autowired private MockMvc mockMvc;

  @MockitoBean private HealthEndpoint healthEndpoint;

  @Test
  void unhealthyApplicationReturnsServiceUnavailable() throws Exception {
    HealthDescriptor health = mock(IndicatedHealthDescriptor.class);
    when(health.getStatus()).thenReturn(Status.DOWN);
    when(healthEndpoint.health()).thenReturn(health);

    mockMvc
        .perform(get("/api/health"))
        .andExpect(status().isServiceUnavailable())
        .andExpect(jsonPath("$.status").value("DOWN"));
  }

  @Test
  void healthSubpathsStillRequireAuthentication() throws Exception {
    mockMvc.perform(get("/api/health/private")).andExpect(status().isUnauthorized());
  }
}
