package com.elsehere.user.dto;

import com.elsehere.user.entity.UiTheme;
import jakarta.validation.constraints.NotNull;

/**
 * USER-01 화면 테마 변경 요청.
 *
 * @param uiTheme 고른 테마. JELLY·STICKER·CLOUD 밖의 값은 본문을 읽을 때 거절한다.
 */
public record UiThemeRequest(@NotNull UiTheme uiTheme) {}
