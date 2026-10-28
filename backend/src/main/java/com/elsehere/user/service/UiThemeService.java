package com.elsehere.user.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.user.dto.UiThemeResponse;
import com.elsehere.user.entity.UiTheme;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 사용자가 집에서 고른 화면 테마를 계정에 저장한다(USER-01). */
@Service
@RequiredArgsConstructor
@Transactional
public class UiThemeService {

  private final UserRepository userRepository;

  public UiThemeResponse changeUiTheme(Long userId, UiTheme uiTheme) {
    User user =
        userRepository
            .findById(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    user.changeUiTheme(uiTheme);
    return new UiThemeResponse(user.getUiTheme());
  }
}
