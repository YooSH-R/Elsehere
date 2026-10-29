package com.elsehere.admin.dto;

/** 어드민 NPC 목록의 한 줄. */
public record AdminNpcSummary(String npcCode, String name, String roleLabel, String sceneName) {}
