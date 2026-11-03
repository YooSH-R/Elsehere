package com.elsehere.user.dto;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.AvatarType;
import com.elsehere.user.entity.User;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

class UserResponseTest {

  @Test
  @DisplayName("게스트로 시작한 사용자는 guest가 true다")
  void from_guestUser_marksGuest() {
    User guest = User.signUp(AuthProvider.GUEST, "guest-uid", null, "guest-123456", AvatarType.M);

    assertThat(UserResponse.from(guest, false).guest()).isTrue();
  }

  @Test
  @DisplayName("소셜 로그인으로 가입한 사용자는 guest가 false다")
  void from_socialUser_isNotGuest() {
    User member =
        User.signUp(AuthProvider.GOOGLE, "google-sub-1", "sun@example.com", "Sun", AvatarType.F);

    assertThat(UserResponse.from(member, false).guest()).isFalse();
  }
}
