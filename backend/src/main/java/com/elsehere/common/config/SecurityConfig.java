package com.elsehere.common.config;

import com.elsehere.admin.service.AdminAccess;
import com.elsehere.auth.handler.LoginRequiredEntryPoint;
import com.elsehere.auth.handler.SocialLoginFailureHandler;
import com.elsehere.auth.handler.SocialLoginSuccessHandler;
import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.common.logging.UserLoggingFilter;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.http.HttpMethod;
import org.springframework.http.HttpStatus;
import org.springframework.security.authorization.AuthorizationDecision;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.config.annotation.web.configurers.AbstractHttpConfigurer;
import org.springframework.security.core.Authentication;
import org.springframework.security.web.SecurityFilterChain;
import org.springframework.security.web.authentication.AnonymousAuthenticationFilter;
import org.springframework.security.web.authentication.logout.HttpStatusReturningLogoutSuccessHandler;
import org.springframework.security.web.csrf.CsrfFilter;
import org.springframework.security.web.servlet.util.matcher.PathPatternRequestMatcher;
import org.springframework.security.web.util.matcher.AnyRequestMatcher;
import org.springframework.web.servlet.HandlerExceptionResolver;

/**
 * 세션 기반 인증 설정. 로그인 상태는 서버 세션에 두고 클라이언트는 세션 쿠키만 보낸다.
 *
 * <p>보안 필터에서 생긴 오류도 {@link HandlerExceptionResolver}로 넘겨 컨트롤러 오류와 같은 공통 오류 응답을 쓴다.
 */
@Configuration(proxyBeanMethods = false)
public class SecurityConfig {

  /** AUTH-01. 뒤에 제공자 이름이 붙는다(예: /api/v1/auth/login/google). */
  private static final String SOCIAL_LOGIN_START_PATH = "/api/v1/auth/login";

  /** AUTH-02. 제공자 인증을 마치고 돌아오는 주소다(예: /api/v1/auth/callback/google). */
  private static final String SOCIAL_LOGIN_CALLBACK_PATH = "/api/v1/auth/callback/*";

  /** 운영자 어드민 화면. 백엔드가 Thymeleaf로 그린다(docs/admin.md). */
  private static final String ADMIN_PAGES = "/admin/**";

  /** 운영자 관리 API. */
  private static final String ADMIN_API = "/api/v1/admin/**";

  /** 가입 화면에서 여는 약관 문서(AUTH-03의 url). 백엔드가 그린다. */
  private static final String[] LEGAL_DOCUMENTS = {
    "/terms/*", "/privacy/*", "/voice/*", "/ai-training/*"
  };

