import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { execFileSync } from "node:child_process";
import { readFileSync, statSync } from "node:fs";
import { test } from "node:test";
import * as entry from "@hex1b/web-terminal";
import { normalizeFont } from "../.build/terminal-font.js";

const root = new URL("../", import.meta.url);
const read = path => readFileSync(new URL(path, root), "utf8");

test("Package entry exposes only the supported runtime API", () => {
  assert.deepEqual(Object.keys(entry).sort(), [
    "InputRoute", "MAX_FONT_SIZE", "MIN_FONT_SIZE", "TerminalAction", "WebTerminal",
    "createDefaultScrollbarRenderer", "defaultDarkPalette", "defaultInputBindings", "defaultLightPalette",
    "getCmdlineUrl", "linkAction", "parseCommandMarkParameters", "renderDefaultScrollbar", "renderDefaultScrollbarTooltip"
  ]);
  assert.equal(entry.MIN_FONT_SIZE, 8);
  assert.equal(entry.MAX_FONT_SIZE, 32);
  assert.equal(typeof entry.WebTerminal.mount, "function");
  assert.notEqual(entry.defaultInputBindings(), entry.defaultInputBindings());
});

test("Packed allowlist ships a complete, registry-neutral browser package", () => {
  const [packed] = JSON.parse(execFileSync("npm", ["pack", "--dry-run", "--ignore-scripts", "--json"], {
    cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"]
  }));
  const manifest = JSON.parse(read("package.json"));
  assert.equal(packed.name, "@hex1b/web-terminal");
  assert.equal(packed.version, manifest.version);
  assert.equal(manifest.publishConfig.registry, undefined);
  assert.equal(manifest.publishConfig.access, "public");
  assert.deepEqual(manifest.dependencies ?? {}, {});
  assert.deepEqual(Object.keys(manifest.exports), ["."]);
  const paths = new Set(packed.files.map(file => file.path));
  for (const file of ["package.json", "README.md", "LICENSE", "dist/index.js", "dist/index.d.ts",
    "dist/index.js.map", "dist/web-terminal.d.ts", "dist/types.d.ts", "dist/LICENSE",
    "dist/scrollbar-types.d.ts", "dist/scrollbar-appearance.d.ts",
    "dist/fonts/cascadia-mono-nf/CascadiaMonoNF.woff2",
    "dist/fonts/cascadia-mono-nf/LICENSE.txt", "dist/fonts/cascadia-mono-nf/README.md"]) {
    assert.ok(paths.has(file), `Missing ${file}`);
  }
  assert.deepEqual([...paths].filter(path => /\.[cm]?js$/u.test(path)), ["dist/index.js"],
    "Vendoring must need exactly one JavaScript file, including both workers");
  for (const path of paths) {
    assert.ok(path.startsWith("dist/") || ["package.json", "README.md", "LICENSE"].includes(path), path);
    if (path.endsWith(".js")) {
      assert.ok(paths.has(`${path}.map`), `Missing source map for ${path}`);
      assert.ok(paths.has(path.replace(/\.js$/u, ".d.ts")), `Missing declaration for ${path}`);
      const source = read(path);
      assert.doesNotMatch(source, /\bimport\s*(?:\(|["'{*])/u, "The bundle must contain no runtime imports");
      assert.doesNotMatch(source, /samples\/WebTerminalDemo|wwwroot/u);
      const map = JSON.parse(read(`${path}.map`));
      assert.ok(map.sourcesContent.length > 0, "Source maps must remain useful without packaged src/");
      for (const module of ["web-terminal.ts", "terminal-worker.ts", "link-detection-worker.ts"]) {
        assert.ok(map.sources.some(source => source.endsWith(`/${module}`)), `Missing bundled ${module}`);
      }
    }
  }
  assert.match(read("LICENSE"), /MIT License/u);
  assert.equal(paths.has("dist/THIRD-PARTY-NOTICES.txt"), false);
});

test("Both workers and the default font resolve relative to the single bundle", () => {
  const bundle = read("dist/index.js");
  assert.match(bundle, /new URL\(import\.meta\.url\)/u);
  assert.match(bundle, /#hex1b-terminal-worker/u);
  assert.match(bundle, /#hex1b-link-detection-worker/u);
  assert.match(bundle, /new URL\("\.\/fonts\/cascadia-mono-nf\/CascadiaMonoNF\.woff2", import\.meta\.url\)/u);
  const font = new URL("dist/fonts/cascadia-mono-nf/CascadiaMonoNF.woff2", root);
  assert.equal(new URL(normalizeFont().faces[0].url).pathname.split("/fonts/")[1],
    "cascadia-mono-nf/CascadiaMonoNF.woff2");
  assert.equal(statSync(font).size, 976460);
  assert.equal(createHash("sha256").update(readFileSync(font)).digest("hex"),
    "bd42b0c992de9c42d8a770112e8140d67d6437798be39d49e57f4954e2f6e8e2");
  assert.match(read("dist/fonts/cascadia-mono-nf/LICENSE.txt"), /SIL OPEN FONT LICENSE/u);
  assert.match(read("dist/fonts/cascadia-mono-nf/README.md"), /v2407\.24/u);
});

test("Worker URLs preserve the module path and query while replacing its fragment", async () => {
  const moduleUrl = new URL("../.build/worker-url.js?version=vendored#host-fragment", import.meta.url);
  const { defaultWorkerUrl } = await import(moduleUrl.href);
  for (const kind of ["terminal", "link-detection"]) {
    const worker = defaultWorkerUrl(kind);
    assert.equal(worker.pathname, moduleUrl.pathname);
    assert.equal(worker.search, moduleUrl.search);
    assert.equal(worker.hash, `#hex1b-${kind}-worker`);
  }
});

test("Worker overrides reject malformed public inputs before DOM initialization", () => {
  for (const workerUrl of [null, false, 42, {}, [], "", "   "]) {
    assert.throws(() => new entry.WebTerminal({ url: "/terminal", workerUrl }),
      /workerUrl must be a nonempty URL string or URL/u);
  }
  for (const linkDetectionWorkerUrl of [null, false, 42, {}, [], "", "   "]) {
    assert.throws(() => new entry.WebTerminal({ url: "/terminal", linkDetectionWorkerUrl }),
      /linkDetectionWorkerUrl must be a nonempty URL string or URL/u);
  }
});
