import assert from "node:assert/strict";
import { test } from "node:test";
import { HistoryState } from "../dist/history-state.js";
import { validateHistory } from "../dist/protocol.js";

function history(overrides = {}) {
  return {
    generation: "1", buffer: "main", totalRows: 4, liveTop: 2, top: 2, following: true,
    rowIds: ["3", "4"], requestId: 0,
    selection: { requestId: 0, status: "none", mode: "character", ranges: [], text: null },
    copy: null, ...overrides
  };
}

function selected(requestId = 1, text = "hello") {
  return { requestId, status: "valid", mode: "word", ranges: [{ row: 0, startColumn: 1, endColumn: 6 }], text };
}

test("History metadata validates row identities, extent, generation and exclusive ranges", () => {
  validateHistory(history(), 10, 2);
  validateHistory(history({ selection: selected() }), 10, 2);
  validateHistory(null, 10, 2);
  for (const override of [
    { generation: 1 }, { generation: "0" }, { generation: "9223372036854775808" },
    { buffer: "unsupported" }, { totalRows: 1 }, { totalRows: 4.5 }, { liveTop: 1 },
    { top: -1 }, { top: 3 }, { top: 0 }, { following: "true" },
    { requestId: Number.MAX_SAFE_INTEGER + 1 }, { rowIds: ["3"] },
    { rowIds: ["3", "3"] }, { rowIds: ["3", "-4"] }, { selection: undefined },
    { copy: undefined }, { copy: { requestId: 0, status: "valid", text: "x" } }
  ]) assert.throws(() => validateHistory(history(override), 10, 2), JSON.stringify(override));
  assert.throws(() => validateHistory(undefined, 10, 2));
});

test("History metadata rejects malformed selection text and off-grid or overlapping ranges", () => {
  for (const override of [
    { status: "pending" }, { mode: "paragraph" }, { text: null }, { status: "none", text: "" },
    { ranges: [{ row: 2, startColumn: 1, endColumn: 2 }] },
    { ranges: [{ row: 0, startColumn: 2, endColumn: 2 }] },
    { ranges: [{ row: 0, startColumn: 2, endColumn: 11 }] },
    { ranges: [{ row: 0, startColumn: 2, endColumn: 3 }, { row: 0, startColumn: 4, endColumn: 5 }] },
    { ranges: [null] }
  ]) assert.throws(() => validateHistory(history({ selection: { ...selected(), ...override } }), 10, 2));
});

test("Selection commands reference stable producer rows and report pending until acknowledged", () => {
  const commands = [];
  const state = new HistoryState(command => commands.push(command));
  state.accept(history(), 1);
  state.begin({ x: 2, y: 1 }, { mode: "word" });
  assert.deepEqual(commands[0], { type: "selection", action: "start", mode: "word", requestId: 1,
    generation: "1", rowId: "4", column: 2 });
  assert.equal(state.selection.status, "pending");
  state.accept(history({ selection: selected(1) }), 2);
  assert.equal(state.selection.status, "valid");
  const snapshot = state.selection;
  snapshot.ranges[0].startColumn = 9;
  assert.equal(state.selection.ranges[0].startColumn, 1);
});

test("A selection beyond the presented history rows sends nothing and consumes no request id", () => {
  const commands = [];
  const state = new HistoryState(command => commands.push(command));
  state.accept(history(), 1);
  assert.throws(() => state.begin({ x: 2, y: 2 }, { mode: "character" }), /viewport is not ready/);
  assert.deepEqual(commands, []);
  assert.equal(state.selection.status, "none");
  state.begin({ x: 2, y: 1 }, { mode: "character" });
  assert.equal(commands[0].requestId, 1);
  assert.equal(commands[0].rowId, "4");
});

test("Malformed or unavailable selection points cannot queue extensions or viewport commands", () => {
  for (const point of [{ x: 1, y: -1 }, { x: 1, y: 1.5 }, { x: 1, y: 20 },
    { x: -1, y: 0 }, { x: 1024, y: 0 }, { x: NaN, y: 0 }, null]) {
    const commands = [];
    const state = new HistoryState(command => commands.push(command));
    state.accept(history({ selection: selected(0) }), 1);
    state.scroll(-1);
    assert.throws(() => state.extend(point), /viewport is not ready/);
    if (point) assert.throws(() => state.scroll(-1, point), /viewport is not ready/);
    assert.equal(commands.length, 1);
    state.accept(history({ requestId: 1, selection: selected(0) }), 2);
    assert.equal(commands.length, 1);
  }
});

