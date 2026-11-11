package com.elsehere.ai.client;

import java.util.List;

/** 저장 공연의 재생 의도. 대사와 음성은 포함하지 않는다. */
public record PerformanceIntentResult(String intent, List<WorkerUsage> usage) {}
