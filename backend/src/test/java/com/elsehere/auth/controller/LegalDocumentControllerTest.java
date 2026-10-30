package com.elsehere.auth.controller;

import static org.hamcrest.Matchers.containsString;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.auth.service.ConsentCatalog;
import com.elsehere.auth.service.ConsentTerm;
import com.elsehere.common.config.SecuredWebMvcTest;
import java.util.stream.Stream;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.MethodSource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;

@SecuredWebMvcTest(controllers = LegalDocumentController.class)
@Import(ConsentCatalog.class)
class LegalDocumentControllerTest {

  @Autowired private MockMvc mockMvc;

  static Stream<ConsentTerm> documentedTerms() {
    return new ConsentCatalog().currentTerms().stream().filter(term -> term.url() != null);
  }

  @ParameterizedTest
  @MethodSource("documentedTerms")
  @DisplayName("가입 화면이 받는 약관 주소는 로그인하지 않아도 그 약관 제목과 버전이 적힌 문서로 열린다")
  void document_forCurrentTerm_rendersWithoutLogin(ConsentTerm term) throws Exception {
    mockMvc
        .perform(get(term.url()))
        .andExpect(status().isOk())
        .andExpect(content().contentTypeCompatibleWith(MediaType.TEXT_HTML))
        .andExpect(content().string(containsString(term.title())))
        .andExpect(content().string(containsString("버전 " + term.version())));
  }

  @Test
  @DisplayName("지금 받는 버전이 아닌 약관 주소는 404 NOT_FOUND다")
  void document_forOtherVersion_respondsNotFound() throws Exception {
    mockMvc
        .perform(get("/terms/2025-01"))
        .andExpect(status().isNotFound())
        .andExpect(jsonPath("$.error.code").value("NOT_FOUND"));
  }
}