test("Drag wheel atomically scrolls then extends; released wheel carries no endpoint", () => {
  const commands = [];
  const state = new HistoryState(command => commands.push(command));
  state.accept(history({ selection: selected(0) }), 1);
  state.scroll(-2, { x: 5, y: 1 });
  assert.deepEqual(commands[0], { type: "viewport", requestId: 1, delta: -2, extend: { row: 1, column: 5 } });
  assert.equal(state.selection.status, "pending");
  assert.equal(state.viewport.pending, true);
  state.accept(history({ requestId: 1, top: 0, following: false, rowIds: ["1", "2"], selection: selected(1) }), 2);
  state.scroll(1);
  assert.deepEqual(commands[1], { type: "viewport", requestId: 2, delta: 1 });
  assert.equal(state.selection.status, "valid");
});

test("Pointer movement waits for an outstanding viewport before resolving the new endpoint", () => {
  const commands = [];
  const state = new HistoryState(command => commands.push(command));
  state.accept(history({ selection: selected(0) }), 1);
  state.scroll(-2, { x: 5, y: 1 });
  state.extend({ x: 6, y: 0 });
  assert.equal(commands.length, 1);
  state.accept(history({ requestId: 1, top: 0, following: false, rowIds: ["1", "2"], selection: selected(1) }), 2);
  assert.equal(commands.length, 2);
  assert.equal(commands[1].rowId, "1");
  assert.equal(commands[1].column, 6);
});

test("Release preserves the final deferred endpoint until the delayed viewport arrives", () => {
  const commands = [];
  const state = new HistoryState(command => commands.push(command));
  state.accept(history({ selection: selected(0) }), 1);
  state.scroll(-2, { x: 5, y: 1 });
  state.extend({ x: 6, y: 0 });
  state.endGesture();
  state.accept(history({ requestId: 1, top: 0, following: false, rowIds: ["1", "2"], selection: selected(1) }), 2);
  assert.equal(commands.length, 2);
  assert.deepEqual(commands[1], { type: "selection", action: "extend", mode: "word",
    requestId: 2, generation: "1", rowId: "1", column: 6 });
  state.accept(history({ requestId: 1, top: 0, following: false, rowIds: ["1", "2"],
    selection: selected(2, "final released endpoint") }), 3);
  assert.equal(state.selection.text, "final released endpoint");
  assert.equal(commands.length, 2);
});

test("A deferred endpoint after API scrolling cannot copy the previous selection", () => {
  const commands = [];
  const state = new HistoryState(command => commands.push(command));
  state.accept(history({ selection: selected(0) }), 1);
  state.scroll(-2);
  state.extend({ x: 6, y: 0 });
  state.endGesture();
  assert.equal(state.selection.status, "pending");
  assert.equal(state.selection.mode, "word");
  assert.throws(() => state.copy(), /Resolving selection/);
  assert.equal(commands.length, 1);
  state.accept(history({ requestId: 1, top: 0, following: false, rowIds: ["1", "2"], selection: selected(0) }), 2);
  assert.equal(commands[1].rowId, "1");
  assert.equal(commands[1].column, 6);
  assert.equal(state.selection.status, "pending");
  state.accept(history({ requestId: 1, top: 0, following: false, rowIds: ["1", "2"],
    selection: selected(2, "final endpoint") }), 3);
  assert.equal(state.selection.text, "final endpoint");
});

test("Deferring a new endpoint immediately rejects an already outstanding copy", async () => {
  const state = new HistoryState(() => {});
  state.accept(history({ selection: selected(0) }), 1);
  state.scroll(-2);
  const copy = state.copy();
  const rejection = assert.rejects(copy, /Selection changed/);
  state.extend({ x: 6, y: 0 });
  await rejection;
  assert.equal(state.selection.status, "pending");
  state.endGesture(true);
  assert.equal(state.selection.status, "valid");
});

test("A post-release wheel flushes the final endpoint before scrolling without extending", () => {
  const commands = [];
  const state = new HistoryState(command => commands.push(command));
  state.accept(history({ selection: selected(0) }), 1);
  state.scroll(-2, { x: 5, y: 1 });
  state.extend({ x: 6, y: 0 });
  state.endGesture();
  state.scroll(1);
  assert.deepEqual(commands.slice(1), [
    { type: "viewport", requestId: 2, delta: 0, extend: { row: 0, column: 6 } },
    { type: "viewport", requestId: 3, delta: 1 }
  ]);
  state.accept(history({ requestId: 3, top: 1, following: false, rowIds: ["2", "3"],
    selection: selected(2, "final released endpoint") }), 2);
  assert.equal(state.selection.text, "final released endpoint");
  assert.equal(commands.length, 3);
});

