package com.elsehere.onboarding.service;

/**
 * 학습 언어 하나.
 *
 * @param available 지금 고를 수 있는지. {@code false}면 화면에 "준비 중"으로 보인다.
 */
public record Language(String languageCode, String name, String description, boolean available) {}
