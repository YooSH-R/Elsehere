package com.elsehere.user.repository;

import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.User;
import java.util.Optional;
import org.springframework.data.jpa.repository.JpaRepository;

public interface UserRepository extends JpaRepository<User, Long> {

  Optional<User> findByProviderAndProviderUid(AuthProvider provider, String providerUid);

  boolean existsByNickname(String nickname);
}
