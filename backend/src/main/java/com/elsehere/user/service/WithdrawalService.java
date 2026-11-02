package com.elsehere.user.service;

import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.user.entity.User;
import com.elsehere.user.repository.UserRepository;
import com.elsehere.user.service.UserContentEraser.ErasedFiles;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.support.TransactionTemplate;

/**
 * 회원 탈퇴(USER-05). 사용자가 남긴 내용(범위는 {@link UserContentEraser})과 이메일·소셜 계정 ID·닉네임 같은 개인정보를 바로 지운다.
 * 사용자·대화·AI 호출 기록 같은 뼈대는 누구 것인지 알 수 없게 남기고, 같은 소셜 계정으로 다시 가입할 수 있다(개인정보 처리방침).
 */
@Slf4j
@Service
@RequiredArgsConstructor
public class WithdrawalService {

  private final UserRepository userRepository;
  private final UserContentEraser userContentEraser;
  private final TransactionTemplate transactionTemplate;

  /** 내용과 개인정보는 한 트랜잭션으로 지우고, 지운 내용이 쓰던 음성 파일은 그 뒤에 지운다. */
  public void withdraw(Long userId) {
    ErasedFiles files =
        transactionTemplate.execute(
            status -> {
              User user =
                  userRepository
                      .findById(userId)
                      .orElseThrow(() -> new BusinessException(ErrorCode.NOT_FOUND));
              ErasedFiles erased = userContentEraser.deleteContent(userId);
              user.withdraw();
              return erased;
            });
    log.info("user_withdrew user_id={}", userId);
    userContentEraser.deleteFiles(files);
  }
}
