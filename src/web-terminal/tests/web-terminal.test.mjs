import assert from "node:assert/strict";
import { test } from "node:test";
import { decodeFrame } from "../dist/protocol.js";
import { TerminalRenderer } from "../dist/renderer.js";
import { normalizeFont, measureFont } from "../dist/terminal-font.js";
import { normalizeSizing, requestedGrid, fittedScale } from "../dist/terminal-sizing.js";

test("Sizing defaults to Auto at the original 16px cell scale", () => {
  assert.deepEqual(normalizeSizing(), { mode: "auto", fontSize: 16 });
  assert.deepEqual(normalizeSizing({ mode: "fixed", columns: 80, rows: 24 }, 12),
    { mode: "fixed", fontSize: 12, columns: 80, rows: 24 });
  assert.deepEqual(normalizeSizing({ mode: "auto" }, 12), { mode: "auto", fontSize: 12 });
});

test("Sizing validates mode, font limits, and fixed grid bounds", () => {
  for (const sizing of [null, {}, { mode: "zoom" }, { mode: "auto", fontSize: 7 },
    { mode: "auto", fontSize: 33 }, { mode: "auto", fontSize: 12.5 },
    { mode: "fixed", columns: 301, rows: 24 }, { mode: "fixed", columns: 80, rows: 9 }]) {
    assert.throws(() => normalizeSizing(sizing));
  }
  assert.equal(normalizeSizing({ mode: "auto", fontSize: 8 }).fontSize, 8);
  assert.equal(normalizeSizing({ mode: "auto", fontSize: 32 }).fontSize, 32);
});

const cellGeometry = { columns: 80, rows: 24, cellWidth: 10, cellHeight: 20 };

test("Smaller Auto text fits more cells into the same container", () => {
  const size = { width: 800, height: 480 };
  assert.deepEqual(requestedGrid(size, cellGeometry, normalizeSizing()), { columns: 80, rows: 24 });
  assert.deepEqual(requestedGrid(size, cellGeometry, normalizeSizing({ mode: "auto", fontSize: 12 })), { columns: 106, rows: 32 });
  assert.deepEqual(requestedGrid(size, cellGeometry, normalizeSizing({ mode: "auto", fontSize: 8 })), { columns: 160, rows: 48 });
  assert.deepEqual(requestedGrid(size, cellGeometry, normalizeSizing({ mode: "auto", fontSize: 32 })), { columns: 40, rows: 12 });
});

test("Auto sizing retains local grid bounds and ignores hidden containers", () => {
  assert.deepEqual(requestedGrid({ width: 1, height: 1 }, cellGeometry, normalizeSizing()), { columns: 20, rows: 10 });
  assert.deepEqual(requestedGrid({ width: 10000, height: 10000 }, cellGeometry, normalizeSizing()), { columns: 300, rows: 100 });
  assert.equal(requestedGrid({ width: 0, height: 480 }, cellGeometry, normalizeSizing()), null);
});

test("Explicit fixed sizing does not derive dimensions from the container", () => {
  const fixed = normalizeSizing({ mode: "fixed", columns: 120, rows: 40 });
  for (const size of [{ width: 800, height: 480 }, { width: 240, height: 80 }, { width: 0, height: 0 }]) {
    assert.deepEqual(requestedGrid(size, cellGeometry, fixed), { columns: 120, rows: 40 });
  }
});

test("Primary Auto fitting caps at the requested font size", () => {
  const size = { width: 1600, height: 960 };
  assert.equal(fittedScale(size, cellGeometry, true, normalizeSizing()), 1);
  assert.equal(fittedScale(size, cellGeometry, true, normalizeSizing({ mode: "auto", fontSize: 12 })), .75);
  assert.equal(fittedScale(size, cellGeometry, true, normalizeSizing({ mode: "auto", fontSize: 32 })), 2);
});

test("Fixed primary and secondary views both contain-fit the authoritative grid", () => {
  const size = { width: 1600, height: 960 };
  assert.equal(fittedScale(size, cellGeometry, true, normalizeSizing({ mode: "fixed", columns: 80, rows: 24 })), 2);
  assert.equal(fittedScale(size, cellGeometry, false, normalizeSizing({ mode: "auto", fontSize: 8 })), 2);
  assert.equal(fittedScale({ width: 200, height: 200 }, cellGeometry, false, normalizeSizing()), .25);
});

