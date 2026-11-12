package com.elsehere.admin.controller;

import static org.hamcrest.Matchers.containsString;
import static org.hamcrest.Matchers.not;
import static org.mockito.BDDMockito.given;
import static org.springframework.security.test.web.servlet.request.SecurityMockMvcRequestPostProcessors.authentication;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.redirectedUrl;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import com.elsehere.admin.dto.AdminNpcDetail;
import com.elsehere.admin.dto.AdminNpcSummary;
import com.elsehere.admin.dto.AiUsageReport;
import com.elsehere.admin.dto.AiUsageRow;
import com.elsehere.admin.dto.AiUserUsage;
import com.elsehere.admin.dto.OverallUsageReport;
import com.elsehere.admin.dto.UsageReport;
import com.elsehere.admin.dto.UserUsageReport;
import com.elsehere.admin.service.AdminAiUsageService;
import com.elsehere.admin.service.AdminLogin;
import com.elsehere.admin.service.AdminNpcService;
import com.elsehere.admin.service.AdminUsageService;
import com.elsehere.common.auth.LoginUser;
import com.elsehere.common.config.SecuredWebMvcTest;
import java.util.List;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.test.context.bean.override.mockito.MockitoBean;
import org.springframework.test.web.servlet.MockMvc;

@SecuredWebMvcTest(controllers = AdminPageController.class)
class AdminPageControllerTest {

  @Autowired private MockMvc mockMvc;

  @MockitoBean private AdminNpcService adminNpcService;

  @MockitoBean private AdminAiUsageService adminAiUsageService;

  @MockitoBean private AdminUsageService adminUsageService;

  @Test
  @DisplayName("AI 사용량 화면은 고른 기간의 합계와 날짜·종류·모델·사용자 구분·사용자별 사용량을 보여 준다")
  void aiUsage_byAdmin_showsReport() throws Exception {
    given(adminAiUsageService.report(30))
        .willReturn(
            new AiUsageReport(
                30,
                new AiUsageRow("전체", 2, 5, 1, 12345, 678, 1500),
                List.of(new AiUsageRow("2030-01-10", 2, 5, 1, 12345, 678, 1500)),
                List.of(new AiUsageRow("JUDGE", 2, 5, 1, 12345, 678, 0)),
                List.of(new AiUsageRow("gpt-4o-mini", 2, 4, 0, 12345, 678, 0)),
                List.of(new AiUsageRow("GUEST", 1, 3, 1, 10000, 500, 0)),
                List.of(new AiUserUsage(7L, true, 3, 1, 10000, 500))));

    mockMvc
        .perform(get("/admin/ai-usage").param("days", "30").with(authentication(adminLogin())))
        .andExpect(status().isOk())
        .andExpect(content().string(containsString("href=\"/admin/ai-usage\"")))
        .andExpect(content().string(containsString("최근 30일")))
        .andExpect(content().string(containsString("12,345")))
        .andExpect(content().string(containsString("2030-01-10")))
        .andExpect(content().string(containsString("의도 판정")))
        .andExpect(content().string(containsString("gpt-4o-mini")))
        .andExpect(content().string(containsString("게스트")));
  }

  @Test
  @DisplayName("AI 사용량 화면은 기간을 고르지 않으면 최근 7일을 보여 준다")
  void aiUsage_withoutDays_showsLastWeek() throws Exception {
    AiUsageRow empty = new AiUsageRow("전체", 0, 0, 0, 0, 0, 0);
    given(adminAiUsageService.report(7))
        .willReturn(
            new AiUsageReport(7, empty, List.of(), List.of(), List.of(), List.of(), List.of()));

    mockMvc
        .perform(get("/admin/ai-usage").with(authentication(adminLogin())))
        .andExpect(status().isOk())
        .andExpect(content().string(containsString("최근 7일")));
  }

