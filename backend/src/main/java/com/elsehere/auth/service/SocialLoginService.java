package com.elsehere.auth.service;

import com.elsehere.common.auth.LoginUser;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.repository.UserRepository;
import java.util.Optional;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 소셜 로그인 계정으로 가입한 사용자를 찾는다. */
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class SocialLoginService {

  private final UserRepository userRepository;

  /** 가입한 계정이면 로그인할 사용자를, 처음 로그인한 계정이면 빈 값을 돌려준다. */
  public Optional<LoginUser> findLoginUser(AuthProvider provider, String providerUid) {
    return userRepository
        .findByProviderAndProviderUid(provider, providerUid)
        .map(user -> new LoginUser(user.getId()));
  }
}
