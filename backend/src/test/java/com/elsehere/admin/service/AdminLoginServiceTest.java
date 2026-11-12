package com.elsehere.admin.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.elsehere.common.config.AdjustableClock;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import java.time.Instant;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;

class AdminLoginServiceTest {

  private static final String LOGIN_ID = "operator";
  private static final String PASSWORD = "correct-horse-battery";
  private static final Instant NOW = Instant.parse("2030-01-10T00:00:00Z");

  private final AdjustableClock clock = new AdjustableClock(NOW);

  private final AdminLoginService adminLoginService =
      new AdminLoginService(LOGIN_ID, PASSWORD, clock);

  @Test
  @DisplayName("설정한 아이디·비밀번호가 맞으면 그 아이디로 어드민 로그인을 돌려준다")
  void login_withConfiguredAccount_returnsAdminLogin() {
    AdminLogin adminLogin = adminLoginService.login(LOGIN_ID, PASSWORD);

    assertThat(adminLogin).isEqualTo(new AdminLogin(LOGIN_ID));
  }

  @Test
  @DisplayName("비밀번호가 틀리면 ADMIN_LOGIN_FAILED로 거절한다")
  void login_withWrongPassword_isRejected() {
    assertLoginFails(LOGIN_ID, "wrong-password-123", ErrorCode.ADMIN_LOGIN_FAILED);
  }

  @Test
  @DisplayName("아이디가 틀려도 무엇이 틀렸는지 알리지 않고 같은 ADMIN_LOGIN_FAILED로 거절한다")
  void login_withWrongLoginId_isRejectedTheSameWay() {
    assertLoginFails("someone", PASSWORD, ErrorCode.ADMIN_LOGIN_FAILED);
  }

  @Test
  @DisplayName("같은 아이디로 5번 연속 틀리면 15분 동안 맞는 비밀번호로도 ADMIN_LOGIN_LOCKED로 거절한다")
  void login_afterFiveFailures_isLockedForFifteenMinutes() {
    failTimes(5);
    clock.setInstant(NOW.plusSeconds(15 * 60 - 1));

    assertLoginFails(LOGIN_ID, PASSWORD, ErrorCode.ADMIN_LOGIN_LOCKED);
  }

  @Test
  @DisplayName("4번까지 틀려도 잠그지 않는다")
  void login_afterFourFailures_isNotLocked() {
    failTimes(4);

    assertThat(adminLoginService.login(LOGIN_ID, PASSWORD)).isEqualTo(new AdminLogin(LOGIN_ID));
  }

  @Test
  @DisplayName("잠근 지 15분이 지나면 다시 로그인할 수 있다")
  void login_afterLockExpires_succeeds() {
    failTimes(5);
    clock.setInstant(NOW.plusSeconds(15 * 60));

    assertThat(adminLoginService.login(LOGIN_ID, PASSWORD)).isEqualTo(new AdminLogin(LOGIN_ID));
  }

  @Test
  @DisplayName("로그인에 성공하면 틀린 횟수를 처음부터 다시 센다")
  void login_success_resetsFailures() {
    failTimes(4);
    adminLoginService.login(LOGIN_ID, PASSWORD);
    failTimes(4);

    assertThat(adminLoginService.login(LOGIN_ID, PASSWORD)).isEqualTo(new AdminLogin(LOGIN_ID));
  }

  @Test
  @DisplayName("없는 아이디로 틀린 것은 세지 않아 어드민 계정을 잠그지 않는다")
  void login_failuresWithOtherLoginId_doNotLock() {
    for (int attempt = 0; attempt < 5; attempt++) {
      assertLoginFails("someone", "wrong-password-123", ErrorCode.ADMIN_LOGIN_FAILED);
    }

    assertThat(adminLoginService.login(LOGIN_ID, PASSWORD)).isEqualTo(new AdminLogin(LOGIN_ID));
  }

  @Test
  @DisplayName("비밀번호를 설정하지 않은 환경은 어드민 로그인을 꺼 빈 아이디·비밀번호로도 로그인할 수 없다")
  void login_withoutConfiguredPassword_isAlwaysRejected() {
    AdminLoginService disabled = new AdminLoginService("", "", clock);

    assertThatThrownBy(() -> disabled.login("", ""))
        .isInstanceOf(BusinessException.class)
        .extracting("errorCode")
        .isEqualTo(ErrorCode.ADMIN_LOGIN_FAILED);
  }

  @ParameterizedTest(name = "[{index}] 아이디 {0}, 비밀번호 {1}")
  @CsvSource({
    // 12자보다 짧은 비밀번호
    "operator, short-pass1",
    // 72바이트보다 긴 비밀번호(BCrypt가 다루는 길이)
    "operator, aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    // 비밀번호만 있고 아이디가 빔
    "'', correct-horse-battery"
  })
  @DisplayName("비밀번호가 12자보다 짧거나 72바이트보다 길거나 아이디 없이 비밀번호만 있으면 서버를 시작하지 않는다")
  void create_withInvalidAccount_fails(String loginId, String password) {
    assertThatThrownBy(() -> new AdminLoginService(loginId, password, clock))
        .isInstanceOf(IllegalStateException.class);
  }

  private void failTimes(int times) {
    for (int attempt = 0; attempt < times; attempt++) {
      assertLoginFails(LOGIN_ID, "wrong-password-123", ErrorCode.ADMIN_LOGIN_FAILED);
    }
  }

  private void assertLoginFails(String loginId, String password, ErrorCode errorCode) {
    assertThatThrownBy(() -> adminLoginService.login(loginId, password))
        .isInstanceOf(BusinessException.class)
        .extracting("errorCode")
        .isEqualTo(errorCode);
  }
}