test("Hidden fitted surfaces remain zero-sized in either sizing mode", () => {
  for (const sizing of [normalizeSizing(), normalizeSizing({ mode: "fixed", columns: 80, rows: 24 })]) {
    assert.equal(fittedScale({ width: 0, height: 0 }, cellGeometry, true, sizing), 0);
  }
});

test("Default font uses the bundled variable Cascadia Mono NF face", () => {
  const font = normalizeFont();
  assert.equal(font.family, "Cascadia Mono NF");
  assert.ok(font.faces[0].url.endsWith("/fonts/cascadia-mono-nf/CascadiaMonoNF.woff2"));
  assert.equal(font.faces[0].weight, "200 700");
  assert.equal(font.faces[0].style, "normal");
});

test("Custom font sources resolve against the caller and copy descriptors", () => {
  const input = { family: "Developer Font", faces: [{ url: "../fonts/custom.woff2", weight: "100 900", style: "italic" }] };
  const result = normalizeFont(input, "https://example.test/app/view");
  assert.deepEqual(result, { family: "Developer Font", faces: [
    { url: "https://example.test/fonts/custom.woff2", weight: "100 900", style: "italic" }
  ] });
  result.faces[0].weight = "400";
  assert.equal(input.faces[0].weight, "100 900");
  assert.deepEqual(normalizeFont({ family: "monospace" }), { family: "monospace", faces: [] });
});

test("Font configuration rejects malformed families and face descriptors", () => {
  for (const font of ["Example", [], {}, { family: "" }, { family: "bad\nfont" },
    { family: "Example", faces: "file.woff2" }, { family: "Example", faces: [{}] },
    { family: "Example", faces: [{ url: "file.woff2", weight: 700 }] }]) {
    assert.throws(() => normalizeFont(font), TypeError);
  }
});

test("Font-wide metrics map advance, ascent, and descent to the complete cell", () => {
  const raster = { measureText: text => {
    assert.equal(text, "M");
    return { width: 18.75, fontBoundingBoxAscent: 30, fontBoundingBoxDescent: 8 };
  } };
  const metrics = measureFont(raster, '"Developer Font"', 5, 2, 10, 20);
  assert.equal(metrics.font, 'italic bold 32px "Developer Font"');
  assert.equal(18.75 * metrics.xScale, 20);
  assert.equal(38 * metrics.yScale, 40);
  assert.equal(metrics.baseline + 8 * metrics.yScale, 40);
  assert.equal(raster.textBaseline, "alphabetic");
  assert.equal(raster.textAlign, "left");
});

test("Unusable font metrics fail instead of silently choosing another font", () => {
  for (const metrics of [{ width: 0, fontBoundingBoxAscent: 15, fontBoundingBoxDescent: 4 },
    { width: 10 }, { width: 10, fontBoundingBoxAscent: 0, fontBoundingBoxDescent: 0 }]) {
    assert.throws(() => measureFont({ measureText: () => metrics }, "monospace", 0, 1, 10, 20), /Invalid terminal font metrics/);
  }
});

function frame(peer) {
  const metadata = {
    version: 1, revision: 1, baseRevision: 0, full: true,
    title: "",
    progress: { state: "none", percentage: null },
    shellIntegration: { phase: "unknown", lastExitCode: null },
    workingDirectory: { uri: null, host: null, path: null },
    commandMark: null,
    columns: 1, rows: 1, cellWidth: 10, cellHeight: 20, mouseTracking: 0, peer,
    cursor: { x: 0, y: 0, visible: false, shape: 0 }, history: null,
    images: [], retainedImages: [], placements: [], warnings: [], hyperlinks: [],
    stats: { workloadBytes: 0, outputBatches: 0, captureMs: 0, elapsedMs: 0 }
  };
  const json = new TextEncoder().encode(JSON.stringify(metadata));
  const bytes = new Uint8Array(8 + json.length + 4 + 23);
  const view = new DataView(bytes.buffer);
  view.setUint32(0, 0x31545748, true);
  view.setUint32(4, json.length, true);
  bytes.set(json, 8);
  const offset = 8 + json.length;
  view.setUint32(offset, 1, true);
  view.setUint8(offset + 4 + 18, 1);
  view.setUint16(offset + 4 + 20, 1, true);
  bytes[offset + 4 + 22] = 65;
  return bytes.buffer;
}

