package com.elsehere.admin.dto;

/**
 * 어드민 페르소나 화면에 보여 줄 NPC. 이름·역할 이름·장소·목소리는 시드(world.sql)가 관리하므로 보여 주기만 하고, 페르소나(role·facts·scene)만
 * 고친다.
 */
public record AdminNpcDetail(
    String npcCode,
    String name,
    String roleLabel,
    String sceneName,
    String voiceId,
    String role,
    String facts,
    String scene) {}
