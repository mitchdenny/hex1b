import assert from "node:assert/strict";
import { test } from "node:test";
import { normalizePadding, contentSpace, terminalLayout } from "../.build/terminal-layout.js";
import { normalizeSizing, requestedGrid } from "../.build/terminal-sizing.js";
import { normalizeScrollbar } from "../.build/scrollbar.js";

const geometry = { columns: 80, rows: 24, cellWidth: 10, cellHeight: 20, mouseTracking: 0 };
const size = { width: 840, height: 520 };
const sizing = normalizeSizing();

test("Padding is immutable, defaults to zero and supports asymmetric CSS pixels", () => {
  assert.deepEqual(normalizePadding(), { top: 0, right: 0, bottom: 0, left: 0 });
  assert.deepEqual(normalizePadding(8), { top: 8, right: 8, bottom: 8, left: 8 });
  const padding = normalizePadding({ left: 12.5, bottom: 24 });
  assert.deepEqual(padding, { top: 0, right: 0, bottom: 24, left: 12.5 });
  assert.ok(Object.isFrozen(padding));
  for (const bad of [null, [], "8px", -1, Infinity, NaN, { left: -1 }, { top: "2" },
    { bottom: Infinity }, { right: null }])
    assert.throws(() => normalizePadding(bad));
});

test("Beside gutter and padding reduce requested auto columns without becoming terminal cells", () => {
  const padding = normalizePadding(20);
  const overlay = normalizeScrollbar();
  const beside = normalizeScrollbar({ placement: "beside", width: 12 });
  const overlaySpace = contentSpace(size, padding, overlay);
  const besideSpace = contentSpace(size, padding, beside);
  assert.deepEqual(overlaySpace, { width: 800, height: 480 });
  assert.deepEqual(besideSpace, { width: 788, height: 480 });
  assert.deepEqual(requestedGrid(overlaySpace, geometry, sizing), { columns: 80, rows: 24 });
  assert.deepEqual(requestedGrid(besideSpace, geometry, sizing), { columns: 78, rows: 24 });
  assert.deepEqual(requestedGrid(besideSpace, geometry, normalizeSizing({ mode: "fixed", columns: 80, rows: 24 })),
    { columns: 80, rows: 24 });
});

test("Overlay covers only the content edge; beside fits full grid then places its gutter", () => {
  const overlay = terminalLayout(size, geometry, true, sizing, normalizePadding(20), normalizeScrollbar());
  assert.deepEqual(overlay.content, { left: 20, top: 20, width: 800, height: 480 });
  assert.deepEqual(overlay.scrollbar, { left: 808, top: 20, width: 12, height: 480 });
  assert.equal(overlay.cellWidth, 10);
  const beside = terminalLayout(size, geometry, false, sizing, normalizePadding(20),
    normalizeScrollbar({ placement: "beside" }));
  assert.equal(beside.content.width, 788);
  assert.equal(beside.scrollbar.left, beside.content.left + beside.content.width);
  assert.equal(beside.scrollbar.left + beside.scrollbar.width, 820);
  assert.equal(beside.content.width / beside.cellWidth, 80);
  assert.equal(beside.content.height / beside.cellHeight, 24);
  assert.ok(Object.isFrozen(beside) && Object.isFrozen(beside.content) && Object.isFrozen(beside.scrollbar));
});

test("Empty and tiny containers never generate negative content or off-box padding origins", () => {
  for (const box of [{ width: 0, height: 0 }, { width: 1, height: 1 }, { width: 20, height: 15 }]) {
    for (const scrollbar of [false, normalizeScrollbar(), normalizeScrollbar({ placement: "beside" })]) {
      const layout = terminalLayout(box, geometry, true, sizing, normalizePadding(30), scrollbar);
      assert.equal(layout.content.width, 0);
      assert.equal(layout.content.height, 0);
      assert.ok(layout.content.left <= box.width);
      assert.ok(layout.content.top <= box.height);
      assert.ok(Number.isFinite(layout.cellWidth) && Number.isFinite(layout.cellHeight));
    }
  }
});

test("Disabling scrollbar preserves padding and exposes no scrollbar rectangle", () => {
  const layout = terminalLayout(size, geometry, true, sizing, normalizePadding({ left: 30, right: 10 }), false);
  assert.equal(layout.scrollbar, null);
  assert.equal(layout.content.left, 30);
  assert.equal(layout.content.width, 800);
});