test("HWT1 peer metadata accepts standalone, connecting, primary, and secondary states", () => {
  for (const peer of [
    { id: null, primaryId: null, isPrimary: true },
    { id: null, primaryId: null, isPrimary: false },
    { id: "a", primaryId: "a", isPrimary: true },
    { id: "b", primaryId: "a", isPrimary: false },
    { id: "b", primaryId: null, isPrimary: false }
  ]) assert.deepEqual(decodeFrame(frame(peer)).metadata.peer, peer);
});

test("HWT1 peer metadata rejects malformed or contradictory authority", () => {
  for (const peer of [
    undefined, null,
    { id: "", primaryId: null, isPrimary: false },
    { id: "b", isPrimary: false },
    { id: "a", primaryId: "b", isPrimary: true },
    { id: "a", primaryId: "a", isPrimary: false },
    { id: "a", primaryId: null, isPrimary: "false" },
    { id: "a".repeat(257), primaryId: null, isPrimary: false }
  ]) assert.throws(() => decodeFrame(frame(peer)));
});

function renderer(scale = 2, limit = 8192) {
  const writes = [];
  const result = Object.create(TerminalRenderer.prototype);
  Object.assign(result, {
    scale, columns: 0, rows: 0, canvas: { width: 0, height: 0 },
    backend: { maxCanvasDimension2D: limit,
      resize: (width, height) => writes.push(new Float32Array([width, height, 0, 0])) }
  });
  return { result, writes };
}

test("Primary framebuffer retains native requested resolution", () => {
  const { result, writes } = renderer();
  result.resize(100, 30, { width: 2000, height: 1200 });
  assert.equal(result.canvas.width, 2000);
  assert.equal(result.canvas.height, 1200);
  assert.equal(result.backingScale, 2);
  assert.equal(result.canvasLimited, false);
  assert.deepEqual([...writes[0]], [1000, 600, 0, 0]);
});

test("Thumbnail resizing changes resolution without changing the authoritative grid or glyph raster scale", () => {
  const { result } = renderer();
  result.resize(100, 30, { width: 2000, height: 1200 });
  result.resize(100, 30, { width: 300, height: 180 });
  assert.equal(result.canvas.width, 300);
  assert.equal(result.canvas.height, 180);
  assert.equal(result.backingScale, .3);
  assert.equal(result.scale, 2);
  assert.equal(result.columns, 100);
  assert.equal(result.rows, 30);
  assert.equal(result.width, 1000);
  assert.equal(result.height, 600);
});

test("Large remote geometry caps canvas resolution rather than changing or rejecting the grid", () => {
  const { result } = renderer(3);
  result.resize(1000, 200);
  assert.equal(result.columns, 1000);
  assert.equal(result.rows, 200);
  assert.equal(result.canvas.width, 8192);
  assert.ok(result.canvas.height <= 8192);
  assert.equal(result.canvasLimited, true);
  assert.equal(result.scale, 3);
});

test("Hidden and restored surfaces retain their logical state", () => {
  const { result } = renderer();
  result.resize(120, 40, { width: 0, height: 0 });
  assert.deepEqual([result.canvas.width, result.canvas.height], [1, 1]);
  assert.deepEqual([result.columns, result.rows, result.width, result.height], [120, 40, 1200, 800]);
  result.resize(120, 40, { width: 2400, height: 1600 });
  assert.deepEqual([result.canvas.width, result.canvas.height], [2400, 1600]);
  assert.equal(result.backingScale, 2);
});

test("Unchanged geometry does not rewrite the uniform, but equal-aspect remote grids still update it", () => {
  const { result, writes } = renderer();
  result.resize(80, 40, { width: 400, height: 400 });
  result.resize(80, 40, { width: 400, height: 400 });
  assert.equal(writes.length, 1);
  result.resize(160, 80, { width: 400, height: 400 });
  assert.equal(writes.length, 2);
  assert.deepEqual([...writes[1]], [1600, 1600, 0, 0]);
});
