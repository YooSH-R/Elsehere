package com.elsehere.auth.dto;

/** 게스트로 시작하기 전에 보여 줄 안내(AUTH-08). conversationLimit은 게스트 한 계정이 시작할 수 있는 NPC 대화 수다. */
public record GuestGuideResponse(int conversationLimit) {}
