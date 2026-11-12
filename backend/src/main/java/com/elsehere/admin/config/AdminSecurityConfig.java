package com.elsehere.admin.config;

import com.elsehere.admin.service.AdminLogin;
import com.elsehere.admin.service.AdminLoginSession;
import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.core.annotation.Order;
import org.springframework.http.HttpMethod;
import org.springframework.security.authorization.AuthorizationDecision;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.config.annotation.web.configurers.AbstractHttpConfigurer;
import org.springframework.security.web.AuthenticationEntryPoint;
import org.springframework.security.web.SecurityFilterChain;
import org.springframework.security.web.csrf.CsrfFilter;
import org.springframework.security.web.servlet.util.matcher.PathPatternRequestMatcher;
import org.springframework.security.web.util.matcher.RequestMatcher;
import org.springframework.web.servlet.HandlerExceptionResolver;

/**
 * 운영자 어드민(docs/admin.md) 보안 설정. 게임 보안 설정(SecurityConfig)보다 먼저 어드민 주소를 맡아, 어드민 아이디·비밀번호로 로그인한
 * 사람({@link AdminLogin})만 들인다. 로그인 상태는 세션의 어드민 칸에서만 읽으므로 Google 게임 로그인으로는 들어오지 못한다.
 */
@Configuration(proxyBeanMethods = false)
public class AdminSecurityConfig {

  /** 운영자 어드민 화면. 백엔드가 Thymeleaf로 그린다. */
  private static final String ADMIN_PAGES = "/admin/**";

  /** 운영자 관리 API */
  private static final String ADMIN_API = "/api/v1/admin/**";

  private static final String LOGIN_PAGE = "/admin/login";

  private static final RequestMatcher ADMIN_PAGE_MATCHER =
      PathPatternRequestMatcher.withDefaults().matcher(ADMIN_PAGES);

  @Bean
  @Order(2)
  public SecurityFilterChain adminSecurityFilterChain(
      HttpSecurity http,
      @Qualifier("handlerExceptionResolver") HandlerExceptionResolver exceptionResolver,
      AdminLoginSession adminLoginSession)
      throws Exception {
    // 어드민 화면은 브라우저 주소창으로 여는 페이지라 로그인 화면으로 보내고, 관리 API는 게임 API와 같은 401로 답한다.
    AuthenticationEntryPoint loginRequired =
        (request, response, exception) -> {
          if (ADMIN_PAGE_MATCHER.matches(request)) {
            response.sendRedirect(LOGIN_PAGE);
            return;
          }
          exceptionResolver.resolveException(
              request, response, null, new BusinessException(ErrorCode.UNAUTHENTICATED));
        };

    http.securityMatcher(ADMIN_PAGES, ADMIN_API)
        .httpBasic(AbstractHttpConfigurer::disable)
        .formLogin(AbstractHttpConfigurer::disable)
        .logout(AbstractHttpConfigurer::disable)
        .requestCache(AbstractHttpConfigurer::disable)
        // 게임과 같이 Spring의 토큰 방식 대신 사용자 정의 헤더 검사로 요청 위조를 막는다. 로그인 요청도 막아 로그인 위조를 막는다.
        .csrf(AbstractHttpConfigurer::disable)
        .addFilterAt(new CsrfHeaderFilter(exceptionResolver), CsrfFilter.class)
        .securityContext(
            context -> context.securityContextRepository(adminLoginSession.contextRepository()))
        .authorizeHttpRequests(
            authorize ->
                authorize
                    .requestMatchers(HttpMethod.GET, LOGIN_PAGE)
                    .permitAll()
                    // 로그아웃은 이미 끝난 세션의 화면에서 눌러도 오류 없이 끝나도록 로그인 여부를 묻지 않는다.
                    .requestMatchers(HttpMethod.POST, "/api/v1/admin/login", "/api/v1/admin/logout")
                    .permitAll()
                    .anyRequest()
                    .access(
                        (authentication, request) ->
                            new AuthorizationDecision(
                                authentication.get().getPrincipal() instanceof AdminLogin)))
        .exceptionHandling(
            exceptions ->
                exceptions
                    .authenticationEntryPoint(loginRequired)
                    // 어드민 칸에는 어드민 로그인만 저장하므로 로그인했는데 막히는 경우는 없다. 생기더라도 로그인하지 않은 것과 같이 다룬다.
                    .accessDeniedHandler(
                        (request, response, exception) ->
                            loginRequired.commence(request, response, null)));
    return http.build();
  }
}
