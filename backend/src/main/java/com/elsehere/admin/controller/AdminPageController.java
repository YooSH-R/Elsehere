package com.elsehere.admin.controller;

import com.elsehere.admin.service.AdminAiUsageService;
import com.elsehere.admin.service.AdminNpcService;
import com.elsehere.admin.service.AdminUsageService;
import java.util.Map;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestParam;

/**
 * 운영자 어드민 화면(templates/admin). 화면은 조회만 하고, 저장은 화면의 스크립트가 관리 API(/api/v1/admin/**)를 부른다. 상태를 바꾸는 요청에
 * 요청 확인 헤더(X-Requested-With)가 필요해 HTML 폼 전송 대신 이 방식을 쓴다.
 */
@Controller
@RequiredArgsConstructor
public class AdminPageController {

  /** AI 호출 종류(ai_requests.kind)의 화면 이름 */
  private static final Map<String, String> AI_KIND_NAMES =
      Map.of(
          "STT", "음성 인식",
          "GUARD", "무례 판정",
          "JUDGE", "의도 판정",
          "NPC", "NPC 대답",
          "TTS", "음성 합성",
          "HINT", "힌트·추천 단어",
          "MEANING", "뜻 보기",
          "FEEDBACK", "피드백",
          "MEMORY", "기억 카드");

  private final AdminNpcService adminNpcService;
  private final AdminAiUsageService adminAiUsageService;
  private final AdminUsageService adminUsageService;

  @GetMapping({"/admin", "/admin/"})
  public String home() {
    return "redirect:/admin/npcs";
  }

  /** 어드민 로그인 화면. 로그인하지 않고 어드민 화면을 열면 여기로 온다(AdminSecurityConfig). */
  @GetMapping("/admin/login")
  public String login() {
    return "admin/login";
  }

  @GetMapping("/admin/npcs")
  public String npcs(Model model) {
    model.addAttribute("npcs", adminNpcService.npcs());
    return "admin/npcs";
  }

  @GetMapping("/admin/npcs/{npcCode}")
  public String npc(@PathVariable String npcCode, Model model) {
    model.addAttribute("npc", adminNpcService.npc(npcCode));
    return "admin/npc-persona";
  }

  /** AI 사용량. AI 호출 기록을 최근 며칠(1~90일, 기본 7일) 동안 묶어 보여 준다. */
  @GetMapping("/admin/ai-usage")
  public String aiUsage(@RequestParam(defaultValue = "7") int days, Model model) {
    model.addAttribute("report", adminAiUsageService.report(days));
    model.addAttribute("kindNames", AI_KIND_NAMES);
    return "admin/ai-usage";
  }

  /** 이용 현황. DB에 남은 활동 흔적을 최근 며칠(1~90일, 기본 7일) 동안 묶고 활동한 사용자 목록을 보여 준다. 사용자는 ID만 보여 준다. */
  @GetMapping("/admin/usage")
  public String usage(@RequestParam(defaultValue = "7") int days, Model model) {
    model.addAttribute("report", adminUsageService.report(days));
    return "admin/usage";
  }

  /** 사용자 한 명의 이용 현황. 전체 화면의 사용자 목록이나 사용자 ID 입력칸에서 들어온다. */
  @GetMapping("/admin/usage/user")
  public String userUsage(
      @RequestParam long userId, @RequestParam(defaultValue = "7") int days, Model model) {
    model.addAttribute("report", adminUsageService.userReport(userId, days));
    return "admin/usage-user";
  }
}
