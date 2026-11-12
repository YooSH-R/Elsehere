mergeInto(LibraryManager.library, {
  // 주 입력 장치가 손가락이면 true. 마우스·터치패드가 주 입력인 터치 노트북은 false다.
  A305Player_IsPrimaryPointerCoarse: function () {
    return window.matchMedia("(pointer: coarse)").matches;
  },
  // 화면 배율. 창을 다른 모니터로 옮기면 바뀌므로 매번 읽는다.
  A305Player_DevicePixelRatio: function () {
    return window.devicePixelRatio || 1;
  },
  // 게임 캔버스에서만 브라우저 기본 손 모양을 쓰고, 대상을 벗어나면 스타일을 해제한다.
  A305Player_SetClickCursor: function (clickable) {
    const canvas = Module["canvas"];
    if (canvas) canvas.style.cursor = clickable ? "pointer" : "";
  },
  // 손 모양(pointer)과 창 크기 조절 모양(ew-resize 등)을 게임 캔버스에만 쓴다. 빈 문자열이면 기본 커서로 돌린다.
  A305Player_SetCursor: function (cursor) {
    const canvas = Module["canvas"];
    if (canvas) canvas.style.cursor = UTF8ToString(cursor);
  },
});
