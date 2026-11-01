package com.elsehere.vocab.controller;

import static org.hamcrest.Matchers.nullValue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.BDDMockito.given;
import static org.mockito.BDDMockito.then;
import static org.mockito.BDDMockito.willThrow;
import static org.mockito.Mockito.never;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.patch;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.common.auth.CsrfHeaderFilter;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.common.error.BusinessException;
import com.elsehere.common.error.ErrorCode;
import com.elsehere.vocab.dto.VocabItemResponse;
import com.elsehere.vocab.dto.VocabListResponse;
import com.elsehere.vocab.dto.VocabSaveResponse;
import com.elsehere.vocab.entity.UserVocabSourceType;
import com.elsehere.vocab.entity.VocabularyType;
import com.elsehere.vocab.service.VocabService;
import java.time.OffsetDateTime;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.MediaType;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;

// 단어장 규칙은 VocabServiceTest에서 DB와 함께 검증하고, 여기서는 요청·응답 연결과 형식을 본다.
@SecuredWebMvcTest(controllers = VocabController.class)
class VocabControllerTest {

  private static final VocabItemResponse SOFA =
      new VocabItemResponse(
          21L,
          new VocabItemResponse.Word(77L, VocabularyType.WORD, "sofa", "소파"),
          new VocabItemResponse.Context("He is sitting on the sofa.", "그는 소파에 앉아 있어요."),
          "앉을 때 쓰기",
          false,
          OffsetDateTime.parse("2026-10-27T10:00:00+09:00"),
          new VocabItemResponse.Source(UserVocabSourceType.OBJECT, null, null));

  private static final VocabItemResponse LATTE =
      new VocabItemResponse(
          22L,
          new VocabItemResponse.Word(78L, VocabularyType.WORD, "latte", "라떼"),
          new VocabItemResponse.Context("Would you like an iced latte?", "아이스 라떼 드릴까요?"),
          null,
          false,
          OffsetDateTime.parse("2026-10-27T10:00:00+09:00"),
          new VocabItemResponse.Source(UserVocabSourceType.NPC_LINE, "Mia", 58L));

  @Autowired private MockMvc mockMvc;

  @MockitoBean private VocabService vocabService;

  @Test
  @DisplayName("GET /api/v1/vocab은 기본값(외운 단어 제외, 20개)으로 조회하고 명세의 필드 이름으로 돌려준다")
  void getVocab_withDefaults_respondsVocabList() throws Exception {
    given(vocabService.getVocab(1L, false, null, 20))
        .willReturn(new VocabListResponse(new VocabListResponse.Counts(1), List.of(SOFA), null));

    mockMvc
        .perform(get("/api/v1/vocab").with(authentication(loggedIn(1L))))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.counts.all").value(1))
        .andExpect(jsonPath("$.items[0].userVocabId").value(21))
        .andExpect(jsonPath("$.items[0].vocab.vocabId").value(77))
        .andExpect(jsonPath("$.items[0].vocab.type").value("WORD"))
        .andExpect(jsonPath("$.items[0].vocab.text").value("sofa"))
        .andExpect(jsonPath("$.items[0].vocab.meaningKo").value("소파"))
        .andExpect(jsonPath("$.items[0].context.text").value("He is sitting on the sofa."))
        .andExpect(jsonPath("$.items[0].context.meaningKo").value("그는 소파에 앉아 있어요."))
        .andExpect(jsonPath("$.items[0].memo").value("앉을 때 쓰기"))
        .andExpect(jsonPath("$.items[0].memorized").value(false))
        .andExpect(jsonPath("$.items[0].savedAt").value("2026-10-27T10:00:00+09:00"))
        .andExpect(jsonPath("$.items[0].source.sourceType").value("OBJECT"))
        .andExpect(jsonPath("$.items[0].source.npcName").value(nullValue()))
        .andExpect(jsonPath("$.items[0].source.conversationId").value(nullValue()))
        .andExpect(jsonPath("$.nextCursor").value(nullValue()));
  }

  @Test
  @DisplayName("사물 카드 단어를 새로 저장하면 201과 함께 단어장 항목에 created=true를 더해 돌려준다")
  void saveObjectWord_new_respondsCreated() throws Exception {
    given(vocabService.saveObjectWord(1L, "sofa")).willReturn(new VocabSaveResponse(SOFA, true));

    mockMvc
        .perform(
            stateChanging(post("/api/v1/vocab"))
                .contentType(MediaType.APPLICATION_JSON)
                .content(
                    "{\"requestId\": \"5b0c0a52-1c1e-4f7a-9a51-7f1d4c2e9b10\","
                        + " \"sourceType\": \"OBJECT\", \"wordKey\": \"sofa\"}"))
        .andExpect(status().isCreated())
        .andExpect(jsonPath("$.created").value(true))
        .andExpect(jsonPath("$.userVocabId").value(21))
        .andExpect(jsonPath("$.vocab.text").value("sofa"))
        .andExpect(jsonPath("$.context.text").value("He is sitting on the sofa."));
  }

