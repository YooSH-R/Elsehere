package com.elsehere.auth.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import java.util.Optional;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({TestcontainersConfiguration.class, SocialLoginService.class})
class SocialLoginServiceTest {

  @Autowired private EntityManager entityManager;

  @Autowired private SocialLoginService socialLoginService;

  @Test
  @DisplayName("가입한 Google 계정이면 그 사용자의 로그인 정보를 돌려준다")
  void signedUpAccount_returnsLoginUser() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    Optional<LoginUser> loginUser =
        socialLoginService.findLoginUser(AuthProvider.GOOGLE, "google-sub-1");

    assertThat(loginUser).contains(new LoginUser(userId));
  }

  @Test
  @DisplayName("가입하지 않은 계정이면 빈 값을 돌려준다")
  void unknownAccount_returnsEmpty() {
    UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    Optional<LoginUser> loginUser =
        socialLoginService.findLoginUser(AuthProvider.GOOGLE, "google-sub-2");

    assertThat(loginUser).isEmpty();
  }
}
