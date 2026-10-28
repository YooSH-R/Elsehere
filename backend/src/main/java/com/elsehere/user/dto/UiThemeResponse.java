package com.elsehere.user.dto;

import com.elsehere.user.entity.UiTheme;

/**
 * USER-01 화면 테마 변경 결과.
 *
 * @param uiTheme 저장한 테마. 다시 접속하면 BOOT-01의 {@code user.uiTheme}로 내려간다.
 */
public record UiThemeResponse(UiTheme uiTheme) {}
