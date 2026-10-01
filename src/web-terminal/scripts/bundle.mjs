import assert from "node:assert/strict";
import { cp, stat } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { build } from "esbuild";

const result = await build({
  absWorkingDir: fileURLToPath(new URL("../", import.meta.url)),
  entryPoints: ["src/index.ts"],
  outfile: "dist/index.js",
  bundle: true,
  splitting: false,
  format: "esm",
  platform: "browser",
  target: "es2022",
  sourcemap: true,
  sourcesContent: true,
  metafile: true
});

assert.deepEqual(Object.keys(result.metafile.outputs).filter(path => path.endsWith(".js")), ["dist/index.js"]);
assert.deepEqual(result.metafile.outputs["dist/index.js"].imports, [], "The bundle must not load other JavaScript files");

await cp(new URL("../.build/", import.meta.url), new URL("../dist/", import.meta.url), {
  recursive: true,
  filter: async source => (await stat(source)).isDirectory() || source.endsWith(".d.ts") || source.endsWith(".d.ts.map")
});
