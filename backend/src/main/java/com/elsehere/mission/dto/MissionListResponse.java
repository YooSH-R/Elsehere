package com.elsehere.mission.dto;

import java.time.LocalDate;
import java.time.OffsetDateTime;
import java.util.List;

/**
 * MIS-01 오늘의 미션 목록.
 *
 * @param date 오늘 날짜(Asia/Seoul)
 * @param resetsAt 미션이 바뀌는 다음 자정(Asia/Seoul)
 */
public record MissionListResponse(
    LocalDate date, OffsetDateTime resetsAt, List<MissionResponse> missions) {}
