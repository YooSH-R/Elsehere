const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const source = path.resolve(__dirname, "../../frontend/UnityClient/Assets/Plugins/WebGL/BrowserPointer.jslib");

test("clickable targets set the native hand on the game canvas and leaving restores the default", () => {
  const canvas = { style: { cursor: "" } };
  const context = {
    LibraryManager: { library: {} },
    mergeInto: Object.assign,
    Module: { canvas },
  };
  vm.runInNewContext(fs.readFileSync(source, "utf8"), context);

  const setCursor = context.LibraryManager.library.A305Player_SetClickCursor;
  setCursor(1);
  assert.equal(canvas.style.cursor, "pointer");
  setCursor(0);
  assert.equal(canvas.style.cursor, "");
});

function loadSetCursor(canvas) {
  const context = {
    LibraryManager: { library: {} },
    mergeInto: Object.assign,
    Module: { canvas },
    // Unity passes C# strings as pointers; the fake returns the text itself.
    UTF8ToString: (text) => text,
  };
  vm.runInNewContext(fs.readFileSync(source, "utf8"), context);
  // Emscripten exposes a "$name" library entry as the global "name".
  context.A305BigCursor = context.LibraryManager.library.$A305BigCursor;
  return context.LibraryManager.library.A305Player_SetCursor;
}

// A CSS cursor made of one SVG picture, the point it points with, and the native cursor to fall back on.
function parseCursor(css) {
  const parts = /^url\("data:image\/svg\+xml,([^"]+)"\) (\d+) (\d+), ([a-z-]+)$/.exec(css);
  assert.ok(parts, `not a picture cursor: ${css}`);
  const svg = decodeURIComponent(parts[1]);
  return {
    svg,
    side: Number(/width="(\d+)"/.exec(svg)[1]),
    hotX: Number(parts[2]),
    hotY: Number(parts[3]),
    fallback: parts[4],
  };
}

test("the default cursor is a large pointing hand with a yellow glow at the fingertip", () => {
  const canvas = { style: { cursor: "" } };
  const setCursor = loadSetCursor(canvas);

  setCursor("");
  const hand = parseCursor(canvas.style.cursor);

  // Browsers ignore cursor pictures over 128px, and the native arrow is 32px.
  assert.ok(hand.side > 32 && hand.side <= 128);
  assert.match(hand.svg, /radialGradient/);
  assert.match(hand.svg, /stop-color="yellow"/);
  assert.ok(hand.hotX > 0 && hand.hotX < hand.side && hand.hotY > 0 && hand.hotY < hand.side);
  assert.equal(hand.fallback, "auto");
});

test("clickable targets keep the hand and add a ring around the glow", () => {
  const canvas = { style: { cursor: "" } };
  const setCursor = loadSetCursor(canvas);

  setCursor("");
  const idle = parseCursor(canvas.style.cursor);
  setCursor("pointer");
  const clickable = parseCursor(canvas.style.cursor);

  assert.notEqual(clickable.svg, idle.svg);
  assert.match(clickable.svg, /stroke="orange"/);
  assert.doesNotMatch(idle.svg, /stroke="orange"/);
  assert.deepEqual([clickable.hotX, clickable.hotY], [idle.hotX, idle.hotY]);
  assert.equal(clickable.fallback, "pointer");
});

test("window edges set a large resize cursor that points with its centre", () => {
  const canvas = { style: { cursor: "" } };
  const setCursor = loadSetCursor(canvas);

  const pictures = new Set();
  for (const name of ["ew-resize", "ns-resize", "nwse-resize", "nesw-resize"]) {
    setCursor(name);
    const resize = parseCursor(canvas.style.cursor);
    assert.ok(resize.side > 32 && resize.side <= 128);
    assert.deepEqual([resize.hotX, resize.hotY], [resize.side / 2, resize.side / 2]);
    assert.equal(resize.fallback, name);
    pictures.add(resize.svg);
  }
  assert.equal(pictures.size, 4);
});

test("a cursor name without a picture is passed to the browser as it is", () => {
  const canvas = { style: { cursor: "" } };
  const setCursor = loadSetCursor(canvas);

  setCursor("text");
  assert.equal(canvas.style.cursor, "text");
});
