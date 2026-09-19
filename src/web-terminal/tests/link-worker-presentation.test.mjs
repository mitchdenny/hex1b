import assert from "node:assert/strict";
import { test } from "node:test";
import { LinkPresentation } from "../.build/link-presentation.js";
import { browser, frame } from "./fixtures/browser.mjs";

const cell = Object.freeze({ index: 0, text: "a", width: 1, attributes: 0, foreground: 1,
  background: 2, underlineColor: 3, underlineStyle: 0 });
const cells = [cell];
const metadata = { revision: 1, columns: 1, rows: 1, hyperlinks: [], history: null };
const ranges = [{ row: 0, startColumn: 0, endColumn: 1 }];

test("Snapshot cache ignores colors, cursor, image and non-text SGR changes", () => {
  const links = new LinkPresentation();
  assert.deepEqual(links.present(cells, metadata), {});
  links.configure(true, 1);
  assert.equal(links.snapshot().cells, cells);
  assert.equal(links.snapshot(), undefined);
  const colored = [{ ...cell, foreground: 5, background: 6, underlineColor: 7,
    underlineStyle: 3, attributes: 1 | 2 | 4 | 8 | 16 | 32 | 128 | 256 }];
  assert.deepEqual(links.present(colored, { ...metadata, revision: 2,
    cursor: { visible: true }, placements: [{ key: "image" }] }), { linkGeneration: 1 });
  links.configure(true, 2);
  assert.equal(links.snapshot().revision, 2);
  links.configure(false, 3);
  assert.equal(links.snapshot(), undefined);
  assert.deepEqual(links.present(cells, metadata), {});
});

test("Disabled detection does no cell or metadata inspection", () => {
  const links = new LinkPresentation();
  const inaccessible = new Proxy({}, { get() { assert.fail("Disabled detection inspected content"); } });
  links.prepare(inaccessible, inaccessible);
  assert.deepEqual(links.present(inaccessible, inaccessible), {});
  assert.equal(links.snapshot(), undefined);
});

test("Snapshots invalidate for text, width, hidden, soft-wrap, placeholder, dimensions, history and OSC 8", () => {
  const variants = [
    [[{ ...cell, text: "b" }], metadata],
    [[{ ...cell, width: 0 }], metadata],
    [[{ ...cell, attributes: 64 }], metadata],
    [[{ ...cell, attributes: 1024 }], metadata],
    [[{ ...cell, text: "\u{10eeee}" }], metadata],
    [[undefined], metadata],
    [cells, { ...metadata, columns: 2 }],
    [cells, { ...metadata, rows: 2 }],
    [cells, { ...metadata, history: { generation: "1", buffer: "normal", rowIds: ["1"] } }],
    [cells, { ...metadata, hyperlinks: [{ ...ranges[0], uri: "https://example.test" }] }]
  ];
  for (const [nextCells, nextMetadata] of variants) {
    const links = new LinkPresentation();
    links.configure(true, 1);
    links.present(cells, metadata);
    assert.ok(links.accept(1, 1, 1, ranges, false));
    links.prepare(nextCells, nextMetadata);
    assert.equal(links.submission().mask, undefined);
    assert.ok(links.present(nextCells, nextMetadata).linkSnapshot);
  }
  for (const change of [{ generation: "2" }, { buffer: "alternate" }, { rowIds: ["2"] }]) {
    const links = new LinkPresentation();
    links.configure(true, 1);
    const previous = { ...metadata, history: { generation: "1", buffer: "normal", rowIds: ["1"] } };
    links.present(cells, previous);
    assert.ok(links.present(cells, { ...previous, history: { ...previous.history, ...change } }).linkSnapshot);
  }
});

test("Only the matching submitted decoration token may be acknowledged", () => {
  const links = new LinkPresentation();
  links.configure(true, 1);
  links.present(cells, metadata);
  assert.equal(links.accept(1, 1, 1, ranges, true), false);
  assert.equal(links.accept(2, 1, 1, ranges, false), false);
  assert.equal(links.accept(1, 2, 1, ranges, false), false);
  assert.ok(links.accept(1, 1, 1, ranges, false));
  const first = links.submission();
  assert.ok(links.accept(1, 1, 2, [], false));
  assert.equal(first.mask[0], 1, "new ranges do not mutate an in-flight submission");
  assert.equal(links.acknowledge(first.acknowledgement, false), undefined);
  const second = links.submission();
  assert.equal(second.mask[0], 0);
  assert.equal(links.acknowledge(second.acknowledgement, true), undefined);
  assert.deepEqual(links.acknowledge(second.acknowledgement, false), { revision: 1, generation: 1, serial: 2 });
  assert.equal(links.acknowledge(second.acknowledgement, false), undefined);
  assert.equal(links.accept(1, 1, 1, ranges, false), false);
  links.configure(false, 2);
  assert.equal(links.submission().mask, undefined);
});

test("Unchanged authoritative content preserves masks but never stale revision acknowledgements", () => {
  const links = new LinkPresentation();
  links.configure(true, 1);
  links.present(cells, metadata);
  links.accept(1, 1, 1, ranges, false);
  const submitted = links.submission();
  links.prepare(cells, { ...metadata, revision: 2 });
  assert.equal(links.submission().mask, submitted.mask);
  links.present(cells, { ...metadata, revision: 2 });
  assert.equal(links.acknowledge(submitted.acknowledgement, false), undefined);
  assert.ok(links.accept(2, 1, 2, ranges, false));
});

