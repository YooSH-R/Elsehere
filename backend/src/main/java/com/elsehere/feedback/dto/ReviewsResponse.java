package com.elsehere.feedback.dto;

import java.util.List;

/** 잘한 점(good)과 아쉬운 점(improve). */
public record ReviewsResponse(List<String> good, List<String> improve) {}
