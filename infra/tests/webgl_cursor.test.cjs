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
