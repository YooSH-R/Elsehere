package com.elsehere.bootstrap.service;

import com.elsehere.ai.service.AiStatusService;
import com.elsehere.bootstrap.dto.BootstrapResponse;
import com.elsehere.user.dto.UserResponse;
import com.elsehere.user.entity.ConsentType;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import com.elsehere.user.service.OptionalConsentService;
import com.elsehere.world.entity.Scene;
import java.util.Optional;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 접속 직후 첫 화면을 정하는 데 필요한 사용자 상태를 모은다(BOOT-01). */
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class BootstrapService {

  private final UserRepository userRepository;
  private final AiStatusService aiStatusService;
  private final OptionalConsentService optionalConsentService;

  /** 로그인한 사용자의 상태. 사용자가 없으면(탈퇴 등) 빈 값이다. */
  public Optional<BootstrapResponse> findSignedIn(Long userId) {
    return userRepository
        .findById(userId)
        .map(
            user ->
                BootstrapResponse.signedIn(
                    UserResponse.from(
                        user, optionalConsentService.hasAgreed(userId, ConsentType.VOICE)),
                    lastSceneCode(user),
                    aiStatusService.statusOf(user)));
  }

  /** 팀 기기에서 접속한 사용자를 팀 계정으로 표시한다. 사용자가 없으면 아무것도 하지 않는다. */
  @Transactional
  public void markTeamAccount(Long userId) {
    userRepository.findById(userId).ifPresent(User::markTeamAccount);
  }

  private static String lastSceneCode(User user) {
    Scene lastScene = user.getLastScene();
    return lastScene == null ? null : lastScene.getCode();
  }
}
