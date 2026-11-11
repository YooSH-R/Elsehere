const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const templatePath = path.join(
  __dirname,
  "../../frontend/UnityClient/Assets/WebGLTemplates/A305Responsive/index.html",
);
const desktopData = "desktop456.data.gz";
const mobileData = "mobile123.data.gz";
// WebGlBuild.MobileTextureDataToken: a local build leaves it as is because it has no mobile data.
const unreplaced = "A305_MOBILE_TEXTURE_DATA";

// Runs the template's texture data script in a fake browser whose WebGL 2 offers the given extensions.
function chooseTextureData({ extensions = [], webgl2 = true, mobile = mobileData } = {}) {
  const template = fs.readFileSync(templatePath, "utf8");
  const match = template.match(/<script id="texture-data">([\s\S]*?)<\/script>/);
  assert.ok(match, "The WebGL template must choose the texture data for the device");

  let lost = false;
  const gl = {
    getExtension(name) {
      if (name === "WEBGL_lose_context") return { loseContext: () => (lost = true) };
      return extensions.includes(name) ? {} : null;
    },
  };
  const document = {
    createElement: (tag) => ({ getContext: (type) => (tag === "canvas" && type === "webgl2" && webgl2 ? gl : null) }),
  };
  const window = {};
  vm.runInNewContext(match[1], { window, document });
  const chosen = window.chooseTextureData(desktopData, mobile);
  return { chosen, lost };
}

test("phones that read ASTC but not DXT get the mobile texture data", () => {
  const { chosen, lost } = chooseTextureData({ extensions: ["WEBGL_compressed_texture_astc"] });
  assert.equal(chosen, mobileData);
  assert.ok(lost, "The probe context is released so Unity can create its own");
});

test("computers that read DXT keep the desktop texture data", () => {
  assert.equal(
    chooseTextureData({ extensions: ["WEBGL_compressed_texture_s3tc", "WEBGL_compressed_texture_astc"] }).chosen,
    desktopData,
  );
  assert.equal(chooseTextureData({ extensions: ["WEBGL_compressed_texture_s3tc"] }).chosen, desktopData);
});

test("browsers that read neither format keep the desktop texture data", () => {
  assert.equal(chooseTextureData({ extensions: [] }).chosen, desktopData);
  assert.equal(chooseTextureData({ webgl2: false }).chosen, desktopData);
});

test("a build without mobile texture data keeps the desktop data", () => {
  assert.equal(
    chooseTextureData({ extensions: ["WEBGL_compressed_texture_astc"], mobile: unreplaced }).chosen,
    desktopData,
  );
});
