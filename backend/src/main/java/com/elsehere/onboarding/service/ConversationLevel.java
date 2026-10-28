package com.elsehere.onboarding.service;

/**
 * NPC 대화 난이도 한 단계.
 *
 * @param name 화면에 보여주는 단계 이름(초급·중급·고급)
 * @param available 지금 고를 수 있는지. {@code false}면 화면에 "추후 제공"으로 보인다.
 * @param description 단계 설명. 아직 제공하지 않는 단계는 {@code null}이다.
 */
public record ConversationLevel(int level, String name, boolean available, String description) {}
