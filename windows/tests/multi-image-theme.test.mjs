import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const temporary = await fs.mkdtemp(path.join(os.tmpdir(), "codex-multi-image-theme-"));
const injector = path.join(root, "scripts", "injector.mjs");
const sourceImage = path.join(root, "assets", "dream-reference.png");
const names = ["background.png", "sidebar.png", "composer.png", "home.png", "home-composer.png", "polaroid.png"];

try {
  await Promise.all(names.map(name => fs.copyFile(sourceImage, path.join(temporary, name))));
  const theme = {
    schemaVersion: 8,
    id: "custom-11111111111111111111111111111111",
    name: "Multi image fixture",
    image: names[0],
    images: { sidebar: names[1], composer: names[2], home: names[3], homeComposer: names[4], polaroid: names[5] },
    appearance: "auto",
    art: { focusX: 0.64, focusY: 0.44, safeArea: "left", taskMode: "ambient" },
    compositions: {
      background: { focusX: 0.64, focusY: 0.44, zoom: 1, fit: "auto", offsetX: 0, offsetY: 0 },
      sidebar: { focusX: 0.25, focusY: 0.7, zoom: 1.6, fit: "cover", offsetX: 0.2, offsetY: -0.1 },
      composer: { focusX: 0.5, focusY: 0.5, zoom: 1.2, fit: "fill", offsetX: 0, offsetY: 0 },
      home: { focusX: 0.75, focusY: 0.35, zoom: 1, fit: "contain", offsetX: -0.2, offsetY: 0.1 },
      homeComposer: { focusX: 0.6, focusY: 0.4, zoom: 1.1, fit: "cover", offsetX: 0, offsetY: 0 },
      polaroid: { focusX: 0.45, focusY: 0.3, zoom: 1.3, fit: "cover", offsetX: 0, offsetY: 0 },
    },
    palette: { accent: "#1557b0" },
    materials: {
      light: { page: 0.56, sidebar: 0.58, composer: 0.48, card: 0.18 },
      dark: { page: 0.68, sidebar: 0.74, composer: 0.62, card: 0.42 },
      components: {
        messages: { light: { color: "#123456", opacity: 0.21 }, dark: { color: "#ABCDEF", opacity: 0.61 } },
      },
    },
  };
  await fs.writeFile(path.join(temporary, "theme.json"), JSON.stringify(theme), "utf8");
  const accepted = spawnSync(process.execPath, [injector, "--check-payload", "--theme-dir", temporary], { encoding: "utf8" });
  assert.equal(accepted.status, 0, accepted.stderr || accepted.stdout);
    assert.equal(JSON.parse(accepted.stdout).version, "3.9.4");

  theme.materials.components.messages.light.color = "rgba(0,0,0,.5)";
  await fs.writeFile(path.join(temporary, "theme.json"), JSON.stringify(theme), "utf8");
  const invalidComponentColor = spawnSync(process.execPath, [injector, "--check-payload", "--theme-dir", temporary], { encoding: "utf8" });
  assert.notEqual(invalidComponentColor.status, 0, "a non-hex component color was accepted");
  assert.match(invalidComponentColor.stderr + invalidComponentColor.stdout, /components\.messages\.light\.color/);
  theme.materials.components.messages.light.color = "#123456";

  theme.compositions.sidebar.zoom = 3.1;
  await fs.writeFile(path.join(temporary, "theme.json"), JSON.stringify(theme), "utf8");
  const invalidComposition = spawnSync(process.execPath, [injector, "--check-payload", "--theme-dir", temporary], { encoding: "utf8" });
  assert.notEqual(invalidComposition.status, 0, "an out-of-range regional zoom was accepted");
  assert.match(invalidComposition.stderr + invalidComposition.stdout, /compositions\.sidebar\.zoom/);
  theme.compositions.sidebar.zoom = 1.6;

  theme.images.sidebar = "../escape.png";
  await fs.writeFile(path.join(temporary, "theme.json"), JSON.stringify(theme), "utf8");
  const rejected = spawnSync(process.execPath, [injector, "--check-payload", "--theme-dir", temporary], { encoding: "utf8" });
  assert.notEqual(rejected.status, 0, "a regional image path escape was accepted");
  assert.match(rejected.stderr + rejected.stdout, /top-level relative files|remain inside/);
  console.log("PASS: schema-8 six-region composition and component-material validation.");
} finally {
  await fs.rm(temporary, { recursive: true, force: true });
}
