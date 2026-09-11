import assert from "node:assert/strict";
import { test } from "node:test";
import { SelectionUI, sameSelectionUIState, selectionRectangles } from "../dist/selection-ui.js";
import { WebTerminal } from "../dist/web-terminal.js";

function snapshot() {
  return {
    connected: true, readOnly: false,
    selection: { status: "valid", active: true, pending: false, copying: false, copyError: "",
      requestId: 1, revision: 1, mode: "character", text: "hello",
      ranges: [{ row: 2, startColumn: 3, endColumn: 8 }] },
    viewport: { available: true, following: true, pending: false, generation: "1", buffer: "main",
      top: 0, liveTop: 0, totalRows: 10, requestId: 0, revision: 1, rowIds: ["1"] },
    geometry: { columns: 20, rows: 10, cellWidth: 10, cellHeight: 20, mouseTracking: 0 },
    canvasSize: { width: 300, height: 300 }
  };
}

test("Selection UI compares meaningful state, not frame revisions or unrelated cell row identities", () => {
  const previous = snapshot();
  const next = snapshot();
  next.selection.revision++;
  next.viewport.revision++;
  next.viewport.rowIds = ["2"];
  assert.equal(sameSelectionUIState(previous, next), true);
  for (const change of [
    state => state.selection.copying = true,
    state => state.selection.copyError = "denied",
    state => state.selection.ranges[0].row++,
    state => state.viewport.top++,
    state => state.viewport.pending = true,
    state => state.viewport.generation = "2",
    state => state.canvasSize.width++,
    state => state.geometry.columns++,
    state => state.connected = false,
    state => state.readOnly = true
  ]) {
    const changed = snapshot();
    change(changed);
    assert.equal(sameSelectionUIState(previous, changed), false);
  }
});

test("Selection rectangles use overlay-local CSS pixels, not logical or device pixels", () => {
  const state = snapshot();
  assert.deepEqual(selectionRectangles(state.selection.ranges, state.geometry, state.canvasSize),
    [{ left: 45, top: 60, width: 75, height: 30 }]);
  assert.deepEqual(selectionRectangles([], state.geometry, state.canvasSize), []);
});

test("Updates coalesce, preserve the default button, and omit unchanged presentations", async () => {
  const element = new EventTarget();
  const button = {};
  const controller = new AbortController();
  let state = snapshot();
  const events = [];
  const ui = new SelectionUI({
    element, overlay: {}, button, signal: controller.signal, getState: () => state,
    runAction: () => {}, onSelectionUI: event => { events.push(event); }, reportError: () => {}
  });
  ui.refresh();
  ui.refresh();
  state.canvasSize.width = 400;
  await Promise.resolve();
  assert.equal(events.length, 1);
  assert.equal(events[0].detail.canvasSize.width, 400);
  assert.equal(button.hidden, false);
  assert.equal(button.disabled, false);
  assert.equal(button.textContent, "Copy");
  state = { ...state, selection: { ...state.selection, revision: 2 } };
  ui.refresh();
  await Promise.resolve();
  assert.equal(events.length, 1);
  state.selection.copying = true;
  ui.refresh();
  await Promise.resolve();
  assert.equal(events.length, 2);
  assert.equal(button.disabled, true);
  assert.equal(button.textContent, "Copying\u2026");
  controller.abort();
});

test("Canceling the event replaces only default selection controls and exposes stable action/lifetime handles", async () => {
  const element = new EventTarget();
  const overlay = {};
  const button = {};
  const controller = new AbortController();
  let last;
  let replace = true;
  const calls = [];
  const ui = new SelectionUI({
    element, overlay, button, signal: controller.signal, getState: snapshot,
    runAction: (...args) => { calls.push(args); },
    onSelectionUI: event => { last = event; if (replace) event.preventDefault(); },
    reportError: () => {}
  });
  ui.refresh();
  await Promise.resolve();
  assert.equal(last.target, element);
  assert.equal(last.type, "selectionui");
  assert.equal(last.detail.overlay, overlay);
  assert.equal(last.detail.signal, controller.signal);
  assert.equal(button.hidden, true);
  assert.throws(() => { last.detail.selection.ranges[0].row = 9; }, TypeError);
  assert.deepEqual(last.detail.rects, [{ left: 45, top: 60, width: 75, height: 30 }]);
  last.detail.runAction("copySelection", { clear: true });
  assert.deepEqual(calls, [["copySelection", { clear: true }]]);
  replace = false;
  ui.refresh(true);
  await Promise.resolve();
  assert.equal(button.hidden, false);
  assert.equal(last.detail.overlay, overlay);
  controller.abort();
});

