const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const templatePath = path.join(
  __dirname,
  "../../frontend/UnityClient/Assets/WebGLTemplates/A305Responsive/index.html",
);

function createPage() {
  const template = fs.readFileSync(templatePath, "utf8");
  const match = template.match(/<script id="extension-error-filter">([\s\S]*?)<\/script>/);
  assert.ok(match, "The WebGL template must include its extension error filter");

  const listeners = { error: [], unhandledrejection: [] };
  const window = {
    addEventListener(type, listener) {
      listeners[type].push(listener);
    },
  };
  vm.runInNewContext(match[1], { window });

  // Stands in for the Unity loader, which adds its own window listeners when createUnityInstance runs.
  const reachedUnity = [];
  for (const type of Object.keys(listeners)) {
    window.addEventListener(type, event => reachedUnity.push(event));
  }

  function dispatch(type, fields) {
    const event = {
      type,
      ...fields,
      propagationStopped: false,
      defaultPrevented: false,
      stopImmediatePropagation() {
        this.propagationStopped = true;
      },
      preventDefault() {
        this.defaultPrevented = true;
      },
    };
    for (const listener of listeners[type]) {
      listener(event);
      if (event.propagationStopped) break;
    }
    return event;
  }

  return { dispatch, reachedUnity };
}

function errorWithStack(message, stack, extra = {}) {
  return Object.assign(new Error(message), { stack }, extra);
}

test("a MetaMask rejection in Chrome stays in the console instead of reaching the Unity loader", () => {
  const page = createPage();
  // Chrome gives unhandledrejection no file name, so only the stack shows where the error came from.
  const reason = errorWithStack(
    "Failed to connect to MetaMask",
    "i: Failed to connect to MetaMask\n" +
      "    at Object.connect (chrome-extension://nkbihfbeogaeaoehlefnkodbefgpgknn/scripts/inpage.js:12:4822)",
  );

  const event = page.dispatch("unhandledrejection", { reason });

  assert.equal(page.reachedUnity.length, 0);
  assert.equal(event.defaultPrevented, false, "The browser should still print the error in its console");
});

test("errors that Firefox or Safari report from an extension file stay out of the Unity loader", () => {
  const page = createPage();
  // Firefox and Safari write stack frames as "function@file" and report the file on the event or the error.
  const firefoxScript = "moz-extension://4a1b2c3d/scripts/inpage.js";
  const safariScript = "safari-web-extension://5e6f7a8b/scripts/inpage.js";

  page.dispatch("error", {
    message: "Error: Failed to connect to MetaMask",
    filename: firefoxScript,
    error: errorWithStack("Failed to connect to MetaMask", `connect@${firefoxScript}:12:4822\n`),
  });
  page.dispatch("unhandledrejection", {
    reason: errorWithStack("Failed to connect to MetaMask", `connect@${firefoxScript}:12:4822\n`, {
      fileName: firefoxScript,
    }),
  });
  page.dispatch("unhandledrejection", {
    reason: errorWithStack("Failed to connect to MetaMask", `connect@${safariScript}:12:4822`, {
      sourceURL: safariScript,
    }),
  });

  assert.equal(page.reachedUnity.length, 0);
});

test("game errors still reach the Unity loader, even when called through an extension wrapper", () => {
  const page = createPage();
  const gameScript = "https://elsehere.kr/Build/build.framework.js";

  page.dispatch("error", {
    message: "Uncaught TypeError: Cannot read properties of undefined",
    filename: gameScript,
    error: errorWithStack(
      "Cannot read properties of undefined",
      `TypeError: Cannot read properties of undefined\n    at _JS_Sound_Play (${gameScript}:10:200)`,
    ),
  });
  // Some extensions wrap page functions, so their frames can sit below the line that threw.
  page.dispatch("unhandledrejection", {
    reason: errorWithStack(
      "Microphone stream ended",
      "Error: Microphone stream ended\n" +
        `    at onTrackEnded (${gameScript}:20:300)\n` +
        "    at MediaStreamTrack.wrapped (chrome-extension://abcdefgh/scripts/content.js:3:120)",
    ),
  });

  assert.equal(page.reachedUnity.length, 2);
});

test("errors from an unknown place still reach the Unity loader", () => {
  const page = createPage();

  page.dispatch("unhandledrejection", { reason: "Failed to connect" });
  page.dispatch("unhandledrejection", { reason: undefined });
  page.dispatch("error", { message: "Script error.", filename: "", error: null });

  assert.equal(page.reachedUnity.length, 3);
});

test("the filter starts listening before the Unity loader adds its own listeners", () => {
  const template = fs.readFileSync(templatePath, "utf8");

  // Listeners on the same window run in the order they were added, so the filter must come first.
  const filterAt = template.indexOf('<script id="extension-error-filter">');
  const loaderAt = template.indexOf("createUnityInstance(");
  assert.ok(filterAt >= 0 && loaderAt >= 0);
  assert.ok(filterAt < loaderAt);
});
