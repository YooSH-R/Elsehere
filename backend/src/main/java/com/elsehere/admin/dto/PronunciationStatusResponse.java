package com.elsehere.admin.dto;

/**
 * 사물 단어 발음 만들기 진행 상황.
 *
 * @param total 사물 단어 수
 * @param ready 발음 파일이 있는 단어 수
 * @param missing 발음 파일이 없는 단어 수
 * @param running 지금 만드는 중인지
 * @param lastRun 서버가 시작한 뒤 마지막으로 끝난 실행의 결과. 없으면 null
 */
public record PronunciationStatusResponse(
    int total, int ready, int missing, boolean running, PronunciationRun lastRun) {}
