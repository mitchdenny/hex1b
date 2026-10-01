import test from "node:test";
import assert from "node:assert/strict";
import { performance } from "node:perf_hooks";
import { extractLinkText } from "../.build/link-text.js";
import { scanLinks } from "../.build/link-detection-worker.js";
import { LINK_LIMITS } from "../.build/link-options.js";

test("representative and maximum grids stay within extraction and preset scan budgets", t => {
  for (const [columns, rows] of [[80, 24], [1024, 256], [512, 512]]) {
    const line = " See https://example.test/a(b) /usr/local/bin mailto:a@b.test ".padEnd(columns, " ");
    const snapshot = { revision: 1, columns, rows, hyperlinks: [],
      cells: Array.from({ length: columns * rows }, (_, index) => ({
        index, text: line[index % columns], width: 1, attributes: 0,
        foreground: 0, background: 0, underlineColor: 0, underlineStyle: 0,
      })) };
    let extractMs = 0, scanMs = 0, messageBytes = 0;
    const iterations = 5;
    for (let iteration = 0; iteration < iterations; iteration++) {
      let start = performance.now();
      const chunks = extractLinkText(snapshot, "logicalLine")
        .map(({ key, chunk }) => ({ key, text: chunk.text }));
      extractMs += performance.now() - start;
      assert.equal(chunks.reduce((sum, chunk) => sum + chunk.text.length, 0), columns * rows);
      const request = { id: iteration, rule: { builtin: "uri" }, chunks };
      start = performance.now();
      const response = scanLinks(request);
      scanMs += performance.now() - start;
      assert.equal(response.error, undefined);
      assert.equal(response.results.reduce((sum, chunk) => sum + chunk.matches.length, 0), rows * 2);
      messageBytes = Buffer.byteLength(JSON.stringify({ request, response }));
    }
    t.diagnostic(JSON.stringify({ columns, rows, iterations,
      meanExtractionMs: Number((extractMs / iterations).toFixed(3)),
      meanPresetScanMs: Number((scanMs / iterations).toFixed(3)),
      requestAndResponseJsonBytes: messageBytes }));
    assert.ok(columns * rows <= LINK_LIMITS.cells);
  }
});
