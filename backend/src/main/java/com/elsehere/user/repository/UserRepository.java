package com.elsehere.user.repository;

import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.User;
import com.elsehere.user.entity.UserRole;
import jakarta.persistence.LockModeType;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Lock;
import org.springframework.data.jpa.repository.Query;

public interface UserRepository extends JpaRepository<User, Long> {
  @Lock(LockModeType.PESSIMISTIC_WRITE)
  @Query("select user from User user where user.id = :userId")
  Optional<User> findForMissionUpdate(Long userId);

  Optional<User> findByProviderAndProviderUid(AuthProvider provider, String providerUid);

  boolean existsByNickname(String nickname);

  boolean existsByIdAndRole(Long id, UserRole role);
}