  @Test
  @DisplayName("이미 저장한 사물 카드 단어를 다시 저장하면 200과 함께 created=false로 돌려준다")
  void saveObjectWord_existing_respondsOk() throws Exception {
    given(vocabService.saveObjectWord(1L, "sofa")).willReturn(new VocabSaveResponse(SOFA, false));

    mockMvc
        .perform(
            stateChanging(post("/api/v1/vocab"))
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"sourceType\": \"OBJECT\", \"wordKey\": \"sofa\"}"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.created").value(false))
        .andExpect(jsonPath("$.userVocabId").value(21));
  }

  @Test
  @DisplayName("PATCH /api/v1/vocab/{userVocabId}는 보낸 메모·외웠어요 값으로 바꾸고 바뀐 항목을 돌려준다")
  void updateVocab_respondsUpdatedItem() throws Exception {
    given(vocabService.updateVocab(1L, 21L, "앉을 때 쓰기", true)).willReturn(SOFA);

    mockMvc
        .perform(
            stateChanging(patch("/api/v1/vocab/21"))
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"memo\": \"앉을 때 쓰기\", \"memorized\": true}"))
        .andExpect(status().isOk())
        .andExpect(jsonPath("$.userVocabId").value(21))
        .andExpect(jsonPath("$.memo").value("앉을 때 쓰기"));
  }

  @Test
  @DisplayName("DELETE /api/v1/vocab/{userVocabId}는 로그인한 사용자의 항목을 지우고 204로 응답한다")
  void deleteVocab_respondsNoContent() throws Exception {
    mockMvc.perform(stateChanging(delete("/api/v1/vocab/21"))).andExpect(status().isNoContent());

    then(vocabService).should().deleteVocab(1L, 21L);
  }

  @Test
  @DisplayName("NPC 대사 단어 풀이의 단어를 새로 저장하면 201과 함께 출처 NPC 이름·대화를 담아 돌려준다")
  void saveNpcLineWord_new_respondsCreated() throws Exception {
    given(vocabService.saveNpcLineWord(1L, 105L, "latte"))
        .willReturn(new VocabSaveResponse(LATTE, true));

    mockMvc
        .perform(
            stateChanging(post("/api/v1/vocab"))
                .contentType(MediaType.APPLICATION_JSON)
                .content(
                    "{\"requestId\": \"5b0c0a52-1c1e-4f7a-9a51-7f1d4c2e9b10\","
                        + " \"sourceType\": \"NPC_LINE\", \"turnId\": 105, \"text\": \"latte\"}"))
        .andExpect(status().isCreated())
        .andExpect(jsonPath("$.created").value(true))
        .andExpect(jsonPath("$.vocab.text").value("latte"))
        .andExpect(jsonPath("$.context.text").value("Would you like an iced latte?"))
        .andExpect(jsonPath("$.source.sourceType").value("NPC_LINE"))
        .andExpect(jsonPath("$.source.npcName").value("Mia"))
        .andExpect(jsonPath("$.source.conversationId").value(58));
  }

  @Test
  @DisplayName("피드백에서 저장하는 단어는 아직 단어장 후보를 확인할 수 없어 422 VOCAB_SOURCE_MISMATCH로 거절한다")
  void saveFeedbackWord_isRejected() throws Exception {
    mockMvc
        .perform(
            stateChanging(post("/api/v1/vocab"))
                .contentType(MediaType.APPLICATION_JSON)
                .content(
                    "{\"sourceType\": \"FEEDBACK\", \"conversationId\": 58, \"text\": \"order\"}"))
        .andExpect(status().is(422))
        .andExpect(jsonPath("$.error.code").value("VOCAB_SOURCE_MISMATCH"));

    then(vocabService).should(never()).saveObjectWord(anyLong(), any());
    then(vocabService).should(never()).saveNpcLineWord(anyLong(), anyLong(), any());
  }