  @Bean
  public SecurityFilterChain securityFilterChain(
      HttpSecurity http,
      @Qualifier("handlerExceptionResolver") HandlerExceptionResolver exceptionResolver,
      SocialLoginSuccessHandler socialLoginSuccessHandler,
      SocialLoginFailureHandler socialLoginFailureHandler,
      LoginRequiredEntryPoint loginRequiredEntryPoint,
      AdminAccess adminAccess)
      throws Exception {
    http.httpBasic(AbstractHttpConfigurer::disable)
        .formLogin(AbstractHttpConfigurer::disable)
        // CSRF는 Spring의 토큰 방식 대신 사용자 정의 헤더 검사로 막는다(CsrfHeaderFilter).
        .csrf(AbstractHttpConfigurer::disable)
        .addFilterAt(new CsrfHeaderFilter(exceptionResolver), CsrfFilter.class)
        // 세션에서 인증 정보를 읽은 뒤 로그인 사용자 ID를 로그 문맥에 넣는다.
        .addFilterAfter(new UserLoggingFilter(), AnonymousAuthenticationFilter.class)
        .oauth2Login(
            oauth2 ->
                oauth2
                    .authorizationEndpoint(endpoint -> endpoint.baseUri(SOCIAL_LOGIN_START_PATH))
                    .redirectionEndpoint(endpoint -> endpoint.baseUri(SOCIAL_LOGIN_CALLBACK_PATH))
                    .successHandler(socialLoginSuccessHandler)
                    .failureHandler(socialLoginFailureHandler))
        .authorizeHttpRequests(
            authorize ->
                authorize
                    // 관리 포트가 분리돼도 기존 컨테이너·프록시 상태 확인 경로는 유지한다.
                    .requestMatchers(HttpMethod.GET, "/api/health")
                    .permitAll()
                    // 개발용 로그인. 컨트롤러가 local 프로필에만 있어 다른 환경에서는 404다.
                    .requestMatchers(HttpMethod.POST, "/api/v1/dev/login")
                    .permitAll()
                    // 가입(AUTH-03·04)은 로그인 전 단계다. 가입 대기 여부는 컨트롤러가 확인한다.
                    .requestMatchers("/api/v1/auth/signup")
                    .permitAll()
                    // 게스트로 시작하기(AUTH-07)는 로그인 전에 부른다.
                    .requestMatchers(HttpMethod.POST, "/api/v1/auth/guest")
                    .permitAll()
                    // 접속 상태 조회(BOOT-01)는 가입 대기 사용자도 호출한다. 로그인 상태는 컨트롤러가 구분한다.
                    .requestMatchers(HttpMethod.GET, "/api/v1/bootstrap")
                    .permitAll()
                    // 닉네임 중복 확인(AUTH-06)은 가입 대기 사용자도 호출한다. 호출 자격은 컨트롤러가 확인한다.
                    .requestMatchers(HttpMethod.GET, "/api/v1/nicknames/availability")
                    .permitAll()
                    // 약관 문서는 가입하기 전에 읽는다.
                    .requestMatchers(HttpMethod.GET, LEGAL_DOCUMENTS)
                    .permitAll()
                    // 운영자 어드민 화면과 관리 API는 관리자만 쓴다. 역할은 요청마다 DB에서 확인한다(AdminAccess).
                    .requestMatchers(ADMIN_PAGES, ADMIN_API)
                    .access(
                        (authentication, request) ->
                            new AuthorizationDecision(isAdmin(authentication.get(), adminAccess)))
                    .anyRequest()
                    .authenticated())
        // AUTH-05. 세션을 끝내고 204로 응답한다. 가입 대기 중에 다른 계정으로 바꾸려는 사용자도 쓸 수 있도록 로그인 여부를 묻지 않는다.
        .logout(
            logout ->
                logout
                    .logoutRequestMatcher(
                        PathPatternRequestMatcher.withDefaults()
                            .matcher(HttpMethod.POST, "/api/v1/auth/logout"))
                    .logoutSuccessHandler(
                        new HttpStatusReturningLogoutSuccessHandler(HttpStatus.NO_CONTENT))
                    .deleteCookies("JSESSIONID"))
        .exceptionHandling(
            exceptions ->
                exceptions
                    // 어드민 화면은 브라우저 주소창으로 여는 페이지라 로그인하지 않았으면 Google 로그인으로 보낸다.
                    .defaultAuthenticationEntryPointFor(
                        (request, response, exception) ->
                            response.sendRedirect(SOCIAL_LOGIN_START_PATH + "/google"),
                        PathPatternRequestMatcher.withDefaults().matcher(ADMIN_PAGES))
                    .defaultAuthenticationEntryPointFor(
                        loginRequiredEntryPoint, AnyRequestMatcher.INSTANCE)
                    // 로그인했는데 권한이 없어 막히는 곳은 관리자 전용 경로뿐이다.
                    .accessDeniedHandler(
                        (request, response, exception) ->
                            exceptionResolver.resolveException(
                                request,
                                response,
                                null,
                                new BusinessException(ErrorCode.ADMIN_REQUIRED))));
    return http.build();
  }

  private static boolean isAdmin(Authentication authentication, AdminAccess adminAccess) {
    return authentication != null
        && authentication.getPrincipal() instanceof LoginUser loginUser
        && adminAccess.isAdmin(loginUser.userId());
  }
}
