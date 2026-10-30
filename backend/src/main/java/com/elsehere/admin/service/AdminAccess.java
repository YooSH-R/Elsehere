package com.elsehere.admin.service;

import com.elsehere.user.entity.UserRole;
import com.elsehere.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 관리자인지 확인한다. 세션에는 사용자 ID만 두고 요청마다 DB의 역할을 읽으므로, 운영자가 관리자를 해제하면 다시 로그인하지 않아도 바로 막힌다. */
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class AdminAccess {

  private final UserRepository userRepository;

  public boolean isAdmin(Long userId) {
    return userRepository.existsByIdAndRole(userId, UserRole.ADMIN);
  }
}
