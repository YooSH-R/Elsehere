package com.elsehere.admin.service;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import com.elsehere.TestcontainersConfiguration;
import com.elsehere.admin.dto.AdminUser;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
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

// 지정·해제 결과는 요청마다 권한을 확인하는 AdminAccess로 다시 읽어 확인한다.
@DataJpaTest(properties = "spring.jpa.hibernate.ddl-auto=create-drop")
@AutoConfigureTestDatabase(replace = Replace.NONE)
@Import({TestcontainersConfiguration.class, AdminUserService.class, AdminAccess.class})
class AdminUserServiceTest {

  @Autowired private AdminUserService adminUserService;

  @Autowired private AdminAccess adminAccess;

  @Autowired private EntityManager entityManager;

  @Test
  @DisplayName("관리자 목록은 관리자만 가입한 순서로 닉네임·이메일과 함께 보여 준다")
  void admins_listsOnlyAdminsInSignUpOrder() {
    Long first = insertGoogleUser("google-1", "Sun", "sun@example.com");
    insertGoogleUser("google-2", "Moon", "moon@example.com");
    Long third = insertGoogleUser("google-3", "Star", "star@example.com");
    makeAdmin(third);
    makeAdmin(first);

    assertThat(adminUserService.admins())
        .containsExactly(
            new AdminUser(first, "Sun", "sun@example.com"),
            new AdminUser(third, "Star", "star@example.com"));
  }

  @Test
  @DisplayName("Google로 가입한 사용자의 이메일로 관리자를 추가하면 그 사용자가 바로 관리자가 된다")
  void grant_signedUpGoogleEmail_makesAdmin() {
    Long admin = insertAdmin();
    Long userId = insertGoogleUser("google-2", "Moon", "moon@example.com");

    AdminUser granted = adminUserService.grant(admin, "moon@example.com");
    flushAndClear();

    assertThat(granted).isEqualTo(new AdminUser(userId, "Moon", "moon@example.com"));
    assertThat(adminAccess.isAdmin(userId)).isTrue();
  }

  @Test
  @DisplayName("이메일은 대소문자를 가리지 않고 찾는다")
  void grant_ignoresEmailCase() {
    Long admin = insertAdmin();
    Long userId = insertGoogleUser("google-2", "Moon", "moon@example.com");

    adminUserService.grant(admin, "Moon@Example.COM");
    flushAndClear();

    assertThat(adminAccess.isAdmin(userId)).isTrue();
  }

  @Test
  @DisplayName("이미 관리자인 사용자를 다시 추가해도 오류 없이 관리자로 남는다")
  void grant_alreadyAdmin_keepsAdmin() {
    Long admin = insertAdmin();
    Long userId = insertGoogleUser("google-2", "Moon", "moon@example.com");
    makeAdmin(userId);

    AdminUser granted = adminUserService.grant(admin, "moon@example.com");
    flushAndClear();

    assertThat(granted.userId()).isEqualTo(userId);
    assertThat(adminAccess.isAdmin(userId)).isTrue();
  }

  @Test
  @DisplayName("그 이메일로 Google 가입한 사용자가 없으면 ADMIN_CANDIDATE_NOT_FOUND로 거절한다")
  void grant_unknownEmail_throwsCandidateNotFound() {
    Long admin = insertAdmin();

    assertThatThrownBy(() -> adminUserService.grant(admin, "nobody@example.com"))
        .isInstanceOf(BusinessException.class)
        .extracting("errorCode")
        .isEqualTo(ErrorCode.ADMIN_CANDIDATE_NOT_FOUND);
  }

  @Test
  @DisplayName("게스트 계정은 같은 이메일이어도 관리자로 지정하지 않는다")
  void grant_guestUser_throwsCandidateNotFound() {
    Long admin = insertAdmin();
    Long guestId =
        UserTestData.insertUser(entityManager, AuthProvider.GUEST, "guest-uid-1", "guest-1");
    setEmail(guestId, "guest@example.com");

    assertThatThrownBy(() -> adminUserService.grant(admin, "guest@example.com"))
        .isInstanceOf(BusinessException.class)
        .extracting("errorCode")
        .isEqualTo(ErrorCode.ADMIN_CANDIDATE_NOT_FOUND);
    assertThat(adminAccess.isAdmin(guestId)).isFalse();
  }

  @Test
  @DisplayName("다른 관리자를 해제하면 그 사용자는 다음 요청부터 관리자가 아니다")
  void revoke_otherAdmin_makesUser() {
    Long admin = insertAdmin();
    Long userId = insertGoogleUser("google-2", "Moon", "moon@example.com");
    makeAdmin(userId);

    adminUserService.revoke(admin, userId);
    flushAndClear();

    assertThat(adminAccess.isAdmin(userId)).isFalse();
    assertThat(adminUserService.admins()).extracting(AdminUser::userId).containsExactly(admin);
  }

  @Test
  @DisplayName("자기 자신은 해제할 수 없어 ADMIN_SELF_REVOKE로 거절하고 관리자로 남는다")
  void revoke_self_throwsSelfRevokeAndKeepsAdmin() {
    Long admin = insertAdmin();

    assertThatThrownBy(() -> adminUserService.revoke(admin, admin))
        .isInstanceOf(BusinessException.class)
        .extracting("errorCode")
        .isEqualTo(ErrorCode.ADMIN_SELF_REVOKE);
    flushAndClear();
    assertThat(adminAccess.isAdmin(admin)).isTrue();
  }

  @Test
  @DisplayName("없는 사용자를 해제하면 NOT_FOUND로 거절한다")
  void revoke_unknownUser_throwsNotFound() {
    Long admin = insertAdmin();

    assertThatThrownBy(() -> adminUserService.revoke(admin, 999L))
        .isInstanceOf(BusinessException.class)
        .extracting("errorCode")
        .isEqualTo(ErrorCode.NOT_FOUND);
  }

  private Long insertAdmin() {
    Long userId = insertGoogleUser("google-admin", "Admin", "admin@example.com");
    makeAdmin(userId);
    return userId;
  }

  private Long insertGoogleUser(String providerUid, String nickname, String email) {
    Long userId =
        UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, providerUid, nickname);
    setEmail(userId, email);
    return userId;
  }

  private void setEmail(Long userId, String email) {
    entityManager
        .createNativeQuery("update users set email = ? where id = ?")
        .setParameter(1, email)
        .setParameter(2, userId)
        .executeUpdate();
  }

  private void makeAdmin(Long userId) {
    entityManager
        .createNativeQuery("update users set role = 'ADMIN' where id = ?")
        .setParameter(1, userId)
        .executeUpdate();
  }

  private void flushAndClear() {
    entityManager.flush();
    entityManager.clear();
  }
}
