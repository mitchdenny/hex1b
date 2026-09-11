import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { execFileSync } from "node:child_process";
import { readFileSync, statSync } from "node:fs";
import { test } from "node:test";
import * as entry from "@hex1b/web-terminal";
import { normalizeFont } from "../dist/terminal-font.js";

const root = new URL("../", import.meta.url);
const read = path => readFileSync(new URL(path, root), "utf8");

test("Package entry exposes only the supported runtime API", () => {
  assert.deepEqual(Object.keys(entry).sort(), [
    "InputRoute", "MAX_FONT_SIZE", "MIN_FONT_SIZE", "TerminalAction", "WebTerminal", "defaultInputBindings"
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
    "dist/web-terminal.js", "dist/terminal-worker.js", "dist/renderer.js", "dist/protocol.js",
    "dist/backend-selection.js", "dist/render-backend.js", "dist/renderer-options.js",
    "dist/webgpu-backend.js", "dist/webgl2-backend.js",
    "dist/fonts/cascadia-mono-nf/CascadiaMonoNF.woff2",
    "dist/fonts/cascadia-mono-nf/LICENSE.txt", "dist/fonts/cascadia-mono-nf/README.md"]) {
    assert.ok(paths.has(file), `Missing ${file}`);
  }
  for (const path of paths) {
    assert.ok(path.startsWith("dist/") || ["package.json", "README.md", "LICENSE"].includes(path), path);
    if (path.endsWith(".js")) {
      assert.ok(paths.has(`${path}.map`), `Missing source map for ${path}`);
      assert.ok(paths.has(path.replace(/\.js$/u, ".d.ts")), `Missing declaration for ${path}`);
      const source = read(path);
      for (const match of source.matchAll(/(?:from\s+|import\s*)["'](\.\/[^"']+)["']/gu)) {
        assert.ok(paths.has(`dist/${match[1].slice(2)}`), `Missing import ${match[1]} from ${path}`);
      }
      assert.doesNotMatch(source, /samples\/WebTerminalDemo|wwwroot/u);
      const map = JSON.parse(read(`${path}.map`));
      assert.ok(map.sourcesContent.length > 0, "Source maps must remain useful without packaged src/");
    }
  }
  assert.match(read("LICENSE"), /MIT License/u);
});

test("Worker and default font resolve relative to emitted modules", () => {
  assert.match(read("dist/web-terminal.js"), /new Worker\(new URL\("\.\/terminal-worker\.js", import\.meta\.url\)/u);
  assert.match(read("dist/terminal-font.js"), /new URL\("\.\/fonts\/cascadia-mono-nf\/CascadiaMonoNF\.woff2", import\.meta\.url\)/u);
  const font = new URL(normalizeFont().faces[0].url);
  assert.equal(statSync(font).size, 976460);
  assert.equal(createHash("sha256").update(readFileSync(font)).digest("hex"),
    "bd42b0c992de9c42d8a770112e8140d67d6437798be39d49e57f4954e2f6e8e2");
  assert.match(read("dist/fonts/cascadia-mono-nf/LICENSE.txt"), /SIL OPEN FONT LICENSE/u);
  assert.match(read("dist/fonts/cascadia-mono-nf/README.md"), /v2407\.24/u);
});

test("Worker overrides reject malformed public inputs before DOM initialization", () => {
  for (const workerUrl of [null, false, 42, {}, [], "", "   "]) {
    assert.throws(() => new entry.WebTerminal({ url: "/terminal", workerUrl }),
      /workerUrl must be a nonempty URL string or URL/u);
  }
});
