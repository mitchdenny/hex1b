import assert from "node:assert/strict";
import { test } from "node:test";
import { decodeFrame } from "../dist/protocol.js";

function metadata() {
  return {
    version: 1, revision: 1, baseRevision: 0, full: true,
    title: "",
    progress: { state: "none", percentage: null },
    shellIntegration: { phase: "unknown", lastExitCode: null },
    workingDirectory: { uri: null, host: null, path: null },
    commandMark: null,
    columns: 1, rows: 1, cellWidth: 10, cellHeight: 20, mouseTracking: 0,
    peer: { id: null, primaryId: null, isPrimary: true },
    cursor: { x: 0, y: 0, visible: false, shape: "SteadyBlock" }, history: null,
    images: [], retainedImages: [], placements: [], warnings: [], hyperlinks: [],
    stats: { workloadBytes: 0, outputBatches: 0, captureMs: 0, elapsedMs: 0 }
  };
}

function frame(state) {
  const json = new TextEncoder().encode(JSON.stringify(state));
  const buffer = new ArrayBuffer(8 + json.byteLength + 4 + 23);
  const view = new DataView(buffer);
  view.setUint32(0, 0x31545748, true);
  view.setUint32(4, json.byteLength, true);
  new Uint8Array(buffer, 8, json.byteLength).set(json);
  const offset = 8 + json.byteLength;
  view.setUint32(offset, 1, true);
  view.setUint8(offset + 4 + 18, 1);
  view.setUint16(offset + 4 + 20, 1, true);
  view.setUint8(offset + 4 + 22, 65);
  return buffer;
}

test("Typed protocol decoding preserves cursor normalization and binary cells", () => {
  const decoded = decodeFrame(frame(metadata()));
  assert.equal(decoded.metadata.cursor.shape, 2);
  assert.equal(decoded.cells.length, 1);
  assert.equal(decoded.cells[0].text, "A");
  assert.equal(decoded.cells[0].width, 1);
});

test("Title metadata preserves empty, literal, and scalar Unicode text at the UTF-16 limit", () => {
  for (const title of ["", "shell; 日本語 😀", "<script>alert(1)</script>", "left\u202eright",
    "\ufffd", "a".repeat(4096), "a".repeat(4094) + "😀", "😀".repeat(2048)]) {
    assert.equal(decodeFrame(frame({ ...metadata(), title })).metadata.title, title);
  }
});

test("Title metadata is required and rejects controls, oversized strings, and lone surrogates", () => {
  const controls = [...Array(32).keys(), ...Array.from({ length: 33 }, (_, i) => 127 + i)];
  for (const title of [undefined, null, 42, false, {}, [], "a".repeat(4097),
    "a".repeat(4095) + "😀", "\ud800", "\udfff", "\ud800x", "x\udfff", "\udfff\ud800",
    ...controls.map(code => `prefix${String.fromCharCode(code)}suffix`)]) {
    assert.throws(() => decodeFrame(frame({ ...metadata(), title })), /Invalid terminal title/u);
  }
});

test("Typed protocol boundary still rejects malformed metadata before use", () => {
  for (const value of [null, [], false, "metadata"]) {
    assert.throws(() => decodeFrame(frame(value)));
  }
  for (const [field, invalid] of [
    ["columns", "1"], ["rows", -1], ["cellWidth", 11], ["mouseTracking", 1],
    ["cursor", null], ["peer", []], ["stats", {}], ["history", {}], ["warnings", [1]],
    ["images", [null]], ["retainedImages", ["duplicate", "duplicate"]], ["placements", [null]]
  ]) {
    const state = metadata();
    state[field] = invalid;
    assert.throws(() => decodeFrame(frame(state)), field);
  }
});

test("Activity metadata preserves all progress states and signed shell exit codes", () => {
  for (const state of ["none", "normal", "error", "indeterminate", "warning"]) {
    for (const percentage of state === "none" || state === "indeterminate" ? [null] : [0, 50, 100]) {
      const progress = { state, percentage };
      assert.deepEqual(decodeFrame(frame({ ...metadata(), progress })).metadata.progress, progress);
    }
  }
  for (const phase of ["unknown", "prompt", "commandLine", "executing", "finished"]) {
    for (const lastExitCode of phase === "unknown" ? [null] : [null, -2147483648, -1, 0, 1, 2147483647]) {
      const shellIntegration = { phase, lastExitCode };
      assert.deepEqual(decodeFrame(frame({ ...metadata(), shellIntegration })).metadata.shellIntegration, shellIntegration);
    }
  }
});

test("Activity metadata rejects missing, inconsistent, and out-of-range state", () => {
  for (const progress of [undefined, null, {}, [], { state: 1, percentage: 50 },
    { state: "busy", percentage: null }, { state: "none", percentage: 0 },
    { state: "indeterminate" }, { state: "indeterminate", percentage: 50 },
    ...[undefined, null, -1, 101, 1.5, "50"].map(percentage => ({ state: "normal", percentage }))]) {
    assert.throws(() => decodeFrame(frame({ ...metadata(), progress })), /progress/u);
  }
  for (const shellIntegration of [undefined, null, {}, [], { phase: "idle", lastExitCode: null },
    { phase: 3, lastExitCode: 0 }, { phase: "unknown", lastExitCode: 0 },
    ...[undefined, -2147483649, 2147483648, .5, "0"].map(lastExitCode => ({ phase: "finished", lastExitCode }))]) {
    assert.throws(() => decodeFrame(frame({ ...metadata(), shellIntegration })), /shell/u);
  }
});

test("Typed protocol boundary rejects every truncated frame and trailing payload", () => {
  const buffer = frame(metadata());
  for (let length = 0; length < buffer.byteLength; length++) {
    assert.throws(() => decodeFrame(buffer.slice(0, length)), `length ${length}`);
  }
  const trailing = new Uint8Array(buffer.byteLength + 1);
  trailing.set(new Uint8Array(buffer));
  assert.throws(() => decodeFrame(trailing.buffer), /Image payload length mismatch/u);
});

test("Hyperlink metadata preserves URI data and rejects malformed or overlapping ranges", () => {
  const link = { row: 0, startColumn: 0, endColumn: 1, uri: "https://example.com" };
  assert.deepEqual(decodeFrame(frame({ ...metadata(), hyperlinks: [link] })).metadata.hyperlinks, [link]);
  for (const hyperlinks of [undefined, null, {}, [null], [link, link],
    [{ ...link, row: 1 }], [{ ...link, row: -1 }], [{ ...link, row: .5 }],
    [{ ...link, startColumn: -1 }], [{ ...link, startColumn: "0" }],
    [{ ...link, endColumn: 0 }], [{ ...link, endColumn: 2 }],
    [{ ...link, uri: "" }], [{ ...link, uri: null }], [{ ...link, uri: 42 }]]) {
    assert.throws(() => decodeFrame(frame({ ...metadata(), hyperlinks })));
  }
});
