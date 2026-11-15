package com.elsehere.dialogue.service;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyBoolean;
import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

import com.elsehere.favor.service.FavorService;
import com.elsehere.favor.service.FavorService.FavorTurn;
import java.util.List;

// 돌발 부탁을 다루지 않는 턴 테스트에 넘기는 부탁 서비스. 부탁을 꺼내지 않고 지시문·응답도 바꾸지 않는다.
final class NoFavors {

  private NoFavors() {}

  static FavorService service() {
    FavorService favors = mock(FavorService.class);
    when(favors.plan(anyLong(), any(), anyString(), anyLong(), anyBoolean()))
        .thenReturn(FavorTurn.NONE);
    when(favors.instruction(any(), any(), anyBoolean(), anyBoolean())).thenReturn("");
    when(favors.apply(anyLong(), any(), anyLong(), any(), any(), any(), anyBoolean(), anyBoolean()))
        .thenReturn(List.of());
    return favors;
  }
}
