const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const templateDirectory = path.join(
  __dirname,
  "../../frontend/UnityClient/Assets/WebGLTemplates/A305Responsive",
);
const bridgePath = path.join(
  __dirname,
  "../../frontend/UnityClient/Assets/Plugins/WebGL/LoadingPageMusic.jslib",
);
const soundKey = "elsehere.audio.background.enabled";
const volumeKey = "elsehere.audio.background.volume";

// Runs the loading page's entry music script in a fake browser and connects the Unity bridge to it.
function loadPage({
  savedSound = null,
  savedVolume = null,
  autoplay = true,
  musicFile = true,
  webAudio = true,
} = {}) {
  const template = fs.readFileSync(path.join(templateDirectory, "index.html"), "utf8");
  const match = template.match(/<script id="entry-music">([\s\S]*?)<\/script>/);
  assert.ok(match, "The WebGL template must start the entry music on its loading page");

  const values = new Map();
  if (savedSound !== null) values.set(soundKey, savedSound);
  if (savedVolume !== null) values.set(volumeKey, savedVolume);
  const listeners = {};
  const requested = [];
  const warnings = [];
  let audio = null;

  class FakeAudioContext {
    constructor() {
      audio = this;
      this.state = autoplay ? "running" : "suspended";
      this.destination = {};
      this.sources = [];
    }
    createGain() {
      this.gain = { gain: { value: 1 }, connect() {} };
      return this.gain;
    }
    createBufferSource() {
      const source = {
        started: 0,
        stopped: 0,
        connect() {},
        disconnect() {},
        start() {
          this.started++;
        },
        stop() {
          this.stopped++;
        },
      };
      this.sources.push(source);
      return source;
    }
    decodeAudioData() {
      return Promise.resolve({ duration: 30.97 });
    }
    resume() {
      this.state = "running";
      return Promise.resolve();
    }
  }

  const window = {
    addEventListener(type, listener) {
      (listeners[type] ??= []).push(listener);
    },
  };
  if (webAudio) window.AudioContext = FakeAudioContext;
  vm.runInNewContext(match[1], {
    window,
    AudioContext: window.AudioContext,
    localStorage: {
      getItem: key => values.get(key) ?? null,
      setItem: (key, value) => values.set(key, value),
    },
    fetch: async url => {
      requested.push(url);
      return { ok: musicFile, status: musicFile ? 200 : 404, arrayBuffer: async () => new ArrayBuffer(8) };
    },
    console: { warn: (...args) => warnings.push(args) },
  });

  const unity = { LibraryManager: { library: {} }, mergeInto: Object.assign, window };
  vm.runInNewContext(fs.readFileSync(bridgePath, "utf8"), unity);

  return {
    unity: unity.LibraryManager.library,
    values,
    requested,
    warnings,
    audio: () => audio,
    dispatch(type) {
      for (const listener of listeners[type] ?? []) listener({ type });
    },
  };
}

// Lets the music download and decoding promises finish.
const settle = () => new Promise(resolve => setImmediate(resolve));

test("the loading page loops the entry music that ships with it, and Unity continues at that volume", async () => {
  const page = loadPage();
  await settle();

  assert.equal(page.requested.length, 1);
  assert.ok(fs.existsSync(path.join(templateDirectory, page.requested[0])));
  const sources = page.audio().sources;
  assert.equal(sources.length, 1);
  assert.equal(sources[0].loop, true);
  assert.equal(sources[0].started, 1);
  assert.ok(Math.abs(page.unity.A305_LoadingMusicVolume() - 0.18) < 1e-6);
});

test("when the browser blocks autoplay, the first click, key or touch starts the music", async () => {
  for (const type of ["pointerdown", "keydown", "touchend"]) {
    const page = loadPage({ autoplay: false });
    await settle();
    assert.equal(page.audio().state, "suspended");

    page.dispatch(type);

    assert.equal(page.audio().state, "running", type);
  }
});

test("Unity's fade lowers the music without restarting it, and leaving the entry screen stops it", async () => {
  const page = loadPage();
  await settle();
  const sources = page.audio().sources;

  page.unity.A305_SetLoadingMusic(0.09, 1, 1);
  assert.ok(Math.abs(page.audio().gain.gain.value - 0.09) < 1e-6);
  assert.equal(sources.length, 1);
  assert.equal(sources[0].stopped, 0);

  page.unity.A305_SetLoadingMusic(0, 0, 1);
  assert.equal(sources[0].stopped, 1);

  page.unity.A305_SetLoadingMusic(0.18, 1, 1);
  assert.equal(sources.length, 2);
  assert.equal(sources[1].started, 1);
});

test("turning the speaker off in the game keeps the next loading page silent", async () => {
  const page = loadPage();
  await settle();
  page.unity.A305_SetLoadingMusic(0, 0, 0);
  assert.equal(page.values.get(soundKey), "0");

  const next = loadPage({ savedSound: "0" });
  await settle();

  assert.equal(next.audio().sources.length, 0);
  assert.equal(next.unity.A305_LoadingMusicVolume(), 0);
});

test("the speaker's volume scales the music, and the next loading page starts at that volume", async () => {
  const page = loadPage();
  await settle();

  page.unity.A305_SetLoadingMusic(0.18, 1, 1, 0.5);

  assert.ok(Math.abs(page.audio().gain.gain.value - 0.09) < 1e-6);
  // Unity keeps fading from the music's own volume; the speaker volume is applied on top.
  assert.ok(Math.abs(page.unity.A305_LoadingMusicVolume() - 0.18) < 1e-6);
  assert.equal(page.values.get(volumeKey), "0.5");

  const next = loadPage({ savedVolume: "0.5" });
  await settle();

  assert.ok(Math.abs(next.audio().gain.gain.value - 0.09) < 1e-6);
  assert.ok(Math.abs(next.unity.A305_LoadingMusicVolume() - 0.18) < 1e-6);
});

test("without the music file or Web Audio the game still loads and Unity plays its own music", async () => {
  const missing = loadPage({ musicFile: false });
  await settle();
  assert.equal(missing.unity.A305_LoadingMusicVolume(), -1);
  assert.equal(missing.warnings.length, 1);

  const noWebAudio = loadPage({ webAudio: false });
  await settle();
  assert.equal(noWebAudio.unity.A305_LoadingMusicVolume(), -1);
  assert.doesNotThrow(() => noWebAudio.unity.A305_SetLoadingMusic(0.18, 1, 1));
});
