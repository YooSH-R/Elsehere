package com.elsehere.mission.service;

import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

/** 시연 전용 환경에서만 명시적으로 켠다. 기본 환경·dev 배포 설정은 변경하지 않는다. */
@Component
public record ShowcaseSettings(@Value("${elsehere.showcase.enabled:false}") boolean enabled) {}
