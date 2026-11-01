package com.elsehere.common.logging;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.common.auth.LoginUser;
import java.util.List;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Test;
import org.slf4j.MDC;
import org.springframework.mock.web.MockHttpServletRequest;
import org.springframework.mock.web.MockHttpServletResponse;
import org.springframework.security.authentication.AnonymousAuthenticationToken;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.authority.AuthorityUtils;
import org.springframework.security.core.context.SecurityContextHolder;

class UserLoggingFilterTest {

  private final UserLoggingFilter filter = new UserLoggingFilter();

  @AfterEach
  void clear() {
    SecurityContextHolder.clearContext();
    MDC.clear();
  }

  @Test
  void loggedInRequest_putsUserIdForEveryLogLine() throws Exception {
    SecurityContextHolder.getContext()
        .setAuthentication(
            new UsernamePasswordAuthenticationToken(new LoginUser(7L), null, List.of()));
    String[] userIdDuringRequest = new String[1];

    filter.doFilter(
        new MockHttpServletRequest("GET", "/api/v1/vocab"),
        new MockHttpServletResponse(),
        (request, response) -> userIdDuringRequest[0] = MDC.get("userId"));

    assertThat(userIdDuringRequest[0]).isEqualTo("7");
  }

  @Test
  void requestBeforeLogin_hasNoUserId() throws Exception {
    SecurityContextHolder.getContext()
        .setAuthentication(
            new AnonymousAuthenticationToken(
                "key", "anonymousUser", AuthorityUtils.createAuthorityList("ROLE_ANONYMOUS")));
    String[] userIdDuringRequest = {"unset"};

    filter.doFilter(
        new MockHttpServletRequest("GET", "/api/v1/bootstrap"),
        new MockHttpServletResponse(),
        (request, response) -> userIdDuringRequest[0] = MDC.get("userId"));

    assertThat(userIdDuringRequest[0]).isNull();
  }
}
