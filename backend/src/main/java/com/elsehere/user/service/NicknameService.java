package com.elsehere.user.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.user.dto.NicknameChangeResponse;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import lombok.RequiredArgsConstructor;
import org.hibernate.exception.ConstraintViolationException;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** 닉네임 중복 확인(AUTH-06)과 닉네임 변경(USER-04). 확인한 닉네임을 예약하지 않으므로 가입·변경할 때 다시 검사한다. */
@Service
@RequiredArgsConstructor
@Transactional(readOnly = true)
public class NicknameService {

  private final UserRepository userRepository;

  public boolean isAvailable(String nickname) {
    return !userRepository.existsByNickname(nickname);
  }

  /** 지금 쓰는 닉네임을 그대로 보내면 바꿀 것이 없으므로 중복으로 보지 않는다. */
  @Transactional
  public NicknameChangeResponse changeNickname(Long userId, String nickname) {
    User user =
        userRepository
            .findById(userId)
            .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
    if (nickname.equals(user.getNickname())) {
      return new NicknameChangeResponse(nickname);
    }
    if (userRepository.existsByNickname(nickname)) {
      throw new BusinessException(ErrorCode.NICKNAME_TAKEN);
    }

    user.changeNickname(nickname);
    saveNickname();
    return new NicknameChangeResponse(user.getNickname());
  }

  /** 사전 검사와 저장 사이에 다른 요청이 같은 닉네임을 먼저 가져가면 DB 제약이 저장을 막는다. 그 경우도 중복 닉네임으로 응답하려고 여기서 저장을 내보낸다. */
  private void saveNickname() {
    try {
      userRepository.flush();
    } catch (DataIntegrityViolationException exception) {
      if (exception.getCause() instanceof ConstraintViolationException violation
          && User.NICKNAME_UNIQUE_CONSTRAINT.equals(violation.getConstraintName())) {
        throw new BusinessException(ErrorCode.NICKNAME_TAKEN, exception);
      }
      throw exception;
    }
  }
}
