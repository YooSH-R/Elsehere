package com.elsehere.common.logging;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.LoginUserTestController;
import com.elsehere.common.config.SecuredWebMvcTest;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.system.CapturedOutput;
import org.springframework.boot.test.system.OutputCaptureExtension;
import org.springframework.test.web.servlet.MockMvc;

@SecuredWebMvcTest(controllers = LoginUserTestController.class)
@ExtendWith(OutputCaptureExtension.class)
class RequestLoggingMvcTest {

  @Autowired private MockMvc mockMvc;

  @Test
  void authenticationFailure_keepsRequestIdAndLogsSafeSummary(CapturedOutput output)
      throws Exception {
    String requestId = "1234567890abcdef1234567890abcdef";

    mockMvc
        .perform(get("/api/v1/test/me?token=private-token").header("X-Request-ID", requestId))
        .andExpect(status().isUnauthorized())
        .andExpect(header().string("X-Request-ID", requestId));

    assertThat(output.getOut())
        .contains("http_request method=GET route=/unmatched status=401")
        .doesNotContain("private-token");
  }
}
