package com.elsehere.admin.dto;

/** 저장한 NPC 페르소나. 비운 값은 빈 문자열이다. */
public record PersonaResponse(String npcCode, String role, String facts, String scene) {}
