package com.elsehere.admin.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import java.nio.charset.StandardCharsets;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.security.crypto.bcrypt.BCryptPasswordEncoder;
import org.springframework.stereotype.Service;

/**
 * 어드민 아이디·비밀번호를 확인한다. 계정은 환경마다 환경변수로 하나만 두며(docs/admin.md), 비밀번호는 시작할 때 해시로 바꿔 메모리에만 둔다.
 *
 * <p>어드민 아이디로 연속 {@value #MAX_FAILURES}번 틀리면 {@link #LOCK_DURATION} 동안 로그인을 막는다. 틀린 횟수와 잠금은 서버
 * 메모리에만 있어 서버를 다시 시작하면 풀린다. 백엔드는 환경마다 컨테이너 하나로 실행한다.
 */
@Slf4j
@Service
public class AdminLoginService {

  private static final int MIN_PASSWORD_LENGTH = 12;
  private static final int MAX_PASSWORD_BYTES = 72;
  private static final int MAX_FAILURES = 5;
  private static final Duration LOCK_DURATION = Duration.ofMinutes(15);

  private final BCryptPasswordEncoder passwordEncoder = new BCryptPasswordEncoder();
  private final String loginId;
  private final String passwordHash;
  private final Clock clock;
  private int failures;
  private Instant lockedUntil;

  /**
   * 비밀번호가 비어 있으면 어드민 로그인을 끈다. 잘못 설정한 계정으로 서버가 뜨지 않도록, 비밀번호가 {@value #MIN_PASSWORD_LENGTH}자보다 짧거나
   * BCrypt가 다루는 {@value #MAX_PASSWORD_BYTES}바이트보다 길거나 아이디가 비어 있으면 시작하지 않는다.
   */
  public AdminLoginService(
      @Value("${elsehere.admin.login-id}") String loginId,
      @Value("${elsehere.admin.password}") String password,
      Clock clock) {
    this.loginId = loginId;
    this.passwordHash = password.isEmpty() ? null : passwordEncoder.encode(validated(password));
    this.clock = clock;
    if (passwordHash != null && loginId.isBlank()) {
      throw new IllegalStateException(
          "어드민 비밀번호(ADMIN_PASSWORD)가 있으면 아이디(ADMIN_LOGIN_ID)도 정해야 합니다.");
    }
  }

  private static String validated(String password) {
    if (password.length() < MIN_PASSWORD_LENGTH) {
      throw new IllegalStateException(
          "어드민 비밀번호(ADMIN_PASSWORD)는 " + MIN_PASSWORD_LENGTH + "자 이상이어야 합니다.");
    }
    if (password.getBytes(StandardCharsets.UTF_8).length > MAX_PASSWORD_BYTES) {
      throw new IllegalStateException(
          "어드민 비밀번호(ADMIN_PASSWORD)는 UTF-8로 " + MAX_PASSWORD_BYTES + "바이트 이하여야 합니다.");
    }
    return password;
  }

  /** 아이디·비밀번호가 맞으면 어드민 로그인을 돌려준다. 무엇이 틀렸는지는 알리지 않는다. */
  public synchronized AdminLogin login(String loginId, String password) {
    if (passwordHash == null) {
      throw new BusinessException(ErrorCode.ADMIN_LOGIN_FAILED);
    }
    Instant now = clock.instant();
    if (lockedUntil != null && now.isBefore(lockedUntil)) {
      throw new BusinessException(ErrorCode.ADMIN_LOGIN_LOCKED);
    }
    // 아이디가 틀려도 비밀번호를 똑같이 확인해, 응답 시간으로 아이디가 맞는지 알 수 없게 한다.
    boolean passwordMatches = passwordEncoder.matches(password, passwordHash);
    boolean loginIdMatches = this.loginId.equals(loginId);
    if (loginIdMatches && passwordMatches) {
      failures = 0;
      lockedUntil = null;
      log.info("admin_login_succeeded");
      return new AdminLogin(this.loginId);
    }
    // 없는 아이디는 맞힐 수 없으므로 세지 않는다. 어드민 아이디로 틀린 것만 세어 비밀번호를 계속 넣어 보는 것을 막는다.
    // 넣은 아이디는 비밀번호를 잘못 넣었을 수 있어 로그에 남기지 않는다.
    if (loginIdMatches) {
      failures++;
      if (failures >= MAX_FAILURES) {
        failures = 0;
        lockedUntil = now.plus(LOCK_DURATION);
        log.warn("admin_login_locked minutes={}", LOCK_DURATION.toMinutes());
      }
    }
    log.info("admin_login_failed known_login_id={}", loginIdMatches);
    throw new BusinessException(ErrorCode.ADMIN_LOGIN_FAILED);
  }
}
