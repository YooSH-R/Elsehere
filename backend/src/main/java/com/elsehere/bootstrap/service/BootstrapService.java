package com.elsehere.bootstrap.service;

import com.elsehere.ai.service.AiStatusService;
import com.elsehere.bootstrap.dto.BootstrapResponse;
import com.elsehere.mission.service.ShowcaseQuestService;
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
  private final ShowcaseQuestService showcase;

  /** 로그인한 사용자의 상태. 사용자가 없으면(탈퇴 등) 빈 값이다. */
  public Optional<BootstrapResponse> findSignedIn(Long userId) {
    return userRepository
        .findById(userId)
        .map(
            user ->
                BootstrapResponse.signedIn(
                    UserResponse.from(
                        user, optionalConsentService.hasAgreed(userId, ConsentType.VOICE)),
                    startSceneCode(user),
                    aiStatusService.statusOf(user)));
  }

  /** 팀 기기에서 접속한 사용자를 팀 계정으로 표시한다. 사용자가 없으면 아무것도 하지 않는다. */
  @Transactional
  public void markTeamAccount(Long userId) {
    userRepository.findById(userId).ifPresent(User::markTeamAccount);
  }

  // 시연 서버에서는 시연을 하는 중이 아니면 마지막 장소와 상관없이 호텔에서 시작한다. 하는 중이면 마지막 장소로 돌아간다.
  private String startSceneCode(User user) {
    if (showcase.enabled()
        && showcase.activeStatus(user.getId()).filter(status -> !status.completed()).isEmpty()) {
      return "HOTEL";
    }
    return lastSceneCode(user);
  }

  private static String lastSceneCode(User user) {
    Scene lastScene = user.getLastScene();
    return lastScene == null ? null : lastScene.getCode();
  }
}
