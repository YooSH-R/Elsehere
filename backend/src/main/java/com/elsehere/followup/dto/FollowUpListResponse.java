package com.elsehere.followup.dto;

import java.util.List;

/** CONV-13 아직 건네지 않은 것. 준비 시각 순서다. */
public record FollowUpListResponse(List<FollowUpResponse> items) {}
