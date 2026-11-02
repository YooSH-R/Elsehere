package com.elsehere.dialogue.dto;

/** CONV-02 음성 인식 응답. */
public record TranscriptionResponse(String sttRawText, long durationMs) {}
