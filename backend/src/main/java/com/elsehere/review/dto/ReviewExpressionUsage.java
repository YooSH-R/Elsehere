package com.elsehere.review.dto;

import java.util.List;

public record ReviewExpressionUsage(String expression, List<ReviewUtterance> evidence) {}
