package com.elsehere.admin.service;

import com.elsehere.admin.dto.AdminUser;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.User;
import com.elsehere.user.entity.UserRole;
import com.elsehere.user.repository.UserRepository;
import java.util.List;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * 관리자 지정·해제. 관리자는 게임에 Google로 가입한 사용자 중에서 이메일로 지정하며 게스트는 지정하지 않는다. 권한은 요청마다 DB에서 확인하므로(AdminAccess)
 * 바꾼 역할은 다시 로그인하지 않아도 다음 요청부터 적용된다.
 */
@Slf4j
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class AdminUserService {

  private final UserRepository userRepository;

  /** 가입한 순서로 보여 준다. */
  public List<AdminUser> admins() {
    return userRepository.findByRoleOrderById(UserRole.ADMIN).stream()
        .map(AdminUserService::toAdminUser)
        .toList();
  }

  /** 이메일로 찾은 Google 가입자를 관리자로 지정한다. 이미 관리자면 그대로 둔다. */
  @Transactional
  public AdminUser grant(Long adminUserId, String email) {
    User user =
        userRepository
            .findByProviderAndEmailIgnoreCase(AuthProvider.GOOGLE, email)
            .orElseThrow(() -> new BusinessException(ErrorCode.ADMIN_CANDIDATE_NOT_FOUND));
    if (user.getRole() != UserRole.ADMIN) {
      user.grantAdmin();
      log.info("admin_granted user_id={} by_user_id={}", user.getId(), adminUserId);
    }
    return toAdminUser(user);
  }

  /** 관리자를 해제한다. 관리자가 한 명도 남지 않는 일을 막으려고 자기 자신은 해제할 수 없다. */
  @Transactional
  public void revoke(Long adminUserId, Long userId) {
    if (adminUserId.equals(userId)) {
      throw new BusinessException(ErrorCode.ADMIN_SELF_REVOKE);
    }
    User user =
        userRepository
            .findById(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    if (user.getRole() == UserRole.ADMIN) {
      user.revokeAdmin();
      log.info("admin_revoked user_id={} by_user_id={}", userId, adminUserId);
    }
  }

  private static AdminUser toAdminUser(User user) {
    return new AdminUser(user.getId(), user.getNickname(), user.getEmail());
  }
}
