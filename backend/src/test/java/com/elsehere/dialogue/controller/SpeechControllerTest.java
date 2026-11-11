package com.elsehere.dialogue.controller;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.multipart;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.conversation.dto.TranscriptionPurpose;
import com.elsehere.dialogue.dto.SpeechResponse;
import com.elsehere.dialogue.dto.TranscriptionResponse;
import com.elsehere.dialogue.service.SpeechService;
import java.util.List;
import java.util.UUID;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.mock.web.MockMultipartFile;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

// 녹음 검사·음성 저장 규칙은 SpeechServiceTest에서 검증하고, 여기서는 multipart·음성 응답 연결만 본다.
@SecuredWebMvcTest(controllers = SpeechController.class)
class SpeechControllerTest {

  private static final UUID REQUEST_ID = UUID.fromString("00000000-0000-0000-0000-000000000001");
  private static final byte[] WAV = {82, 73, 70, 70};

  @Autowired private MockMvc mockMvc;

  @MockitoBean private SpeechService speechService;

  @Test
  @DisplayName("POST /api/v1/speech/transcriptions는 multipart 녹음을 받아 전사 문장과 길이를 돌려준다")
  void transcribe_respondsText() throws Exception {
    given(
            speechService.transcribe(
                eq(1L), eq(REQUEST_ID), eq(TranscriptionPurpose.TURN), eq(5L), any()))
        .willReturn(new TranscriptionResponse("I want a latte", 1250));

    mockMvc
        .perform(
            multipart("/api/v1/speech/transcriptions")
                .file(new MockMultipartFile("audio", "speech.wav", "audio/wav", WAV))
                .param("requestId", REQUEST_ID.toString())
                .param("purpose", "TURN")
                .param("conversationId", "5")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.sttRawText").value("I want a latte"))
        .andExpect(jsonPath("$.durationMs").value(1250));
  }

  @Test
  @DisplayName("녹음 파일 부분이 없으면 400 VALIDATION_FAILED다")
  void transcribe_withoutAudio_isRejected() throws Exception {
    mockMvc
        .perform(
            multipart("/api/v1/speech/transcriptions")
                .param("requestId", REQUEST_ID.toString())
                .param("purpose", "TUTORIAL")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }

  @Test
  @DisplayName("받지 않는 녹음 형식이면 415 AUDIO_FORMAT_UNSUPPORTED다")
  void transcribe_unsupportedFormat_respondsUnsupported() throws Exception {
    given(speechService.transcribe(eq(1L), eq(REQUEST_ID), any(), any(), any()))
        .willThrow(new BusinessException(ErrorCode.AUDIO_FORMAT_UNSUPPORTED));

    mockMvc
        .perform(
            multipart("/api/v1/speech/transcriptions")
                .file(new MockMultipartFile("audio", "speech.mp4", "video/mp4", WAV))
                .param("requestId", REQUEST_ID.toString())
                .param("purpose", "TUTORIAL")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isUnsupportedMediaType())
        .andExpect(jsonPath("$.error.code").value("AUDIO_FORMAT_UNSUPPORTED"));
  }

  @Test
  @DisplayName("GET /api/v1/turns/{turnId}/speech는 저장한 NPC 음성을 audio/wav로 돌려준다")
  void npcSpeech_respondsWav() throws Exception {
    given(speechService.npcSpeech(1L, 12L)).willReturn(WAV);

    mockMvc
        .perform(get("/api/v1/turns/12/speech").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(content().contentType("audio/wav"))
        .andExpect(content().bytes(WAV));
  }

  @Test
  @DisplayName("POST /api/v1/turns/{turnId}/speech는 다시 만든 음성의 상태와 주소를 돌려준다")
  void regenerateSpeech_respondsSpeech() throws Exception {
    given(speechService.regenerateSpeech(1L, 12L, REQUEST_ID, 1.1))
        .willReturn(SpeechResponse.of(12L, "a.wav"));

    mockMvc
        .perform(
            post("/api/v1/turns/12/speech")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"requestId\": \"" + REQUEST_ID + "\"}")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.status").value("READY"))
        .andExpect(jsonPath("$.url").value("/api/v1/turns/12/speech"));
  }

  @Test
  @DisplayName("음성 다시 만들기에 실은 NPC 말 빠르기를 서비스에 넘긴다")
  void regenerateSpeech_withSpeechSpeed_passesSpeechSpeed() throws Exception {
    given(speechService.regenerateSpeech(1L, 12L, REQUEST_ID, 1.3))
        .willReturn(SpeechResponse.of(12L, "a.wav"));

    mockMvc
        .perform(
            post("/api/v1/turns/12/speech")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"requestId\": \"" + REQUEST_ID + "\", \"speechSpeed\": 1.3}")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.status").value("READY"));
  }

  @Test
  @DisplayName("음성 다시 만들기의 NPC 말 빠르기가 0.8~1.5배 밖이면 400 VALIDATION_FAILED다")
  void regenerateSpeech_speechSpeedOutsideSettingRange_isRejected() throws Exception {
    mockMvc
        .perform(
            post("/api/v1/turns/12/speech")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"requestId\": \"" + REQUEST_ID + "\", \"speechSpeed\": 1.6}")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
