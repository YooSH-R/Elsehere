const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const templatePath = path.join(
  __dirname,
  "../../frontend/UnityClient/Assets/WebGLTemplates/A305Responsive/index.html",
);

function createPage(hostname = "elsehere.kr", savedConsent = null, referrer = "", options = {}) {
  const template = fs.readFileSync(templatePath, "utf8");
  const match = template.match(/<script id="analytics-script">([\s\S]*?)<\/script>/);
  assert.ok(match, "The WebGL template must include its analytics script");

  const scripts = [];
  const values = new Map();
  if (savedConsent) values.set("elsehere.analytics.consent", savedConsent);
  const cookies = [];
  const replacedUrls = [];
  const search = options.search ?? "";
  const window = {
    location: {
      hostname,
      origin: `https://${hostname}`,
      href: `https://${hostname}/${search}`,
    },
    history: { replaceState(state, title, url) { replacedUrls.push(url); } },
    localStorage: {
      getItem(key) {
        if (options.blockStorage) throw new Error("Storage unavailable");
        return values.get(key) ?? null;
      },
    },
  };
  const document = {
    createElement() { return {}; },
    head: { appendChild(script) { scripts.push(script); } },
    referrer,
    get cookie() { return options.cookie ?? ""; },
    set cookie(value) { cookies.push(value); },
  };
  const script = options.emptyMeasurementId
    ? match[1].replace('const measurementId = "G-209TJ3SH81";', 'const measurementId = "";')
    : match[1];
  vm.runInNewContext(script, { window, document, URL, Date });
  return { scripts, window, cookies, replacedUrls };
}

async function runUnityLoader(page, result) {
  const template = fs.readFileSync(templatePath, "utf8");
  const match = template.match(/<script>([\s\S]*?)<\/script>/);
  assert.ok(match, "The WebGL template must include its Unity loader");
  const source = match[1]
    .replace(/{{{ JSON\.stringify\([^}]+ }}}/g, '"fixture"')
    .replace(/{{{[^}]+}}}/g, "fixture")
    .replace(/^\s*#(?:if|endif).*$/gm, "");
  let loader;
  const nodes = new Map([
    ["#unity-canvas", { addEventListener() {} }],
    ["#unity-loading", { hidden: false, style: {}, animate() { return { finished: Promise.resolve() }; } }],
    ["#unity-warning", { hidden: true, children: [], appendChild(child) { this.children.push(child); } }],
    ["#unity-progress", { style: { setProperty() {} }, setAttribute() {} }],
    ["#intro-slides", { appendChild() {} }],
    ["#intro-caption", {}],
  ]);
  const document = {
    querySelector(selector) { return nodes.get(selector); },
    createElement() { return { append() {}, appendChild() {} }; },
    body: { appendChild(element) { loader = element; } },
  };
  const context = {
    window: page.window,
    document,
    performance: { now: () => 1000 },
    createUnityInstance: () => result === "initialization"
      ? Promise.reject(new Error("private error details"))
      : Promise.resolve(),
    // The intro slides keep waiting for their first picture so the test only follows the loader.
    Image: class { decode() { return new Promise(() => {}); } },
    requestAnimationFrame() {},
    setTimeout,
    clearTimeout,
  };
  vm.runInNewContext(source, context);
  if (result === "loader") loader.onerror();
  else loader.onload();
  await new Promise(resolve => setImmediate(resolve));
}

test("analytics starts on the first visit without a notice or a settings button", () => {
  const template = fs.readFileSync(templatePath, "utf8");
  const page = createPage();

  assert.equal(page.scripts.length, 1);
  assert.match(page.scripts[0].src, /googletagmanager\.com\/gtag\/js\?id=G-209TJ3SH81/);
  // The privacy policy explains how to refuse (blocking cookies or Google's opt-out add-on).
  assert.doesNotMatch(template, /id="analytics-(consent|settings)"/);
});

