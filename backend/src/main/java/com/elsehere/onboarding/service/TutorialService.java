package com.elsehere.onboarding.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.onboarding.dto.TutorialResponse;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 튜토리얼 완료(TUT-01)와 건너뛰기(TUT-02). 둘 다 초기 설정을 끝내 마을에서 시작하게 한다. */
@Service
@RequiredArgsConstructor
@Transactional
public class TutorialService {

  private final UserRepository userRepository;

  public TutorialResponse complete(Long userId) {
    return finishTutorial(userId);
  }

  public TutorialResponse skip(Long userId) {
    return finishTutorial(userId);
  }

  // 완료와 건너뛰기는 결과가 같다. 튜토리얼 단계 전(LANG·LEVEL)이면 거절하고, 이미 끝낸(DONE) 사용자는 같은 결과를 돌려준다.
  private TutorialResponse finishTutorial(Long userId) {
    User user =
        userRepository
            .findById(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    if (!user.hasCompletedLevelSetting()) {
      throw new BusinessException(ErrorCode.TUTORIAL_NOT_IN_PROGRESS);
    }
    user.finishTutorial();
    return new TutorialResponse(user.getOnboardingStep());
  }
}
