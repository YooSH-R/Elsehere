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
  // 화면을 함께 보는 사람도 가리키는 곳을 알 수 있게, 게임 캔버스의 커서를 브라우저 기본보다 큰 그림으로 그린다.
  // 기본 모양은 검지를 편 손이고, 손끝에 노란 빛을 둥글게 깔아 가리키는 점을 드러낸다. 누를 수 있는 곳에서는 빛이 진해지고 테가 생긴다.
  // 그림마다 [SVG 안쪽, 그림 칸 왼쪽 위 x, y, 칸 한 변, 가리키는 점 x, y]를 두고, 그림을 못 쓰는 브라우저는 같은 이름의 기본 커서를 쓴다.
  $A305BigCursor: {
    outline: 'stroke="black" stroke-width="3" stroke-linejoin="round" fill="white"',
    css: function (name) {
      const big = A305BigCursor;
      const tipX = 26.5;
      const tipY = 6;
      const glow = function (centerOpacity, ring) {
        return (
          '<radialGradient id="glow"><stop offset="0" stop-color="yellow" stop-opacity="' +
          centerOpacity +
          '"/><stop offset="0.65" stop-color="gold" stop-opacity="' +
          centerOpacity * 0.85 +
          '"/><stop offset="1" stop-color="orange" stop-opacity="0"/></radialGradient>' +
          '<circle cx="' +
          tipX +
          '" cy="' +
          tipY +
          '" r="24" fill="url(#glow)"/>' +
          (ring
            ? '<circle cx="' +
              tipX +
              '" cy="' +
              tipY +
              '" r="15" fill="none" stroke="orange" stroke-width="2.5"/>'
            : "")
        );
      };
      const hand =
        '<path d="M22 9a4.5 4.5 0 0 1 9 0v17a4.5 4.5 0 0 1 9 0v2a4.5 4.5 0 0 1 9 0v2' +
        "a4 4 0 0 1 8 0v12c0 10-7 18-17 18h-6c-6 0-9-3-12-8L8 40" +
        'c-4-5 3-11 8-6l6 5z" ' +
        big.outline +
        "/>";
      const resize = '<path d="M5 32 19 18v9h26v-9l14 14-14 14v-9H19v9z" ' + big.outline;
      const drawings = {
        "": [glow(0.9, false) + hand, -18, -20, 88, tipX, tipY],
        pointer: [glow(1, true) + hand, -18, -20, 88, tipX, tipY],
        "ew-resize": [resize + "/>", 0, 0, 64, 32, 32],
        "ns-resize": [resize + ' transform="rotate(90 32 32)"/>', 0, 0, 64, 32, 32],
        "nwse-resize": [resize + ' transform="rotate(45 32 32)"/>', 0, 0, 64, 32, 32],
        "nesw-resize": [resize + ' transform="rotate(-45 32 32)"/>', 0, 0, 64, 32, 32],
      };
      const drawing = drawings[name];
      if (!drawing) return name;

      const left = drawing[1];
      const top = drawing[2];
      const side = drawing[3];
      const svg =
        '<svg xmlns="http://www.w3.org/2000/svg" width="' +
        side +
        '" height="' +
        side +
        '" viewBox="' +
        [left, top, side, side].join(" ") +
        '">' +
        drawing[0] +
        "</svg>";
      return (
        'url("data:image/svg+xml,' +
        encodeURIComponent(svg) +
        '") ' +
        Math.round(drawing[4] - left) +
        " " +
        Math.round(drawing[5] - top) +
        ", " +
        (name || "auto")
      );
    },
  },
  // 기본 손 모양(빈 문자열), 누를 수 있는 곳의 손 모양(pointer), 창 크기 조절 모양(ew-resize 등)을 게임 캔버스에만 쓴다.
  A305Player_SetCursor__deps: ["$A305BigCursor"],
  A305Player_SetCursor: function (cursor) {
    const canvas = Module["canvas"];
    if (canvas) canvas.style.cursor = A305BigCursor.css(UTF8ToString(cursor));
  },
});
