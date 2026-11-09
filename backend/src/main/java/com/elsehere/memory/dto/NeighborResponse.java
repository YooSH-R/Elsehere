package com.elsehere.memory.dto;

import java.time.OffsetDateTime;
import java.util.List;

/**
 * WORLD-08 이웃 목록의 NPC 하나. 열리지 않은 NPC(unlocked false)는 역할·장소만 담고 나머지는 null(관심사는 빈 목록)이다.
 *
 * @param unlocked 끝까지 마친 대화가 있어 이웃으로 열렸는지
 * @param roleLabel 역할 이름(행인, 카페 직원)
 * @param sceneName NPC가 일하거나 지내는 장소의 화면 이름(마을, 모퉁이 카페)
 * @param occupation 인격 카드의 직업(영어)
 * @param interests 인격 카드의 관심사(영어)
 * @param affinity 지금 호감도(-100~100). 대화 화면(CONV-01·03의 conversation.affinity)과 같은 값이다
 * @param recognition 알아봄(0~100). 얼굴을 아는 정도
 * @param talkCount 사용자 발화가 있었던 대화를 끝낸 횟수
 * @param lastTalkedAt 마지막으로 대화를 끝낸 시각
 */
public record NeighborResponse(
    String npcCode,
    boolean unlocked,
    String name,
    String roleLabel,
    String sceneCode,
    String sceneName,
    String occupation,
    List<String> interests,
    Integer affinity,
    Integer recognition,
    Integer talkCount,
    OffsetDateTime lastTalkedAt) {}