test("Cancellation, input clear, invalidation, generation changes and disconnect drop deferred endpoints", () => {
  for (const cancellation of ["cancel", "input", "invalidated", "generation", "unavailable", "disconnect"]) {
    const commands = [];
    const state = new HistoryState(command => commands.push(command));
    state.accept(history({ selection: selected(0) }), 1);
    state.scroll(-2, { x: 5, y: 1 });
    state.extend({ x: 6, y: 0 });
    state.endGesture();
    if (cancellation === "cancel") state.endGesture(true);
    if (cancellation === "input") state.clear();
    if (cancellation === "disconnect") state.disconnect();
    const next = history({ requestId: 1, top: 0, following: false, rowIds: ["1", "2"], selection: selected(1) });
    if (cancellation === "generation") next.generation = "2";
    if (cancellation === "invalidated") next.selection = {
      ...selected(1), status: "invalidated", ranges: [], text: null
    };
    const sent = commands.length;
    state.accept(cancellation === "unavailable" ? null : next, 2);
    assert.equal(commands.length, sent, `${cancellation} must not send the deferred extension`);
  }
});

test("Return to live retains selection and does not send application input or authority changes", () => {
  const commands = [];
  const state = new HistoryState(command => commands.push(command));
  state.accept(history({ top: 0, following: false, selection: selected(0) }), 1);
  state.live();
  assert.deepEqual(commands, [{ type: "viewport", live: true, requestId: 1 }]);
  assert.equal(state.selection.status, "valid");
});

test("Pending clear is not extendable and immediately removes old highlights", () => {
  const state = new HistoryState(() => {});
  state.accept(history({ selection: selected(0) }), 1);
  state.clear();
  assert.equal(state.selection.status, "pending");
  assert.equal(state.selection.canExtend, false);
  assert.deepEqual(state.selection.ranges, []);
});

test("Copy accepts only its correlated authoritative reply, ignoring old frames and other requests", async () => {
  const commands = [];
  const state = new HistoryState(command => commands.push(command));
  state.accept(history({ selection: selected(0) }), 1);
  const copy = state.copy();
  assert.deepEqual(commands[0], { type: "copy", requestId: 1, selectionRequestId: 0, generation: "1" });
  assert.equal(state.accept(history({ copy: { requestId: 1, status: "valid", text: "stale" } }), 1), false);
  state.accept(history({ selection: selected(0), copy: { requestId: 2, status: "valid", text: "different" } }), 2);
  state.accept(history({ selection: selected(0, "producer text"), copy: { requestId: 1, status: "valid", text: "producer text" } }), 3);
  assert.equal(await copy, "producer text");
});

test("Selection changes reject pending copy and stale replies never resurrect it", async () => {
  const state = new HistoryState(() => {});
  state.accept(history({ selection: selected(0) }), 1);
  const copy = state.copy();
  const rejection = assert.rejects(copy, /Selection changed/);
  state.begin({ x: 2, y: 0 }, { mode: "character" });
  state.accept(history({ selection: selected(0), copy: { requestId: 1, status: "valid", text: "old" } }), 2);
  await rejection;
  assert.equal(state.selection.status, "pending");
});

test("Eviction, reset, unavailable history and disconnection reject copy honestly", async () => {
  for (const action of ["evict", "reset", "unavailable", "disconnect"]) {
    const state = new HistoryState(() => {});
    state.accept(history({ selection: selected(0) }), 1);
    const copy = state.copy();
    const rejection = assert.rejects(copy, /expired|disconnected/);
    if (action === "disconnect") state.disconnect();
    else state.accept(action === "unavailable" ? null : history(action === "reset" ? { generation: "2" } : {
      selection: { ...selected(0), status: "invalidated", text: null, ranges: [] }
    }), 2);
    await rejection;
  }
});

test("Pending and invalidated selections cannot start a copy", () => {
  const state = new HistoryState(() => {});
  assert.throws(() => state.copy(), /not available/);
  state.accept(history(), 1);
  assert.throws(() => state.copy(), /Select text/);
  state.begin({ x: 2, y: 0 }, { mode: "character" });
  assert.throws(() => state.copy(), /Resolving/);
  state.accept(history({ selection: { ...selected(1), status: "invalidated", text: null, ranges: [] } }), 2);
  assert.throws(() => state.copy(), /expired/);
});

test("Output changes between extraction and presentation cannot copy a stale text result", async () => {
  const state = new HistoryState(() => {});
  state.accept(history({ selection: selected(0) }), 1);
  const copy = state.copy();
  const rejection = assert.rejects(copy, /expired/);
  state.accept(history({ selection: selected(0, "new output"),
    copy: { requestId: 1, status: "valid", text: "old output" } }), 2);
  await rejection;
});