  @Test
  @DisplayName("이용 현황 화면은 고른 기간의 합계·날짜별·대화·학습·진행 단계와 사용자별 화면 링크가 있는 사용자 목록을 보여 준다")
  void usage_byAdmin_showsReport() throws Exception {
    given(adminUsageService.report(30))
        .willReturn(
            new OverallUsageReport(
                usageReport(30),
                new OverallUsageReport.Progress(
                    List.of(new OverallUsageReport.UserCount("TUTORIAL", 3)),
                    List.of(new OverallUsageReport.UserCount("1. 호텔에 도착했어요", 4)),
                    List.of(new OverallUsageReport.UserCount("4. WASH", 1))),
                new OverallUsageReport.Retention(0, 0, 0, 0, 0, 0),
                List.of(
                    new OverallUsageReport.UserActivity(
                        7L, true, 2, 3, 1, 1800, "2030-01-10 08:30"))));

    mockMvc
        .perform(get("/admin/usage").param("days", "30").with(authentication(adminLogin())))
        .andExpect(status().isOk())
        .andExpect(content().string(containsString("href=\"/admin/usage\"")))
        .andExpect(content().string(containsString("최근 30일")))
        .andExpect(content().string(containsString("1,234")))
        .andExpect(content().string(containsString("2030-01-09")))
        .andExpect(content().string(containsString("NPC가 마무리")))
        .andExpect(content().string(containsString("Mia · 카페 직원")))
        .andExpect(content().string(containsString("음성으로 말함")))
        .andExpect(content().string(containsString("사물 카드")))
        .andExpect(content().string(containsString("사용자가 건너뜀")))
        .andExpect(content().string(containsString("튜토리얼")))
        .andExpect(content().string(containsString("1. 호텔에 도착했어요")))
        .andExpect(content().string(containsString("4. WASH")))
        .andExpect(
            content().string(containsString("href=\"/admin/usage/user?userId=7&amp;days=30\"")))
        .andExpect(content().string(containsString("2030-01-10 08:30")));
  }

  @Test
  @DisplayName("이용 현황 화면은 하루 평균 머문 시간, 날짜별 머문 시간, D1·D7·7일 안 재방문율을 보여 준다")
  void usage_byAdmin_showsStayedTimeAndRetention() throws Exception {
    given(adminUsageService.report(30))
        .willReturn(
            new OverallUsageReport(
                usageReport(30),
                new OverallUsageReport.Progress(List.of(), List.of(), List.of()),
                new OverallUsageReport.Retention(12, 10, 4, 9, 3, 6),
                List.of()));

    // 활동한 하루 6번 동안 5,400초(하루 15분), 2030-01-09에 2,700초(45분) 머묾.
    // D1 대상 10명 중 4명, D7 대상 9명 중 3명, 7일 안 6명
    mockMvc
        .perform(get("/admin/usage").param("days", "30").with(authentication(adminLogin())))
        .andExpect(status().isOk())
        .andExpect(content().string(containsString("15.0분")))
        .andExpect(content().string(containsString("45.0")))
        .andExpect(content().string(containsString("D1")))
        .andExpect(content().string(containsString("40%")))
        .andExpect(content().string(containsString("33%")))
        .andExpect(content().string(containsString("67%")));
  }

  @Test
  @DisplayName("이용 현황 합계는 비율과 평균을 활동 사용자·대화 수로 나눠 보여 준다")
  void usage_byAdmin_showsRatios() throws Exception {
    given(adminUsageService.report(7))
        .willReturn(
            new OverallUsageReport(
                usageReport(7),
                new OverallUsageReport.Progress(List.of(), List.of(), List.of()),
                new OverallUsageReport.Retention(0, 0, 0, 0, 0, 0),
                List.of()));

    // 활동 사용자 4명 중 다시 온 사용자 1명, 대화 5번 중 끝낸 대화 4번, 대화한 사용자 2명
    mockMvc
        .perform(get("/admin/usage").with(authentication(adminLogin())))
        .andExpect(status().isOk())
        .andExpect(content().string(containsString("최근 7일")))
        .andExpect(content().string(containsString("25%")))
        .andExpect(content().string(containsString("80%")))
        .andExpect(content().string(containsString("2.5번")));
  }

  @Test
  @DisplayName("사용자별 이용 현황 화면은 사용자 ID로 개요·활동과 기간 링크, 전체 화면으로 돌아가는 링크를 보여 준다")
  void userUsage_byAdmin_showsProfileAndActivity() throws Exception {
    given(adminUsageService.userReport(7L, 30))
        .willReturn(
            new UserUsageReport(
                new UserUsageReport.Profile(
                    7L,
                    false,
                    false,
                    "2030-01-07 09:00",
                    "2030-01-10 08:00",
                    "DONE",
                    "모두 마침",
                    "1. DINER",
                    1800,
                    "2030-01-10 08:50"),
                usageReport(30)));

    mockMvc
        .perform(
            get("/admin/usage/user")
                .param("userId", "7")
                .param("days", "30")
                .with(authentication(adminLogin())))
        .andExpect(status().isOk())
        .andExpect(content().string(containsString("사용자 7")))
        .andExpect(content().string(containsString("2030-01-07 09:00")))
        .andExpect(content().string(containsString("2030-01-10 08:00")))
        .andExpect(content().string(containsString("모두 마침")))
        .andExpect(content().string(containsString("1. DINER")))
        .andExpect(content().string(containsString("30.0분")))
        .andExpect(content().string(containsString("NPC가 마무리")))
        .andExpect(
            content().string(containsString("href=\"/admin/usage/user?userId=7&amp;days=7\"")))
        .andExpect(content().string(containsString("href=\"/admin/usage?days=30\"")));
  }

