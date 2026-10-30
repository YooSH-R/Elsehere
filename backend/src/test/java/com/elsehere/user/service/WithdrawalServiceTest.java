package com.elsehere.user.service;

import static org.assertj.core.api.Assertions.assertThat;

import com.elsehere.conversation.service.DialogueServiceTest;
import com.elsehere.user.UserTestData;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.User;
import com.elsehere.user.entity.UserRole;
import com.elsehere.user.repository.UserRepository;
import jakarta.persistence.EntityManager;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.jdbc.core.JdbcTemplate;

// 회원 탈퇴(USER-05). 지우는 내용의 범위는 게스트 정리와 같아 UserContentEraser가 정하고, 여기서는 탈퇴만의 개인정보 지우기를 함께 본다.
@DialogueServiceTest
class WithdrawalServiceTest {

  @Autowired private WithdrawalService withdrawalService;

  @Autowired private UserRepository userRepository;

  @Autowired private EntityManager entityManager;

  @Autowired private JdbcTemplate jdbcTemplate;

  @Test
  @DisplayName("탈퇴하면 발화·피드백·기억·저장 단어·숨김·레벨 테스트를 바로 지우고 사용자·대화·사용량은 남긴다")
  void withdraw_deletesContentAndKeepsSkeleton() {
    Long memberId =
        UserContentTestData.userWithContent(
            entityManager, jdbcTemplate, AuthProvider.GOOGLE, "google-sub-1", "Sun");
    Long otherId =
        UserContentTestData.userWithContent(
            entityManager, jdbcTemplate, AuthProvider.GOOGLE, "google-sub-2", "Moon");

    withdrawalService.withdraw(memberId);

    assertThat(UserContentTestData.contentRows(entityManager, jdbcTemplate, memberId))
        .allSatisfy((table, rows) -> assertThat(rows).as(table).isZero());
    assertThat(rows("select count(*) from users where id = ?", memberId)).isEqualTo(1);
    assertThat(rows("select count(*) from conversations where user_id = ?", memberId)).isEqualTo(1);
    assertThat(rows("select count(*) from ai_requests where user_id = ?", memberId)).isEqualTo(1);
    assertThat(UserContentTestData.contentRows(entityManager, jdbcTemplate, otherId))
        .allSatisfy((table, rows) -> assertThat(rows).as(table).isPositive());
  }

  @Test
  @DisplayName("탈퇴하면 이메일·Google 계정 ID·닉네임을 지워 같은 Google 계정과 닉네임으로 다시 가입할 수 있다")
  void withdraw_removesPersonalInfo() {
    Long memberId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    jdbcTemplate.update("update users set email = 'sun@example.com' where id = ?", memberId);

    withdrawalService.withdraw(memberId);

    entityManager.flush();
    entityManager.clear();
    User withdrawn = userRepository.findById(memberId).orElseThrow();
    assertThat(withdrawn.getEmail()).isNull();
    assertThat(withdrawn.getProviderUid()).isNotEqualTo("google-sub-1");
    assertThat(withdrawn.getNickname()).isNotEqualTo("Sun");
    assertThat(userRepository.findByProviderAndProviderUid(AuthProvider.GOOGLE, "google-sub-1"))
        .isEmpty();
    assertThat(userRepository.existsByNickname("Sun")).isFalse();
  }

  @Test
  @DisplayName("관리자가 탈퇴하면 관리자 권한도 없앤다")
  void withdraw_admin_revokesAdmin() {
    Long adminId = UserTestData.insertUser(entityManager, AuthProvider.GOOGLE, "google-sub-1");
    jdbcTemplate.update("update users set role = 'ADMIN' where id = ?", adminId);

    withdrawalService.withdraw(adminId);

    entityManager.flush();
    entityManager.clear();
    assertThat(userRepository.findById(adminId).orElseThrow().getRole()).isEqualTo(UserRole.USER);
  }

  private int rows(String sql, Long userId) {
    return UserContentTestData.rows(entityManager, jdbcTemplate, sql, userId);
  }
}
