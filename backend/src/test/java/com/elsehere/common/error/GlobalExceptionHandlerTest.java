package com.elsehere.common.error;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.webmvc.test.autoconfigure.AutoConfigureMockMvc;
import org.springframework.boot.webmvc.test.autoconfigure.WebMvcTest;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;

// 보안 필터는 SecurityConfigTest에서 검증하므로 여기서는 끄고 오류 응답 형식만 본다.
@WebMvcTest(controllers = ErrorTestController.class)
@AutoConfigureMockMvc(addFilters = false)
class GlobalExceptionHandlerTest {

  @Autowired private MockMvc mockMvc;

  @Test
  @DisplayName("업무 예외는 오류 코드의 상태와 공통 오류 형식으로 응답한다")
  void businessException_respondsWithErrorCodeStatusAndBody() throws Exception {
    mockMvc
        .perform(get("/test/errors/not-found"))
        .andExpect(status().isNotFound())
        .andExpect(jsonPath("$.error.code").value("NOT_FOUND"))
        .andExpect(jsonPath("$.error.message").value(ErrorCode.NOT_FOUND.getMessage()))
        .andExpect(jsonPath("$.error.details").doesNotExist());
  }

  @Test
  @DisplayName("업무 예외에 상세 정보가 있으면 details에 담아 응답한다")
  void businessExceptionWithDetails_includesDetails() throws Exception {
    mockMvc
        .perform(get("/test/errors/with-details"))
        .andExpect(status().isNotFound())
        .andExpect(jsonPath("$.error.code").value("NOT_FOUND"))
        .andExpect(jsonPath("$.error.details.targetId").value(7));
  }

  @Test
  @DisplayName("업무 예외에 상황별 문구가 있으면 오류 코드의 기본 문구 대신 그 문구로 응답한다")
  void businessExceptionWithMessage_respondsThatMessage() throws Exception {
    mockMvc
        .perform(get("/test/errors/with-message"))
        .andExpect(status().isTooManyRequests())
        .andExpect(jsonPath("$.error.code").value("AI_QUOTA_EXHAUSTED"))
        .andExpect(jsonPath("$.error.message").value("게스트는 NPC와 3번까지 대화할 수 있어요."))
        .andExpect(jsonPath("$.error.details.targetId").value(7));
  }

  @Test
  @DisplayName("요청 값 검증에 실패하면 400 VALIDATION_FAILED로 응답한다")
  void invalidRequestBody_respondsValidationFailed() throws Exception {
    mockMvc
        .perform(
            post("/test/errors/validation")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"name\": \"\"}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"))
        .andExpect(jsonPath("$.error.message").value(ErrorCode.VALIDATION_FAILED.getMessage()));
  }

  @Test
  @DisplayName("JSON 형식이 잘못된 요청은 400 VALIDATION_FAILED로 응답한다")
  void malformedJson_respondsValidationFailed() throws Exception {
    mockMvc
        .perform(
            post("/test/errors/validation")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"name\": "))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }

  @Test
  @DisplayName("없는 경로는 404 NOT_FOUND로 응답한다")
  void unknownPath_respondsNotFound() throws Exception {
    mockMvc
        .perform(get("/test/errors/unknown-path"))
        .andExpect(status().isNotFound())
        .andExpect(jsonPath("$.error.code").value("NOT_FOUND"));
  }

  @Test
  @DisplayName("예상하지 못한 예외는 내부 메시지를 숨기고 500 INTERNAL_ERROR로 응답한다")
  void unexpectedException_respondsInternalErrorWithoutDetails() throws Exception {
    mockMvc
        .perform(get("/test/errors/unexpected"))
        .andExpect(status().isInternalServerError())
        .andExpect(jsonPath("$.error.code").value("INTERNAL_ERROR"))
        .andExpect(jsonPath("$.error.message").value(ErrorCode.INTERNAL_ERROR.getMessage()));
  }

  @Test
  @DisplayName("지원하지 않는 메서드처럼 Spring이 상태를 정한 요청 오류는 그 상태와 상태 이름 코드로 응답한다")
  void unsupportedMethod_keepsStatusDecidedBySpring() throws Exception {
    mockMvc
        .perform(delete("/test/errors/not-found"))
        .andExpect(status().isMethodNotAllowed())
        .andExpect(jsonPath("$.error.code").value("METHOD_NOT_ALLOWED"));
  }
}