test("Disposal aborts host cleanup and cancels queued notifications", async () => {
  const controller = new AbortController();
  let updates = 0, cleanup = 0;
  const ui = new SelectionUI({
    element: new EventTarget(), overlay: {}, button: {}, signal: controller.signal, getState: snapshot,
    runAction: () => {}, reportError: () => {},
    onSelectionUI: event => {
      updates++;
      event.detail.signal.addEventListener("abort", () => cleanup++, { once: true });
    }
  });
  ui.refresh();
  await Promise.resolve();
  ui.refresh(true);
  controller.abort();
  await Promise.resolve();
  assert.equal(updates, 1);
  assert.equal(cleanup, 1);
});

test("Host UI errors are surfaced without silently showing a successful default UI", async () => {
  const errors = [];
  const button = {};
  const ui = new SelectionUI({
    element: new EventTarget(), overlay: {}, button, signal: new AbortController().signal,
    getState: snapshot, runAction: () => {},
    onSelectionUI: () => { throw new Error("UI failed"); },
    reportError: error => { if (error) errors.push(error.message); }
  });
  ui.refresh();
  await Promise.resolve();
  assert.equal(button.hidden, true);
  assert.deepEqual(errors, ["UI failed"]);
  assert.throws(() => new WebTerminal({ onSelectionUI: async () => {} }), /synchronous/);
  assert.throws(() => new WebTerminal({ onSelectionUI: false }), /synchronous/);
});

test("Default controls reflect unavailable, pending, invalidated, disconnected and offscreen read-only selections", async () => {
  const button = {};
  let state = snapshot();
  state.readOnly = true;
  state.selection.ranges = [];
  const ui = new SelectionUI({
    element: new EventTarget(), overlay: {}, button, signal: new AbortController().signal,
    getState: () => state, runAction: () => {}, reportError: () => {}
  });
  for (const [status, connected, hidden, disabled] of [
    ["valid", true, false, false],
    ["valid", false, false, true],
    ["pending", true, false, true],
    ["invalidated", true, false, true],
    ["none", true, true, true],
    ["unavailable", true, true, true]
  ]) {
    state.selection.status = status;
    state.connected = connected;
    ui.refresh();
    await Promise.resolve();
    assert.equal(button.hidden, hidden, status);
    assert.equal(button.disabled, disabled, status);
  }
});

test("External DOM listeners can replace defaults and unregister without changing overlay ownership", async () => {
  const element = new EventTarget();
  const button = {};
  const overlay = {};
  const ui = new SelectionUI({
    element, overlay, button, signal: new AbortController().signal,
    getState: snapshot, runAction: () => {}, reportError: () => {}
  });
  ui.refresh();
  await Promise.resolve();
  assert.equal(button.hidden, false);
  const listener = event => {
    assert.equal(event.detail.overlay, overlay);
    event.preventDefault();
  };
  element.addEventListener("selectionui", listener);
  ui.refresh(true);
  await Promise.resolve();
  assert.equal(button.hidden, true);
  element.removeEventListener("selectionui", listener);
  ui.refresh(true);
  await Promise.resolve();
  assert.equal(button.hidden, false);
});

test("Rejected asynchronous handlers report errors without updating recovered or disposed UI", async () => {
  for (const phase of ["current", "recovered", "disposed"]) {
    const errors = [];
    const deferred = Promise.withResolvers();
    const controller = new AbortController();
    let fail = true;
    const ui = new SelectionUI({
      element: new EventTarget(), overlay: {}, button: {}, signal: controller.signal,
      getState: snapshot, runAction: () => {},
      onSelectionUI: () => fail ? deferred.promise : undefined,
      reportError: error => { if (error) errors.push(error); }
    });
    ui.refresh();
    await Promise.resolve();
    assert.equal(errors.length, 1);
    assert.match(errors[0].message, /synchronously/);
    if (phase === "recovered") {
      fail = false;
      ui.refresh(true);
      await Promise.resolve();
    } else if (phase === "disposed") {
      controller.abort();
    }
    deferred.reject(new Error("late rejection"));
    await Promise.resolve();
    assert.equal(errors.length, phase === "current" ? 2 : 1, phase);
    controller.abort();
  }
});
