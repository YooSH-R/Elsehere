package com.elsehere.world.dto;

/**
 * 사용자의 지금 게임 시각.
 *
 * @param day 게임 날짜. 1일차부터 센다
 * @param minuteOfDay 하루 중 분(0~1439). 480이면 오전 8시
 * @param paused 진행 중인 대화가 있어 시간이 멈췄는지
 */
public record GameClockResponse(int day, int minuteOfDay, boolean paused) {}
