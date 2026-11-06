package com.elsehere.onboarding.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.onboarding.dto.TutorialResponse;
import com.elsehere.user.entity.OnboardingStep;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 튜토리얼 완료(TUT-01), 건너뛰기(TUT-02), Ben 길 안내의 관계 저장(TUT-03). */
@Service
@RequiredArgsConstructor
@Transactional
public class TutorialService {

  private final UserRepository userRepository;
  private final TutorialRelationshipService tutorialRelationshipService;

  public TutorialResponse complete(Long userId) {
    return finishTutorial(userId, true);
  }

  public TutorialResponse skip(Long userId) {
    return finishTutorial(userId, false);
  }

  /** 고정 대본을 끝낸 즉시 반영한다. 전체 완료·건너뛰기를 기다리지 않으며 재전송은 중복되지 않는다. */
  public TutorialResponse completeBenDialogue(Long userId) {
    User user = tutorialUser(userId);
    if (user.getOnboardingStep() == OnboardingStep.TUTORIAL) {
      tutorialRelationshipService.recordBenDialogue(userId);
    }
    return new TutorialResponse(user.getOnboardingStep());
  }

  // 완료만 Ben 관계를 보완한다. 이미 끝낸 사용자는 추가 보상 없이 같은 DONE 상태를 돌려준다.
  private TutorialResponse finishTutorial(Long userId, boolean completed) {
    User user = tutorialUser(userId);
    // 이전 클라이언트 또는 길 안내 저장 실패 뒤 전체 완료도 같은 첫 방문을 보완한다.
    if (completed && user.getOnboardingStep() == OnboardingStep.TUTORIAL) {
      tutorialRelationshipService.recordBenDialogue(userId);
    }
    user.finishTutorial();
    return new TutorialResponse(user.getOnboardingStep());
  }

  private User tutorialUser(Long userId) {
    User user =
        userRepository
            .findForUpdate(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    if (!user.hasCompletedLevelSetting()) {
      throw new BusinessException(ErrorCode.TUTORIAL_NOT_IN_PROGRESS);
    }
    return user;
  }
}
