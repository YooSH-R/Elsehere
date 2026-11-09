package com.elsehere.auth.controller;

import static org.assertj.core.api.Assertions.assertThat;
import static org.hamcrest.Matchers.containsString;
import static org.hamcrest.Matchers.not;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.redirectedUrl;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.auth.service.ConsentCatalog;
import com.elsehere.auth.service.ConsentTerm;
import com.elsehere.common.config.SecuredWebMvcTest;
import com.elsehere.user.entity.ConsentType;
import java.util.Set;
import java.util.regex.Pattern;
import java.util.stream.Collectors;
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

  private static final Pattern LEGAL_LINK =
      Pattern.compile("href=\"(/(?:terms|privacy|voice|ai-training)/[^\"]+)\"");

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

  @ParameterizedTest
  @MethodSource("documentedTerms")
  @DisplayName("약관 문서끼리의 링크는 문서마다 버전이 달라도 각 문서의 지금 버전 주소로 간다")
  void document_linksToCurrentVersionOfEachDocument(ConsentTerm term) throws Exception {
    String html = mockMvc.perform(get(term.url())).andReturn().getResponse().getContentAsString();
    Set<String> currentUrls = documentedTerms().map(ConsentTerm::url).collect(Collectors.toSet());

    assertThat(LEGAL_LINK.matcher(html).results().map(link -> link.group(1)).toList())
        .isNotEmpty()
        .isSubsetOf(currentUrls);
  }

  @ParameterizedTest
  @MethodSource("documentedTerms")
  @DisplayName("버전 없는 약관 주소는 로그인하지 않아도 그 약관의 지금 버전 주소로 보낸다. 게임 화면은 이 주소로 연결해 버전이 바뀌어도 고치지 않는다")
  void document_withoutVersion_redirectsToCurrentVersion(ConsentTerm term) throws Exception {
    String withoutVersion = term.url().substring(0, term.url().lastIndexOf('/'));

    mockMvc
        .perform(get(withoutVersion))
        .andExpect(status().isFound())
        .andExpect(redirectedUrl(term.url()));
  }

  @Test
  @DisplayName("개인정보 처리방침은 이용 기록으로 접속한 날짜, 장소 화면에 머문 시간, 게임 시간을 처리한다고 밝힌다")
  void privacy_disclosesVisitDatesAndStayedTime() throws Exception {
    mockMvc
        .perform(get(new ConsentCatalog().currentTerm(ConsentType.PRIVACY).url()))
        .andExpect(status().isOk())
        .andExpect(content().string(containsString("접속한 날짜")))
        .andExpect(content().string(containsString("머문 시간")))
        .andExpect(content().string(containsString("게임 시간")));
  }

  @Test
  @DisplayName("개인정보 처리방침은 Google 애널리틱스의 분석 쿠키, 수집 항목, Google(미국) 제공, 거부 방법을 밝힌다")
  void privacy_disclosesGoogleAnalytics() throws Exception {
    mockMvc
        .perform(get(new ConsentCatalog().currentTerm(ConsentType.PRIVACY).url()))
        .andExpect(status().isOk())
        .andExpect(content().string(containsString("Google 애널리틱스")))
        .andExpect(content().string(containsString("분석 쿠키")))
        .andExpect(content().string(containsString("로딩 완료·실패")))
        .andExpect(content().string(containsString("Google LLC")))
        .andExpect(content().string(containsString("tools.google.com/dlpage/gaoptout")))
        .andExpect(content().string(not(containsString("광고·분석 쿠키는 쓰지 않습니다"))));
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
