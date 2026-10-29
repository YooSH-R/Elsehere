package com.elsehere.admin.controller;

import com.elsehere.admin.service.AdminNpcService;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;

/**
 * 운영자 어드민 화면(templates/admin). 화면은 조회만 하고, 저장은 화면의 스크립트가 관리 API(/api/v1/admin/**)를 부른다. 상태를 바꾸는 요청에
 * 요청 확인 헤더(X-Requested-With)가 필요해 HTML 폼 전송 대신 이 방식을 쓴다.
 */
@Controller
@RequiredArgsConstructor
public class AdminPageController {

  private final AdminNpcService adminNpcService;

  @GetMapping({"/admin", "/admin/"})
  public String home() {
    return "redirect:/admin/npcs";
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
}
