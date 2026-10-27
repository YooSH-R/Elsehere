package com.elsehere.auth.service;

import com.elsehere.user.dto.UserResponse;
import com.elsehere.user.entity.AuthProvider;
import com.elsehere.user.entity.AvatarType;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import java.util.UUID;
import java.util.concurrent.ThreadLocalRandom;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * 소셜 계정 없이 바로 시작하는 게스트 사용자를 만든다(AUTH-07).
 *
 * <p>가입 화면을 거치지 않으므로 닉네임과 캐릭터는 정해진 값으로 채우고 약관 동의 기록은 남기지 않는다.
 */
@Service
@RequiredArgsConstructor
@Transactional
public class GuestLoginService {

  private static final String NICKNAME_PREFIX = "게스트";
  private static final int NICKNAME_NUMBER_BOUND = 1_000_000;
  private static final AvatarType GUEST_AVATAR = AvatarType.F;

  private final UserRepository userRepository;

  public UserResponse startAsGuest() {
    User guest =
        User.signUp(
            AuthProvider.GUEST, UUID.randomUUID().toString(), null, unusedNickname(), GUEST_AVATAR);
    return UserResponse.from(userRepository.save(guest));
  }

  /** "게스트" 뒤에 여섯 자리 숫자를 붙인다. 이미 쓰는 닉네임이면 숫자를 다시 뽑는다. */
  private String unusedNickname() {
    String nickname;
    do {
      int number = ThreadLocalRandom.current().nextInt(NICKNAME_NUMBER_BOUND);
      nickname = NICKNAME_PREFIX + String.format("%06d", number);
    } while (userRepository.existsByNickname(nickname));
    return nickname;
  }
}
