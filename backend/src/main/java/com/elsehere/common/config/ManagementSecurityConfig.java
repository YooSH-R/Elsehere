package com.elsehere.common.config;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.security.autoconfigure.actuate.web.servlet.EndpointRequest;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.core.annotation.Order;
import org.springframework.http.HttpMethod;
import org.springframework.security.authentication.dao.DaoAuthenticationProvider;
import org.springframework.security.config.Customizer;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.config.http.SessionCreationPolicy;
import org.springframework.security.core.userdetails.User;
import org.springframework.security.crypto.bcrypt.BCryptPasswordEncoder;
import org.springframework.security.provisioning.InMemoryUserDetailsManager;
import org.springframework.security.web.SecurityFilterChain;

@Configuration(proxyBeanMethods = false)
public class ManagementSecurityConfig {

  @Bean
  @Order(1)
  public SecurityFilterChain managementSecurityFilterChain(
      HttpSecurity http, @Value("${monitoring.password:}") String password) throws Exception {
    BCryptPasswordEncoder passwordEncoder = new BCryptPasswordEncoder();
    InMemoryUserDetailsManager users = new InMemoryUserDetailsManager();
    // 비밀번호를 지정하지 않은 환경에서는 관리용 사용자를 만들지 않아 접근을 거절한다.
    if (!password.isBlank()) {
      users.createUser(
          User.withUsername("monitoring")
              .password(passwordEncoder.encode(password))
              .roles("MONITORING")
              .build());
    }
    DaoAuthenticationProvider provider = new DaoAuthenticationProvider(users);
    provider.setPasswordEncoder(passwordEncoder);

    http.securityMatcher(EndpointRequest.toAnyEndpoint())
        .authenticationProvider(provider)
        .httpBasic(Customizer.withDefaults())
        .sessionManagement(
            session -> session.sessionCreationPolicy(SessionCreationPolicy.STATELESS))
        .authorizeHttpRequests(
            requests ->
                requests
                    .requestMatchers(HttpMethod.GET, "/actuator/health", "/actuator/prometheus")
                    .hasRole("MONITORING")
                    .anyRequest()
                    .denyAll());
    return http.build();
  }
}
