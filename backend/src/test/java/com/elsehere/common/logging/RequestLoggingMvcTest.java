package com.elsehere.common.logging;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.auth.LoginUserTestController;
import com.elsehere.common.config.SecuredWebMvcTest;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.system.CapturedOutput;
import org.springframework.boot.test.system.OutputCaptureExtension;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.test.web.servlet.MockMvc;

@SecuredWebMvcTest(controllers = LoginUserTestController.class)
@ExtendWith(OutputCaptureExtension.class)
class RequestLoggingMvcTest {

  @Autowired private MockMvc mockMvc;

  @Test
  void loggedInRequest_summaryCarriesUserId(CapturedOutput output) throws Exception {
    mockMvc
        .perform(
            get("/api/v1/test/me")
                .with(
                    authentication(
                        UsernamePasswordAuthenticationToken.authenticated(
                            new LoginUser(7L), null, List.of()))))
        .andExpect(status().isOk());

    assertThat(output.getOut()).containsPattern("user_id=7\\].*http_request method=GET");
  }

  @Test
  void requestBeforeLogin_summaryHasNoUserId(CapturedOutput output) throws Exception {
    mockMvc.perform(get("/api/v1/test/me")).andExpect(status().isUnauthorized());

    assertThat(output.getOut()).containsPattern("user_id=none\\].*http_request method=GET");
  }

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
