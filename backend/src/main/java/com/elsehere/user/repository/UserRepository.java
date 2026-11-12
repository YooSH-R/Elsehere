package com.elsehere.user.repository;

import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.User;
import jakarta.persistence.LockModeType;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;

public interface UserRepository extends JpaRepository<User, Long> {
  /** 같은 사용자의 요청을 한 줄로 세울 때 사용자 행을 잠근다(첫날 미션 시작, 대화 시작 저장). */
  @Lock(LockModeType.PESSIMISTIC_WRITE)
  @Query("select user from User user where user.id = :userId")
  Optional<User> findForUpdate(Long userId);

  Optional<User> findByProviderAndProviderUid(AuthProvider provider, String providerUid);

  boolean existsByNickname(String nickname);

  Optional<User> findByProviderAndEmailIgnoreCase(AuthProvider provider, String email);
}
