package com.elsehere.review.dto;

import java.util.List;

/** ID 내림차순 복습 카드 한 페이지. 마지막 페이지의 nextCursor는 null이다. */
public record ReviewCardListResponse(List<ReviewCardResponse> items, String nextCursor) {}
