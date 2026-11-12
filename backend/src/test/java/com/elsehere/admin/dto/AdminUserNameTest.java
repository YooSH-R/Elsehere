package com.elsehere.admin.dto;

import static org.assertj.core.api.Assertions.assertThat;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class AdminUserNameTest {

  @Test
  @DisplayName("가입자는 이메일로, 게스트는 '게스트 #ID', 탈퇴한 사용자는 '탈퇴 #ID', 이메일이 없는 가입자는 '회원 #ID'로 부른다")
  void of_namesUserForAdminScreens() {
    assertThat(AdminUserName.of(7L, false, false, "sun@example.com")).isEqualTo("sun@example.com");
    assertThat(AdminUserName.of(57L, true, false, null)).isEqualTo("게스트 #57");
    assertThat(AdminUserName.of(12L, false, true, null)).isEqualTo("탈퇴 #12");
    assertThat(AdminUserName.of(3L, false, false, null)).isEqualTo("회원 #3");
  }
}
