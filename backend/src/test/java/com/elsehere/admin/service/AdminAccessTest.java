package com.elsehere.admin.service;

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
@Import({TestcontainersConfiguration.class, AdminAccess.class})
class AdminAccessTest {

  @Autowired private AdminAccess adminAccess;

  @Autowired private EntityManager entityManager;

  @Test
  @DisplayName("가입한 사용자는 관리자가 아니다")
  void signedUpUser_isNotAdmin() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-1");

    assertThat(adminAccess.isAdmin(userId)).isFalse();
  }

  @Test
  @DisplayName("users.role이 ADMIN인 사용자는 관리자다")
  void adminRoleUser_isAdmin() {
    Long userId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-1");
    entityManager
        .createNativeQuery("update users set role = 'ADMIN' where id = ?")
        .setParameter(1, userId)
        .executeUpdate();

    assertThat(adminAccess.isAdmin(userId)).isTrue();
  }

  @Test
  @DisplayName("없는 사용자는 관리자가 아니다")
  void unknownUser_isNotAdmin() {
    assertThat(adminAccess.isAdmin(999L)).isFalse();
  }
}