test("only production domains can start analytics, and saved rejection stays rejected", () => {
  const development = createPage("dev.elsehere.kr", "granted");
  const production = createPage("www.elsehere.kr", "granted");
  const rejected = createPage("elsehere.kr", "denied");

  assert.equal(development.scripts.length, 0);
  assert.equal(production.scripts.length, 1);
  assert.equal(rejected.scripts.length, 0);
});

test("a load on the first visit is sent right away", () => {
  const page = createPage();

  page.window.reportUnityLoad("loaded", 1.5);
  const events = page.window.dataLayer.map(args => Array.from(args));
  const event = events.find(args => args[0] === "event" && args[1] === "webgl_loaded");
  assert.ok(event);
  assert.equal(event[2].duration_seconds, 1.5);
});

test("load failures send only a fixed failure stage after consent", () => {
  const page = createPage("elsehere.kr", "granted");

  page.window.reportUnityLoad("failed", "loader");
  page.window.reportUnityLoad("failed", "private exception text");
  const events = page.window.dataLayer
    .map(args => Array.from(args))
    .filter(args => args[0] === "event");
  assert.equal(events.length, 1);
  assert.equal(events[0][1], "webgl_load_failed");
  assert.equal(events[0][2].failure_stage, "loader");
  assert.deepEqual(Object.keys(events[0][2]), ["failure_stage"]);
});

test("a browser that refused on the earlier notice sends no load events", () => {
  const page = createPage("elsehere.kr", "denied");

  page.window.reportUnityLoad?.("loaded", 3);
  assert.equal(page.window.dataLayer, undefined);
});

test("Unity loader success and failures report the corresponding safe events", async () => {
  for (const outcome of ["loaded", "loader", "initialization"]) {
    const page = createPage("elsehere.kr", "granted");
    await runUnityLoader(page, outcome);
    const events = page.window.dataLayer
      .map(args => Array.from(args))
      .filter(args => args[0] === "event");
    assert.equal(events.length, 1);
    if (outcome === "loaded") {
      assert.equal(events[0][1], "webgl_loaded");
    } else {
      assert.equal(events[0][1], "webgl_load_failed");
      assert.equal(events[0][2].failure_stage, outcome);
      assert.doesNotMatch(JSON.stringify(events[0]), /private error details/);
    }
  }
});

test("referral information excludes the source path and query", () => {
  const page = createPage("elsehere.kr", "granted", "https://example.com/private?email=test@example.com");
  const config = page.window.dataLayer.map(args => Array.from(args)).find(args => args[0] === "config");

  assert.equal(config[2].page_location, "https://elsehere.kr/");
  assert.equal(config[2].page_referrer, "https://example.com/");
});

test("an empty measurement ID leaves analytics disabled", () => {
  const page = createPage("elsehere.kr", "granted", "", { emptyMeasurementId: true });

  assert.equal(page.scripts.length, 0);
});

test("opening the page with ?team=1 marks this browser as a team device and skips analytics", () => {
  const page = createPage("elsehere.kr", null, "", { search: "?team=1&from=qr" });

  assert.ok(page.cookies.some(cookie => cookie.startsWith("elsehere_team=1;")));
  assert.equal(page.scripts.length, 0);
  assert.deepEqual(page.replacedUrls, ["/?from=qr"]);
});

test("a team device is not measured on later visits even with saved consent", () => {
  const page = createPage("elsehere.kr", "granted", "", { cookie: "other=1; elsehere_team=1" });

  assert.equal(page.scripts.length, 0);
  assert.equal(page.window.dataLayer, undefined);
  assert.deepEqual(page.replacedUrls, []);
});

test("opening the page with ?team=0 removes the team mark and measures again", () => {
  const page = createPage("elsehere.kr", null, "", { cookie: "elsehere_team=1", search: "?team=0" });

  assert.ok(page.cookies.some(cookie => cookie.startsWith("elsehere_team=;") && cookie.includes("max-age=0")));
  assert.equal(page.scripts.length, 1);
  assert.deepEqual(page.replacedUrls, ["/"]);
});

test("blocked storage still measures by default", () => {
  const page = createPage("elsehere.kr", null, "", { blockStorage: true });

  assert.equal(page.scripts.length, 1);
});
