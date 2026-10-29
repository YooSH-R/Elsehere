package com.elsehere.onboarding.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.onboarding.dto.LanguageProfileResponse;
import com.elsehere.onboarding.dto.LanguageSelectionResponse;
import com.elsehere.onboarding.dto.LevelSelectionResponse;
import com.elsehere.onboarding.dto.LevelTestSummary;
import com.elsehere.onboarding.entity.UserLanguageProfile;
import com.elsehere.onboarding.entity.UserLanguageProfileId;
import com.elsehere.onboarding.repository.LevelTestRepository;
import com.elsehere.onboarding.repository.UserLanguageProfileRepository;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 가입 후 초기 설정(학습 언어 선택 ONB-02, 대화 난이도 조회·설정 ONB-08·09). */
@Service
@RequiredArgsConstructor
@Transactional
public class OnboardingService {

  private final UserRepository userRepository;
  private final UserLanguageProfileRepository profileRepository;
  private final LevelTestRepository levelTestRepository;
  private final LanguageCatalog languageCatalog;
  private final LevelCatalog levelCatalog;

  public LanguageSelectionResponse chooseLanguage(Long userId, String languageCode) {
    if (!languageCatalog.isSelectable(languageCode)) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }
    User user =
        userRepository
            .findById(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    // 이미 고른 적 있는 언어면 추천·선택 난이도가 남아 있으므로 그대로 둔다.
    if (!profileRepository.existsById(new UserLanguageProfileId(userId, languageCode))) {
      profileRepository.save(UserLanguageProfile.start(user, languageCode));
    }
    user.chooseLanguage(languageCode);
    return new LanguageSelectionResponse(languageCode, user.getOnboardingStep());
  }

  @Transactional(readOnly = true)
  public LanguageProfileResponse getLanguageProfile(Long userId, String languageCode) {
    UserLanguageProfile profile = findProfile(userId, languageCode);
    // selected_level은 프로필을 만들 때 기본값으로 채워지므로, 난이도 설정을 마쳤을 때만 사용자가 고른 값으로 보여준다.
    Integer selectedLevel =
        profile.getUser().hasCompletedLevelSetting() ? profile.getSelectedLevel() : null;
    return new LanguageProfileResponse(
        languageCode,
        profile.getRecommendedLevel(),
        selectedLevel,
        findLastLevelTest(profile),
        levelCatalog.levels());
  }

  private LevelTestSummary findLastLevelTest(UserLanguageProfile profile) {
    if (profile.getLastTestId() == null) {
      return null;
    }
    return levelTestRepository
        .findById(profile.getLastTestId())
        .map(test -> new LevelTestSummary(test.getId(), test.getStatus()))
        .orElse(null);
  }

  public LevelSelectionResponse selectLevel(Long userId, String languageCode, int level) {
    if (!levelCatalog.isSelectable(level)) {
      throw new BusinessException(ErrorCode.VALIDATION_FAILED);
    }
    UserLanguageProfile profile = findProfile(userId, languageCode);
    profile.selectLevel(level);
    User user = profile.getUser();
    user.completeLevelSetting();
    return new LevelSelectionResponse(languageCode, level, user.getOnboardingStep());
  }

  /** 고른 적 없는 언어면 학습 설정이 없다. */
  private UserLanguageProfile findProfile(Long userId, String languageCode) {
    return profileRepository
        .findById(new UserLanguageProfileId(userId, languageCode))
        .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
  }
}
