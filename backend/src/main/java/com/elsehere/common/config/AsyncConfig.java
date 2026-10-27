package com.elsehere.common.config;

import org.springframework.context.annotation.Configuration;
import org.springframework.scheduling.annotation.EnableAsync;

/**
 * 응답을 기다리게 할 필요가 없는 후속 작업(대화 종료 뒤 NPC 기억 갱신)을 요청 스레드 밖에서 실행한다. 실행기는 Spring Boot 기본
 * 실행기(applicationTaskExecutor)를 쓴다.
 */
@Configuration
@EnableAsync
public class AsyncConfig {}