test("Local decoration masks carry underline style and replace it without changing cells", () => {
  const links = new LinkPresentation();
  links.configure(true, 1);
  links.present(cells, metadata);
  assert.equal(links.accept(1, 1, 1, ranges, false, "dashed"), true);
  const dashed = links.submission();
  assert.deepEqual([...dashed.mask], [5]);
  assert.equal(links.accept(1, 1, 2, ranges, false, "solid"), true);
  assert.deepEqual([...links.submission().mask], [1]);
  assert.deepEqual([...dashed.mask], [5], "an in-flight mask remains unchanged");
  assert.throws(() => links.accept(1, 1, 3, ranges, false, "wavy"), /Invalid link decorations/);
  assert.equal(cell.underlineStyle, 0);
});

async function worker(t) {
  browser(t);
  const worker = new Worker("terminal-worker.js");
  await worker.request("input", { message: { type: "init", canvas: {}, scale: 1,
    font: {}, renderer: "webgl2", url: "wss://example.test" } });
  await worker.request("open");
  return worker;
}
const input = (worker, message) => worker.request("input", { message });
const outputs = (worker, type) => worker.outputs.filter(message => message.type === type);

test("Real worker enables on static presented state and decorates only after GPU idle without duplicate server ack", async t => {
  const w = await worker(t);
  await w.request("frame", { buffer: frame() });
  await w.request("draw");
  assert.equal(outputs(w, "geometry")[0].linkSnapshot, undefined);
  await input(w, { type: "linkDetection", enabled: true, generation: 1 });
  assert.equal(outputs(w, "linkSnapshot").length, 1);
  assert.equal(outputs(w, "linkSnapshot")[0].snapshot.revision, 1);
  await input(w, { type: "linkDecorations", revision: 1, generation: 1, serial: 1, ranges });
  await w.request("hold");
  await w.request("draw");
  assert.equal(outputs(w, "linkDecorations").length, 0);
  await input(w, { type: "linkDecorations", revision: 1, generation: 1, serial: 2, ranges: [] });
  await w.request("release");
  assert.equal(outputs(w, "linkDecorations").length, 0);
  await w.request("draw");
  assert.deepEqual(outputs(w, "linkDecorations"), [{ type: "linkDecorations", revision: 1, generation: 1, serial: 2 }]);
  assert.equal(outputs(w, "geometry").length, 1);
  assert.deepEqual(w.commands.filter(command => command.type === "ack"), [{ type: "ack", revision: 1 }]);
});

test("Real worker snapshots only presented frames and rebases unchanged geometry without cloned cells", async t => {
  const w = await worker(t);
  await input(w, { type: "linkDetection", enabled: true, generation: 1 });
  await w.request("frame", { buffer: frame() });
  await w.request("hold");
  await w.request("draw");
  assert.equal(outputs(w, "geometry").length, 0);
  assert.equal(outputs(w, "linkSnapshot").length, 0);
  await input(w, { type: "linkDetection", enabled: true, generation: 2 });
  assert.equal(outputs(w, "linkSnapshot").length, 0);
  await w.request("release");
  assert.equal(outputs(w, "geometry")[0].linkSnapshot.revision, 1);
  assert.equal(outputs(w, "geometry")[0].linkGeneration, 2);
  await w.request("frame", { buffer: frame({ revision: 2 }) });
  await input(w, { type: "linkDecorations", revision: 1, generation: 2, serial: 1, ranges });
  await w.request("draw");
  assert.equal(outputs(w, "geometry")[1].linkGeneration, 2);
  assert.equal(outputs(w, "geometry")[1].linkSnapshot, undefined);
  assert.equal(outputs(w, "linkDecorations").length, 0);
  await w.request("frame", { buffer: frame({ revision: 3, cells: [{ index: 0, text: "B", width: 1 }] }) });
  await w.request("draw");
  assert.equal(outputs(w, "geometry")[2].linkSnapshot.cells[0].text, "B");
  await input(w, { type: "linkDetection", enabled: false, generation: 3 });
  await w.request("frame", { buffer: frame({ revision: 4, cells: [{ index: 0, text: "C", width: 1 }] }) });
  await w.request("draw");
  assert.equal(outputs(w, "geometry")[3].linkGeneration, undefined);
  assert.equal(outputs(w, "geometry")[3].linkSnapshot, undefined);
  assert.equal(w.commands.filter(command => command.type === "ack").length, 4);
});

test("Newer frames and configuration changes suppress acknowledgements of in-flight local draws", async t => {
  const w = await worker(t);
  await input(w, { type: "linkDetection", enabled: true, generation: 1 });
  await w.request("frame", { buffer: frame() });
  await w.request("draw");
  await input(w, { type: "linkDecorations", revision: 1, generation: 1, serial: 1, ranges });
  await w.request("hold");
  await w.request("draw");
  await w.request("frame", { buffer: frame({ revision: 2 }) });
  await w.request("release");
  assert.equal(outputs(w, "linkDecorations").length, 0);
  await w.request("draw");
  assert.equal(outputs(w, "geometry").at(-1).revision, 2);
  await input(w, { type: "linkDecorations", revision: 2, generation: 1, serial: 2, ranges });
  await w.request("hold");
  await w.request("draw");
  await input(w, { type: "linkDetection", enabled: false, generation: 2 });
  await w.request("release");
  await w.request("draw");
  assert.equal(outputs(w, "linkDecorations").length, 0);
  assert.equal(outputs(w, "geometry").length, 2);
  assert.equal(w.commands.filter(command => command.type === "ack").length, 2);
});
