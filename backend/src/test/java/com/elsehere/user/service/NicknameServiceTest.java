package com.elsehere.user.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import jakarta.persistence.EntityManager;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.data.jpa.test.autoconfigure.DataJpaTest;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase;
import org.springframework.boot.jdbc.test.autoconfigure.AutoConfigureTestDatabase.Replace;
import org.springframework.context.annotation.Import;

@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({TestcontainersConfiguration.class, NicknameService.class})
class NicknameServiceTest {

  @Autowired private NicknameService nicknameService;

  @Autowired private EntityManager entityManager;

  @Test
  @DisplayName("아무도 쓰지 않는 닉네임은 쓸 수 있다")
  void unusedNickname_isAvailable() {
    assertThat(nicknameService.isAvailable("Sun")).isTrue();
  }

  @Test
  @DisplayName("다른 사용자가 쓰는 닉네임은 쓸 수 없다")
  void takenNickname_isNotAvailable() {
    UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");

    assertThat(nicknameService.isAvailable("Sun")).isFalse();
  }
}
