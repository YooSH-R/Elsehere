const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const source = path.resolve(__dirname, "../../frontend/UnityClient/Assets/Plugins/WebGL/WordPronunciation.jslib");

function browser(supported = true) {
  const spoken = [];
  let cancelled = 0;
  const english = { lang: "en-US", name: "English" };
  const speech = { getVoices: () => [{ lang: "ko-KR" }, english], cancel() { cancelled++; }, speak: (line) => spoken.push(line) };
  const context = {
    LibraryManager: { library: {} },
    mergeInto: Object.assign,
    UTF8ToString: (text) => text,
    window: supported ? { speechSynthesis: speech } : {},
    SpeechSynthesisUtterance: function (text) { this.text = text; },
  };
  vm.runInNewContext(fs.readFileSync(source, "utf8"), context);
  return { api: context.LibraryManager.library, spoken, english, cancellations: () => cancelled };
}

test("a word can be pronounced in English without a server audio file", () => {
  const { api, spoken, english } = browser();
  assert.equal(api.A305Word_CanPronounce(), 1);
  assert.equal(api.A305Word_Pronounce("floor"), 1);
  assert.equal(spoken.length, 1);
  assert.equal(spoken[0].text, "floor");
  assert.equal(spoken[0].lang, "en-US");
  assert.equal(spoken[0].voice, english);
});

test("unsupported browsers and empty words do not claim to play audio", () => {
  const unsupported = browser(false).api;
  assert.equal(unsupported.A305Word_CanPronounce(), 0);
  assert.equal(unsupported.A305Word_Pronounce("floor"), 0);
  const { api, spoken } = browser();
  assert.equal(api.A305Word_Pronounce("  "), 0);
  assert.equal(spoken.length, 0);
});


test("stopping a sentence cancels device speech without sending another utterance", () => {
  const { api, spoken, cancellations } = browser();
  api.A305Word_Pronounce("Where is the cafe?");
  const before = cancellations();
  api.A305Word_StopPronunciation();
  assert.equal(cancellations(), before + 1);
  assert.equal(spoken.length, 1);
  assert.doesNotThrow(() => browser(false).api.A305Word_StopPronunciation());
});
