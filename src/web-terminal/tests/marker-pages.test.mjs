import assert from "node:assert/strict";
import { test } from "node:test";
import { MarkerPages } from "../.build/marker-pages.js";
import { validateHistory } from "../.build/protocol.js";
import { scrollbarGeometry } from "../.build/scrollbar.js";
import { browser, mounting, present } from "./fixtures/browser.mjs";

const marker = id => ({ id: `command:${id}`, source: "command", buffer: "main", row: 0, column: 0,
  phase: "executing", exitCode: null });
const history = (offset, overrides = {}) => ({
  generation: "1", buffer: "main", totalRows: 5, liveTop: 4, top: 4, following: true,
  requestId: 0, rowIds: ["5"], selection: { requestId: 0, status: "none", mode: "character", ranges: [], text: null },
  copy: null, markers: [marker(offset + 1)], markerResult: null,
  markerPage: { revision: "1", offset, total: 2 }, ...overrides
});

test("Inventory pages publish a single complete, ordered history snapshot", () => {
  const pages = new MarkerPages();
  assert.equal(pages.accept(history(0)), undefined);
  assert.equal(pages.pending, true);
  const result = pages.accept(history(1));
  assert.deepEqual(result.markers, [marker(1), marker(2)]);
  assert.equal(result.markerPage, null);
  assert.equal(pages.pending, false);
});

test("large paged inventories preserve full-track positions and restart atomically", () => {
  const pages = new MarkerPages();
  const count = 6000, totalRows = 1_000_000;
  const marks = Array.from({ length: count }, (_, i) => ({
    ...marker(i + 1), row: Math.floor(i * (totalRows - 1) / (count - 1))
  }));
  const page = (offset, revision) => history(offset, {
    totalRows, liveTop: totalRows - 1, top: totalRows - 1,
    markers: marks.slice(offset, offset + 2048),
    markerPage: { revision, offset, total: count }
  });
  assert.equal(pages.accept(page(0, "1")), undefined);
  assert.equal(pages.accept(page(2048, "1")), undefined);
  assert.equal(pages.accept(page(0, "2")), undefined);
  assert.equal(pages.accept(page(2048, "2")), undefined);
  const result = pages.accept(page(4096, "2"));
  assert.deepEqual(result.markers, marks);
  const geometry = scrollbarGeometry({ scrollbar: { left: 100, top: 10, width: 12, height: 403 } },
    { ...result, available: true }, result.markers);
  assert.equal(geometry.markers.length, count);
  assert.equal(geometry.markers[0].bounds.top, 10);
  assert.equal(geometry.markers.at(-1).bounds.top, 408);
  assert.ok(Math.abs(geometry.markers[3000].bounds.top - 209) < 0.1);
});

test("cumulative inventory byte budget rejects rather than publishing a truncated history", () => {
  const pages = new MarkerPages();
  const count = 80_000;
  let bytes = 0, rejected = false;
  for (let offset = 0; offset < count; offset += 2048) {
    const markers = Array.from({ length: Math.min(2048, count - offset) }, (_, i) => marker(offset + i + 1));
    const next = history(offset, { markers, markerPage: { revision: "1", offset, total: count } });
    validateHistory(next, 10, 1);
    bytes += new TextEncoder().encode(JSON.stringify(markers)).length;
    if (bytes <= 8 * 1024 * 1024) assert.equal(pages.accept(next), undefined);
    else {
      assert.throws(() => pages.accept(next), /8 MiB/);
      rejected = true;
      break;
    }
  }
  assert.equal(rejected, true, "The fixture must actually exceed the cumulative wire budget");
  pages.reset();
  assert.equal(pages.pending, false);
});

test("Incomplete inventories restart on page zero and reject inconsistent or duplicated pages", () => {
  const pages = new MarkerPages();
  pages.accept(history(0));
  pages.accept(history(0, { markerPage: { revision: "2", offset: 0, total: 2 } }));
  assert.throws(() => pages.accept(history(1)), /same contiguous/);
  pages.reset();
  assert.throws(() => pages.accept(history(1)), /same contiguous/);
  pages.accept(history(0));
  assert.throws(() => pages.accept(history(1, { top: 3, following: false })), /same contiguous/);
  pages.accept(history(0));
  assert.throws(() => pages.accept(history(1, { markers: [marker(1)] })), /Duplicate marker/);
  assert.equal(pages.accept(null), null);
  assert.equal(pages.pending, false);
});

test("Wire pages validate extents, revisions and nonempty progress", () => {
  validateHistory(history(0), 10, 1);
  for (const page of [{ revision: "", offset: 0, total: 2 }, { revision: "1", offset: -1, total: 2 },
    { revision: "1", offset: 2, total: 2 }, { revision: "1", offset: 0, total: 0 },
    { revision: "1", offset: 0, total: 2147483647 }])
    assert.throws(() => validateHistory(history(0, { markerPage: page }), 10, 1));
  assert.throws(() => validateHistory(history(0, { markers: [] }), 10, 1));
});

test("Worker acknowledges fragments without painting or publishing partial marker/viewport state", async t => {
  const workers = browser(t);
  const inventories = [];
  const view = await mounting(t, workers, { onMarkersChange: markers => inventories.push(markers) });
  await view.worker.request("open");
  await present(view, { revision: 1, history: history(0) });
  assert.equal(view.settled, false);
  assert.deepEqual(inventories, []);
  assert.equal(view.worker.outputs.filter(output => output.type === "geometry").length, 0);
  assert.ok(view.worker.commands.some(command => command.type === "ack" && command.revision === 1));
  await present(view, { revision: 2, history: history(1) });
  const terminal = await view.promise;
  assert.deepEqual(terminal.markers, [marker(1), marker(2)]);
  assert.equal(inventories.length, 1);
  assert.equal(terminal.viewport.top, 4);
  await present(view, { revision: 3, history: history(0, {
    top: 2, following: false, rowIds: ["3"], markerPage: { revision: "2", offset: 0, total: 2 }
  }) });
  assert.equal(terminal.viewport.top, 4, "The previous complete presentation remains authoritative");
  assert.equal(inventories.length, 1);
  await present(view, { revision: 4, history: history(1, {
    top: 2, following: false, rowIds: ["3"], markerPage: { revision: "2", offset: 1, total: 2 }
  }) });
  assert.equal(terminal.viewport.top, 2);
  assert.equal(terminal.stats.frames, 2, "Only complete presentation transactions count as frames");
  terminal.dispose();
});
