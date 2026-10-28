package com.elsehere.common.config;

import org.springframework.context.annotation.Configuration;
import org.springframework.scheduling.annotation.EnableScheduling;

/** 정해진 시각에 도는 작업(NPC 음성 보관 정리)을 켠다. 실행기는 Spring Boot 기본 스케줄러를 쓴다. */
@Configuration
@EnableScheduling
public class SchedulingConfig {}
