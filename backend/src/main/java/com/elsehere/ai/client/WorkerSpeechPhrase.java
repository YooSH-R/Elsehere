package com.elsehere.ai.client;

/** 음성이 실패해도 대사와 함께 보존할 구절별 표정. 시간은 실제 재합성한 PCM으로 다시 정한다. */
public record WorkerSpeechPhrase(String text, String emotion, double intensity) {}
