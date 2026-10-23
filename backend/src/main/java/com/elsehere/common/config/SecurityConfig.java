package com.elsehere.common.config;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import org.springframework.beans.factory.annotation.Qualifier;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.http.HttpMethod;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.config.annotation.web.configurers.AbstractHttpConfigurer;
import org.springframework.security.web.SecurityFilterChain;
import org.springframework.security.web.csrf.CsrfFilter;
import org.springframework.web.servlet.HandlerExceptionResolver;

/**
 * 세션 기반 인증 설정. 로그인 상태는 서버 세션에 두고 클라이언트는 세션 쿠키만 보낸다.
 *
 * <p>보안 필터에서 생긴 오류도 {@link HandlerExceptionResolver}로 넘겨 컨트롤러 오류와 같은 공통 오류 응답을 쓴다.
 */
@Configuration(proxyBeanMethods = false)
public class SecurityConfig {

  @Bean
  public SecurityFilterChain securityFilterChain(
      HttpSecurity http,
      @Qualifier("handlerExceptionResolver") HandlerExceptionResolver exceptionResolver)
      throws Exception {
    http.httpBasic(AbstractHttpConfigurer::disable)
        .formLogin(AbstractHttpConfigurer::disable)
        // CSRF는 Spring의 토큰 방식 대신 사용자 정의 헤더 검사로 막는다(CsrfHeaderFilter).
        .csrf(AbstractHttpConfigurer::disable)
        .addFilterAt(new CsrfHeaderFilter(exceptionResolver), CsrfFilter.class)
        .authorizeHttpRequests(
            authorize ->
                authorize
                    // 개발용 로그인. 컨트롤러가 local 프로필에만 있어 다른 환경에서는 404다.
                    .requestMatchers(HttpMethod.POST, "/api/v1/dev/login")
                    .permitAll()
                    .anyRequest()
                    .authenticated())
        .exceptionHandling(
            exceptions ->
                exceptions.authenticationEntryPoint(
                    (request, response, authenticationException) ->
                        exceptionResolver.resolveException(
                            request,
                            response,
                            null,
                            new BusinessException(ErrorCode.UNAUTHENTICATED))));
    return http.build();
  }
}