  @ParameterizedTest
  @ValueSource(
      strings = {
        "{\"wordKey\": \"sofa\"}",
        "{\"sourceType\": \"OBJECT\"}",
        "{\"sourceType\": \"OBJECT\", \"wordKey\": \" \"}",
        "{\"sourceType\": \"NPC_LINE\", \"text\": \"latte\"}",
        "{\"sourceType\": \"NPC_LINE\", \"turnId\": 105}",
        "{\"sourceType\": \"NPC_LINE\", \"turnId\": 105, \"text\": \" \"}"
      })
  @DisplayName("저장 출처가 없거나, 사물 저장인데 단어 키가 없거나, NPC 대사 저장인데 턴·단어가 없으면 400 VALIDATION_FAILED로 응답한다")
  void saveWithoutRequiredField_isRejected(String body) throws Exception {
    mockMvc
        .perform(
            stateChanging(post("/api/v1/vocab"))
                .contentType(MediaType.APPLICATION_JSON)
                .content(body))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));

    then(vocabService).should(never()).saveObjectWord(anyLong(), any());
    then(vocabService).should(never()).saveNpcLineWord(anyLong(), anyLong(), any());
  }

  @Test
  @DisplayName("메모가 200자를 넘으면 400 VALIDATION_FAILED로 응답하고 바꾸지 않는다")
  void updateWithTooLongMemo_isRejected() throws Exception {
    String memo = "가".repeat(201);

    mockMvc
        .perform(
            stateChanging(patch("/api/v1/vocab/21"))
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"memo\": \"" + memo + "\"}"))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));

    then(vocabService).should(never()).updateVocab(anyLong(), anyLong(), any(), any());
  }

  @Test
  @DisplayName("없는 단어 키로 저장하면 404 NOT_FOUND 오류 응답을 돌려준다")
  void saveUnknownObject_respondsNotFound() throws Exception {
    given(vocabService.saveObjectWord(1L, "moon_rock"))
        .willThrow(new BusinessException(ErrorCode.NOT_FOUND));

    mockMvc
        .perform(
            stateChanging(post("/api/v1/vocab"))
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"sourceType\": \"OBJECT\", \"wordKey\": \"moon_rock\"}"))
        .andExpect(status().isNotFound())
        .andExpect(jsonPath("$.error.code").value("NOT_FOUND"));
  }

  @Test
  @DisplayName("잘못된 커서로 목록을 조회하면 400 VALIDATION_FAILED 오류 응답을 돌려준다")
  void getVocabWithInvalidCursor_respondsValidationFailed() throws Exception {
    given(vocabService.getVocab(1L, false, "abc", 20))
        .willThrow(new BusinessException(ErrorCode.VALIDATION_FAILED));

    mockMvc
        .perform(get("/api/v1/vocab?cursor=abc").with(authentication(loggedIn(1L))))
        .andExpect(status().isBadRequest())
        .andExpect(jsonPath("$.error.code").value("VALIDATION_FAILED"));
  }

  @Test
  @DisplayName("내 단어장에 없는 항목을 수정·삭제하면 404 NOT_FOUND 오류 응답을 돌려준다")
  void updateOrDeleteOthersEntry_respondsNotFound() throws Exception {
    given(vocabService.updateVocab(1L, 99L, "메모", null))
        .willThrow(new BusinessException(ErrorCode.NOT_FOUND));
    willThrow(new BusinessException(ErrorCode.NOT_FOUND)).given(vocabService).deleteVocab(1L, 99L);

    mockMvc
        .perform(
            stateChanging(patch("/api/v1/vocab/99"))
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"memo\": \"메모\"}"))
        .andExpect(status().isNotFound())
        .andExpect(jsonPath("$.error.code").value("NOT_FOUND"));
    mockMvc
        .perform(stateChanging(delete("/api/v1/vocab/99")))
        .andExpect(status().isNotFound())
        .andExpect(jsonPath("$.error.code").value("NOT_FOUND"));
  }

  @Test
  @DisplayName("로그인하지 않으면 단어장 조회·저장 모두 401 UNAUTHENTICATED로 응답한다")
  void notLoggedIn_respondsUnauthenticated() throws Exception {
    mockMvc
        .perform(get("/api/v1/vocab"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
    mockMvc
        .perform(
            post("/api/v1/vocab")
                .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"sourceType\": \"OBJECT\", \"wordKey\": \"sofa\"}"))
        .andExpect(status().isUnauthorized())
        .andExpect(jsonPath("$.error.code").value("UNAUTHENTICATED"));
  }

  @Test
  @DisplayName("X-Requested-With 헤더가 없으면 단어 저장·수정·삭제를 403 CSRF_HEADER_REQUIRED로 거절한다")
  void withoutCsrfHeader_isRejected() throws Exception {
    mockMvc
        .perform(
            post("/api/v1/vocab")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"sourceType\": \"OBJECT\", \"wordKey\": \"sofa\"}")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("CSRF_HEADER_REQUIRED"));
    mockMvc
        .perform(
            patch("/api/v1/vocab/21")
                .contentType(MediaType.APPLICATION_JSON)
                .content("{\"memorized\": true}")
                .with(authentication(loggedIn(1L))))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("CSRF_HEADER_REQUIRED"));
    mockMvc
        .perform(delete("/api/v1/vocab/21").with(authentication(loggedIn(1L))))
        .andExpect(status().isForbidden())
        .andExpect(jsonPath("$.error.code").value("CSRF_HEADER_REQUIRED"));
  }

  private static MockHttpServletRequestBuilder stateChanging(
      MockHttpServletRequestBuilder request) {
    return request
        .header(CsrfHeaderFilter.HEADER_NAME, "ElsehereClient")
        .with(authentication(loggedIn(1L)));
  }

  private static Authentication loggedIn(Long userId) {
    return UsernamePasswordAuthenticationToken.authenticated(
        new LoginUser(userId), null, List.of());
  }
}
