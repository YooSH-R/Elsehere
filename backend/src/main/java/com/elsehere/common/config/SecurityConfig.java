package com.elsehere.common.config;

import com.elsehere.auth.handler.LoginRequiredEntryPoint;
import com.elsehere.auth.handler.SocialLoginFailureHandler;
import com.elsehere.auth.handler.SocialLoginSuccessHandler;
import com.elsehere.common.auth.CsrfHeaderFilter;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.http.HttpMethod;
import org.springframework.http.HttpStatus;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.config.annotation.web.configurers.AbstractHttpConfigurer;
import org.springframework.security.web.SecurityFilterChain;
import org.springframework.security.web.authentication.logout.HttpStatusReturningLogoutSuccessHandler;
import org.springframework.security.web.csrf.CsrfFilter;
import org.springframework.security.web.servlet.util.matcher.PathPatternRequestMatcher;
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

  @Bean
  public SecurityFilterChain securityFilterChain(
      HttpSecurity http,
      @Qualifier("handlerExceptionResolver") HandlerExceptionResolver exceptionResolver,
      SocialLoginSuccessHandler socialLoginSuccessHandler,
      SocialLoginFailureHandler socialLoginFailureHandler,
      LoginRequiredEntryPoint loginRequiredEntryPoint)
      throws Exception {
    http.httpBasic(AbstractHttpConfigurer::disable)
        .formLogin(AbstractHttpConfigurer::disable)
        // CSRF는 Spring의 토큰 방식 대신 사용자 정의 헤더 검사로 막는다(CsrfHeaderFilter).
        .csrf(AbstractHttpConfigurer::disable)
        .addFilterAt(new CsrfHeaderFilter(exceptionResolver), CsrfFilter.class)
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
            exceptions -> exceptions.authenticationEntryPoint(loginRequiredEntryPoint));
    return http.build();
  }
}
