package com.elsehere;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;

@SpringBootTest
@AutoConfigureMockMvc
@Import(TestcontainersConfiguration.class)
class HealthEndpointTest {

  @Autowired private MockMvc mockMvc;

  @Test
  void healthyApplicationAndDatabaseAreVisibleWithoutAuthentication() throws Exception {
    mockMvc
        .perform(get("/api/health").accept(MediaType.APPLICATION_JSON))
        .andExpect(status().isOk())
        .andExpect(content().json("{\"status\":\"UP\"}"))
        .andExpect(jsonPath("$.groups").doesNotExist())
        .andExpect(jsonPath("$.components").doesNotExist());
  }

  @Test
  void otherApiRequestsStillRequireAuthentication() throws Exception {
    mockMvc
        .perform(get("/api/private-resource").accept(MediaType.APPLICATION_JSON))
        .andExpect(status().isUnauthorized());
  }
}
