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

  const elements = new Map();
  for (const id of ["analytics-consent", "analytics-accept", "analytics-reject", "analytics-settings"]) {
    elements.set(id, {
      hidden: true,
      addEventListener(type, listener) {
        this.listeners ??= {};
        this.listeners[type] = listener;
      },
      click() {
        this.listeners?.click?.();
      },
    });
  }
  const scripts = [];
  const values = new Map();
  if (savedConsent) values.set("elsehere.analytics.consent", savedConsent);
  const cookies = [];
  let reloads = 0;
  const window = {
    location: { hostname, origin: `https://${hostname}`, reload() { reloads++; } },
    localStorage: {
      getItem(key) {
        if (options.blockStorage) throw new Error("Storage unavailable");
        return values.get(key) ?? null;
      },
      setItem(key, value) {
        if (options.blockStorage) throw new Error("Storage unavailable");
        values.set(key, value);
      },
      removeItem(key) {
        if (options.blockStorage) throw new Error("Storage unavailable");
        values.delete(key);
      },
    },
  };
  const document = {
    getElementById(id) { return elements.get(id); },
    createElement() { return {}; },
    head: { appendChild(script) { scripts.push(script); } },
    referrer,
    set cookie(value) { cookies.push(value); },
  };
  const script = options.emptyMeasurementId
    ? match[1].replace('const measurementId = "G-209TJ3SH81";', 'const measurementId = "";')
    : match[1];
  vm.runInNewContext(script, { window, document, URL, Date });
  return { elements, scripts, window, values, cookies, getReloads: () => reloads };
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
    ["#unity-loading", { hidden: false }],
    ["#unity-warning", { hidden: true, children: [], appendChild(child) { this.children.push(child); } }],
    ["#unity-progress", {}],
  ]);
  const document = {
    querySelector(selector) { return nodes.get(selector); },
    createElement() { return {}; },
    body: { appendChild(element) { loader = element; } },
  };
  const context = {
    window: page.window,
    document,
    performance: { now: () => 1000 },
    createUnityInstance: () => result === "initialization"
      ? Promise.reject(new Error("private error details"))
      : Promise.resolve(),
    setTimeout,
  };
  vm.runInNewContext(source, context);
  if (result === "loader") loader.onerror();
  else loader.onload();
  await new Promise(resolve => setImmediate(resolve));
}

test("analytics loads only after the visitor accepts", () => {
  const page = createPage();

  assert.equal(page.scripts.length, 0);
  assert.equal(page.elements.get("analytics-consent").hidden, false);
  page.elements.get("analytics-accept").click();
  assert.equal(page.scripts.length, 1);
  assert.match(page.scripts[0].src, /googletagmanager\.com\/gtag\/js\?id=G-209TJ3SH81/);
});

test("rejecting analytics saves the choice without loading Google and allows reopening settings", () => {
  const page = createPage();

  page.elements.get("analytics-reject").click();
  assert.equal(page.scripts.length, 0);
  assert.equal(page.values.get("elsehere.analytics.consent"), "denied");
  assert.equal(page.elements.get("analytics-consent").hidden, true);
  page.elements.get("analytics-settings").click();
  assert.equal(page.elements.get("analytics-consent").hidden, false);
});

test("withdrawing consent disables measurement and removes analytics cookies", () => {
  const page = createPage();

  page.elements.get("analytics-accept").click();
  page.elements.get("analytics-settings").click();
  page.elements.get("analytics-reject").click();

  assert.equal(page.window["ga-disable-G-209TJ3SH81"], true);
  assert.equal(page.window.dataLayer.length, 0);
  assert.equal(page.values.get("elsehere.analytics.consent"), "denied");
  assert.ok(page.cookies.some(cookie => cookie.startsWith("_ga=")));
  assert.ok(page.cookies.some(cookie => cookie.startsWith("_ga_209TJ3SH81=")));
  assert.equal(page.getReloads(), 1);
});

test("only production domains can start analytics, and saved rejection stays rejected", () => {
  const development = createPage("dev.elsehere.kr", "granted");
  const production = createPage("www.elsehere.kr", "granted");
  const rejected = createPage("elsehere.kr", "denied");

  assert.equal(development.scripts.length, 0);
  assert.equal(development.elements.get("analytics-consent").hidden, true);
  assert.equal(production.scripts.length, 1);
  assert.equal(rejected.scripts.length, 0);
  assert.equal(rejected.elements.get("analytics-consent").hidden, true);
});

test("a load completed before consent is sent only after acceptance", () => {
  const page = createPage();

  page.window.reportUnityLoad("loaded", 1.5);
  assert.equal(page.scripts.length, 0);
  assert.equal(page.window.dataLayer, undefined);
  page.elements.get("analytics-accept").click();
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

test("a rejected load is not sent if the visitor later changes their choice", () => {
  const page = createPage();

  page.elements.get("analytics-reject").click();
  page.window.reportUnityLoad("loaded", 3);
  page.elements.get("analytics-settings").click();
  page.elements.get("analytics-accept").click();

  const events = page.window.dataLayer.map(args => Array.from(args));
  assert.equal(events.filter(args => args[0] === "event").length, 0);
});

test("repeated acceptance does not install the Google tag twice", () => {
  const page = createPage();

  page.elements.get("analytics-accept").click();
  page.elements.get("analytics-settings").click();
  page.elements.get("analytics-accept").click();

  assert.equal(page.scripts.length, 1);
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

test("an empty measurement ID leaves analytics and the consent prompt disabled", () => {
  const page = createPage("elsehere.kr", "granted", "", { emptyMeasurementId: true });

  assert.equal(page.scripts.length, 0);
  assert.equal(page.elements.get("analytics-consent").hidden, true);
  assert.equal(page.elements.get("analytics-settings").hidden, true);
});

test("blocked storage keeps consent temporary and still disables measurement on rejection", () => {
  const page = createPage("elsehere.kr", null, "", { blockStorage: true });

  page.elements.get("analytics-accept").click();
  assert.equal(page.scripts.length, 1);
  page.elements.get("analytics-reject").click();
  assert.equal(page.window["ga-disable-G-209TJ3SH81"], true);
  assert.equal(page.getReloads(), 0);
});