  @Test
  @DisplayName("로그인하지 않고 어드민 화면을 열면 어드민 로그인 화면으로 보낸다")
  void npcs_withoutLogin_redirectsToAdminLogin() throws Exception {
    mockMvc
        .perform(get("/admin/npcs"))
        .andExpect(status().is3xxRedirection())
        .andExpect(redirectedUrl("/admin/login"));
  }

  @Test
  @DisplayName("게임 사용자로 로그인했어도 어드민 화면을 열면 어드민 로그인 화면으로 보낸다")
  void npcs_byGameUser_redirectsToAdminLogin() throws Exception {
    mockMvc
        .perform(get("/admin/npcs").with(authentication(gameLogin())))
        .andExpect(status().is3xxRedirection())
        .andExpect(redirectedUrl("/admin/login"));
  }

  @Test
  @DisplayName("로그인 화면은 로그인하지 않아도 열리고 아이디·비밀번호 칸과 로그인 API 주소를 담는다")
  void login_withoutLogin_showsLoginForm() throws Exception {
    mockMvc
        .perform(get("/admin/login"))
        .andExpect(status().isOk())
        .andExpect(content().string(containsString("data-url=\"/api/v1/admin/login\"")))
        .andExpect(content().string(containsString("name=\"loginId\"")))
        .andExpect(content().string(containsString("type=\"password\"")))
        .andExpect(content().string(not(containsString("href=\"/admin/npcs\""))));
  }

  @Test
  @DisplayName("어드민 첫 화면은 NPC 목록으로 보낸다")
  void home_redirectsToNpcs() throws Exception {
    mockMvc
        .perform(get("/admin").with(authentication(adminLogin())))
        .andExpect(status().is3xxRedirection())
        .andExpect(redirectedUrl("/admin/npcs"));
  }

  @Test
  @DisplayName("관리자는 NPC 목록에서 이름·역할 이름·장소와 페르소나 화면 링크를 본다")
  void npcs_byAdmin_listsNpcs() throws Exception {
    given(adminNpcService.npcs())
        .willReturn(List.of(new AdminNpcSummary("CAFE_STAFF", "Mia", "카페 직원", "모퉁이 카페")));

    mockMvc
        .perform(get("/admin/npcs").with(authentication(adminLogin())))
        .andExpect(status().isOk())
        .andExpect(content().string(containsString("Mia")))
        .andExpect(content().string(containsString("카페 직원")))
        .andExpect(content().string(containsString("모퉁이 카페")))
        .andExpect(content().string(containsString("href=\"/admin/npcs/CAFE_STAFF\"")));
  }

  @Test
  @DisplayName("관리자는 NPC 페르소나 화면에서 지금 값과 저장할 API 주소를 본다")
  void npc_byAdmin_showsCurrentPersona() throws Exception {
    given(adminNpcService.npc("CAFE_STAFF"))
        .willReturn(
            new AdminNpcDetail(
                "CAFE_STAFF",
                "Mia",
                "카페 직원",
                "모퉁이 카페",
                "af_heart",
                "the barista at the corner cafe.",
                "Soy milk is free.",
                "The counter of the corner cafe."));

    mockMvc
        .perform(get("/admin/npcs/CAFE_STAFF").with(authentication(adminLogin())))
        .andExpect(status().isOk())
        .andExpect(content().string(containsString("the barista at the corner cafe.")))
        .andExpect(content().string(containsString("Soy milk is free.")))
        .andExpect(content().string(containsString("The counter of the corner cafe.")))
        .andExpect(content().string(containsString("/api/v1/admin/npcs/CAFE_STAFF/persona")));
  }

  // 활동 사용자 4명(다시 온 사용자 1명), 대화 5번(끝낸 대화 4번, 대화한 사용자 2명)
  private static UsageReport usageReport(int days) {
    return new UsageReport(
        days,
        new UsageReport.Summary(4, 3, 1, 6, 1, 2, 1, 5, 4, 2, 1234, 3, 7200, 5400),
        List.of(new UsageReport.Day("2030-01-09", 4, 3, 1, 2, 1, 5, 1, 3, 2700)),
        List.of(new UsageReport.Count("NPC_ENDED", 3, 2)),
        List.of(new UsageReport.Count("Mia · 카페 직원", 5, 2)),
        List.of(new UsageReport.Count("VOICE", 8, 2)),
        List.of(new UsageReport.Count("OBJECT", 3, 1)),
        List.of(new UsageReport.Count("SKIPPED_BY_USER", 1, 1)));
  }

  private static Authentication adminLogin() {
    return UsernamePasswordAuthenticationToken.authenticated(
        new AdminLogin("operator"), null, List.of());
  }

  private static Authentication gameLogin() {
    return UsernamePasswordAuthenticationToken.authenticated(new LoginUser(2L), null, List.of());
  }
}
