mergeInto(LibraryManager.library, {
  // 주 입력 장치가 손가락이면 true. 마우스·터치패드가 주 입력인 터치 노트북은 false다.
  A305Player_IsPrimaryPointerCoarse: function () {
    return window.matchMedia("(pointer: coarse)").matches;
  },
  // 화면 배율. 창을 다른 모니터로 옮기면 바뀌므로 매번 읽는다.
  A305Player_DevicePixelRatio: function () {
    return window.devicePixelRatio || 1;
  },
});
