package com.elsehere.user.entity;

/** 가입할 때 받는 동의 항목. TRAINING_*는 AI 개선(학습·평가)에 쓰는 선택 동의로, 가입 뒤에도 켜고 끌 수 있다. */
public enum ConsentType {
  TERMS,
  PRIVACY,
  AGE14,
  VOICE,
  TRAINING_TEXT,
  TRAINING_VOICE;

  public boolean isTraining() {
    return this == TRAINING_TEXT || this == TRAINING_VOICE;
  }
}
