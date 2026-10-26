package com.elsehere.user.service;

import com.elsehere.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 닉네임 중복 확인(AUTH-06). 확인한 닉네임을 예약하지 않으므로 가입할 때 다시 검사한다. */
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class NicknameService {

  private final UserRepository userRepository;

  public boolean isAvailable(String nickname) {
    return !userRepository.existsByNickname(nickname);
  }
}
