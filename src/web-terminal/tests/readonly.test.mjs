import assert from "node:assert/strict";
import { test } from "node:test";
import { setImmediate as nextTurn } from "node:timers/promises";
import { WebTerminal, TerminalAction, InputRoute } from "../dist/index.js";
import { browser, Element, mounting, present } from "./fixtures/browser.mjs";

function history(overrides = {}) {
  return {
    generation: "1", buffer: "main", totalRows: 2, liveTop: 1, top: 1, following: true,
    rowIds: ["2"], requestId: 0,
    selection: { requestId: 0, status: "none", mode: "character", ranges: [], text: null },
    copy: null, ...overrides
  };
}

function emit(target, type, data = {}) {
  const event = new Event(type, { cancelable: true });
  Object.assign(event, {
    pointerId: 1, pointerType: "mouse", button: 0, buttons: 1, clientX: 25, clientY: 25,
    key: "Enter", code: "Enter", repeat: false, isComposing: false, getModifierState: () => false,
    deltaMode: 1, deltaX: 0, deltaY: -1,
    composedPath: () => [target], ...data
  });
  target.dispatchEvent(event);
  return event;
}

test("Runtime readOnly updates public context, keyboard focus and selection UI without reconnecting", async t => {
  const workers = browser(t);
  const states = [];
  const view = await mounting(t, workers, { onSelectionUI: event => { states.push(event.detail.readOnly); } });
  await view.worker.request("open");
  await present(view, { mouseTracking: 1003 });
  const terminal = await view.promise;
  const peer = terminal.peer;
  const input = terminal.element.shadowRoot.querySelector("textarea");
  terminal.focus();
  assert.equal(terminal.readOnly, false);
  assert.equal(input.disabled, false);
  assert.equal(terminal.element.shadowRoot.activeElement, input);
  terminal.setReadOnly(true);
  assert.equal(terminal.readOnly, true);
  assert.equal(terminal.inputContext.readOnly, true);
  assert.equal(terminal.inputContext.mouseCaptured, false);
  assert.equal(input.disabled, true);
  assert.equal(document.activeElement, terminal.element);
  assert.equal(terminal.element.shadowRoot.activeElement, null);
  await nextTurn();
  const notices = states.length;
  terminal.setReadOnly(true);
  assert.equal(states.length, notices, "idempotent writes do not refresh UI");
  terminal.setReadOnly(false);
  await nextTurn();
  assert.equal(input.disabled, false);
  assert.equal(terminal.element.shadowRoot.activeElement, input);
  assert.equal(terminal.inputContext.mouseCaptured, true);
  assert.deepEqual(states.slice(-2), [true, false]);
  assert.deepEqual(terminal.peer, peer);
  assert.equal(terminal.connected, true);
  assert.equal(workers.length, 1);
  assert.throws(() => terminal.setReadOnly("true"), TypeError);
  assert.throws(() => { terminal.readOnly = true; }, TypeError);
  terminal.dispose();
  assert.throws(() => terminal.setReadOnly(true), /disposed/);
});

test("Initial readOnly gates the first frame and does not change by mutating mount options", async t => {
  const workers = browser(t);
  const controller = new AbortController();
  t.after(() => controller.abort());
  const options = { url: "/ws", renderer: "webgl2", readOnly: true, signal: controller.signal };
  const promise = WebTerminal.mount(new Element(), options);
  await nextTurn();
  const worker = workers.at(-1);
  await worker.request("flush");
  await worker.request("open");
  const view = { worker };
  await present(view, {});
  const terminal = await promise;
  worker.disposeView = () => terminal.dispose();
  options.readOnly = false;
  assert.equal(terminal.readOnly, true);
  assert.throws(() => terminal.paste("blocked"), /does not accept input/);
  terminal.setReadOnly(false);
  terminal.paste("allowed");
  await worker.request("flush");
  assert.deepEqual(worker.commands.filter(command => command.type === "paste"), [{ type: "paste", text: "allowed" }]);
  await assert.rejects(WebTerminal.mount(new Element(), { ...options, readOnly: "true" }), /must be a boolean/);
});

for (const peer of [
  { id: "viewer", primaryId: "viewer", isPrimary: true },
  { id: "viewer", primaryId: "other", isPrimary: false }
]) {
  test(`Read-only ${peer.isPrimary ? "primary" : "secondary"} rejects direct input and takeover, preserving output and other views`, async t => {
    let clipboardReads = 0;
    const workers = browser(t, { navigator: { clipboard: { async readText() { clipboardReads++; return "clipboard"; } } } });
    const view = await mounting(t, workers, {
      actions: { send: context => context.terminal.paste("custom") }
    });
    const other = await mounting(t, workers);
    for (const item of [view, other]) {
      await item.worker.request("open");
      await present(item, { peer });
      await item.promise;
    }
    const terminal = view.handle;
    terminal.setReadOnly(true);
    assert.throws(() => terminal.paste("blocked"), /does not accept input/);
    await assert.rejects(terminal.pasteClipboard(), /does not accept input/);
    await assert.rejects(terminal.runAction(TerminalAction.PasteClipboard), /does not accept input/);
    await assert.rejects(terminal.runAction("send"), /does not accept input/);
    await assert.rejects(terminal.runAction(context => context.terminal.paste("callback")), /does not accept input/);
    assert.equal(await terminal.runAction(TerminalAction.CopyOrPaste), undefined);
    assert.equal(clipboardReads, 0);
    assert.throws(() => terminal.resize(80, 24), /does not accept input/);
    assert.throws(() => terminal.requestPrimary(), /does not accept input/);
    assert.throws(() => terminal.setSizing({ mode: "fixed", columns: 80, rows: 24 }), /does not accept input/);
    other.handle.paste("other viewer");
    await other.worker.request("flush");
    terminal.resync();
    await present(view, { revision: 2, full: true, peer, title: "output continues" });
    assert.equal(terminal.title, "output continues");
    assert.deepEqual(terminal.peer, peer);
    assert.equal(terminal.connected, true);
    assert.equal(terminal.readOnly, true);
    assert.equal(view.worker.commands.some(command =>
      ["input", "key", "mouse", "paste", "resize", "requestPrimary"].includes(command.type)), false);
    assert.ok(view.worker.commands.some(command => command.type === "ack" && command.revision === 2));
    assert.ok(other.worker.commands.some(command => command.type === "paste" && command.text === "other viewer"));
    terminal.setReadOnly(false);
    terminal.paste("resumed");
    await view.worker.request("flush");
    assert.ok(view.worker.commands.some(command => command.type === "paste" && command.text === "resumed"));
  });
}

test("Keyboard, text, paste and pending composition cannot cross a read-only transition", async t => {
  const workers = browser(t, { InputEvent: class extends Event {} });
  t.mock.timers.enable({ apis: ["setTimeout"] });
  const view = await mounting(t, workers);
  await view.worker.request("open");
  await present(view, {});
  const terminal = await view.promise;
  const input = terminal.element.shadowRoot.querySelector("textarea");
  emit(terminal.element, "keydown");
  emit(input, "compositionstart");
  emit(input, "compositionend", { data: "stale composition" });
  terminal.setReadOnly(true);
  emit(terminal.element, "keydown");
  input.value = "blocked text";
  emit(input, "input");
  emit(input, "paste", { clipboardData: { getData: () => "blocked paste" } });
  terminal.setReadOnly(false);
  t.mock.timers.tick(1);
  emit(terminal.element, "keydown", { key: "Tab" });
  emit(input, "compositionstart");
  emit(input, "compositionend", { data: "new composition" });
  t.mock.timers.tick(1);
  await view.worker.request("flush");
  assert.deepEqual(view.worker.commands.filter(command => ["input", "key", "paste"].includes(command.type)), [
    { type: "key", key: "Enter", ctrl: false, alt: false, shift: false },
    { type: "key", key: "Tab", ctrl: false, alt: false, shift: false },
    { type: "input", text: "new composition" }
  ]);
});

test("Revocation invalidates an asynchronous clipboard paste even if input is re-enabled before it resolves", async t => {
  const clipboard = Promise.withResolvers();
  const workers = browser(t, { navigator: { clipboard: { readText: () => clipboard.promise } } });
  const view = await mounting(t, workers);
  await view.worker.request("open");
  await present(view, {});
  const terminal = await view.promise;
  const paste = terminal.runAction(TerminalAction.PasteClipboard);
  terminal.setReadOnly(true);
  terminal.setReadOnly(false);
  clipboard.resolve("stale clipboard");
  await assert.rejects(paste, /changed while reading the clipboard/);
  await view.worker.request("flush");
  assert.equal(view.worker.commands.some(command => command.type === "paste"), false);
});

test("Read-only cancels application pointer capture and queued movement without delayed reports after re-enable", async t => {
  const animations = new Map();
  let nextAnimation = 0;
  const workers = browser(t, {
    requestAnimationFrame: callback => { animations.set(++nextAnimation, callback); return nextAnimation; },
    cancelAnimationFrame: id => animations.delete(id)
  });
  const view = await mounting(t, workers);
  await view.worker.request("open");
  await present(view, { mouseTracking: 1003, history: history() });
  const terminal = await view.promise;
  const canvas = terminal.element.shadowRoot.querySelector("canvas");
  emit(canvas, "pointerdown");
  emit(canvas, "pointermove", { clientX: 50 });
  assert.equal(canvas.hasPointerCapture(1), true);
  assert.equal(animations.size, 1);
  terminal.setReadOnly(true);
  assert.equal(canvas.hasPointerCapture(1), false);
  assert.equal(animations.size, 0);
  terminal.setReadOnly(false);
  emit(canvas, "pointerup", { buttons: 0 });
  await view.worker.request("flush");
  assert.deepEqual(view.worker.commands.filter(command => command.type === "mouse").map(command => command.action), ["down"]);
  emit(canvas, "pointerdown");
  emit(canvas, "pointerup", { buttons: 0 });
  await view.worker.request("flush");
  assert.deepEqual(view.worker.commands.filter(command => command.type === "mouse").map(command => command.action), ["down", "down", "up"]);
});

test("Read-only pointer and wheel stay local even when the input interceptor requests application routing", async t => {
  const workers = browser(t);
  const view = await mounting(t, workers, {
    readOnly: true, onInput: () => InputRoute.Application
  });
  await view.worker.request("open");
  await present(view, { mouseTracking: 1003, history: history() });
  const terminal = await view.promise;
  const canvas = terminal.element.shadowRoot.querySelector("canvas");
  emit(canvas, "pointerdown");
  emit(canvas, "pointerup", { buttons: 0 });
  emit(canvas, "wheel");
  await view.worker.request("flush");
  assert.ok(view.worker.commands.some(command => command.type === "selection" && command.action === "start"));
  assert.ok(view.worker.commands.some(command => command.type === "viewport"));
  assert.equal(view.worker.commands.some(command => command.type === "mouse"), false);
});

test("Read-only preserves copy, selection, history, resync and output acknowledgement", async t => {
  const clipboard = [];
  const workers = browser(t, {
    ClipboardItem: class { constructor(parts) { this.parts = parts; } },
    navigator: { clipboard: { async write(items) {
      clipboard.push(await (await items[0].parts["text/plain"]).text());
    } } }
  });
  const selected = { requestId: 0, status: "valid", mode: "character",
    ranges: [{ row: 0, startColumn: 0, endColumn: 1 }], text: "A" };
  const view = await mounting(t, workers);
  await view.worker.request("open");
  await present(view, { history: history({ selection: selected }) });
  const terminal = await view.promise;
  const copying = terminal.copySelection();
  terminal.setReadOnly(true);
  assert.equal(terminal.selection.copying, true);
  assert.equal(terminal.selection.text, "A");
  await view.worker.request("flush");
  const copy = view.worker.commands.find(command => command.type === "copy");
  await present(view, { revision: 2, history: history({
    selection: selected, copy: { requestId: copy.requestId, status: "valid", text: "A" }
  }) });
  assert.equal(await copying, "A");
  assert.deepEqual(clipboard, ["A"]);
  terminal.clearSelection();
  terminal.scrollLines(-1);
  terminal.scrollToLive();
  terminal.resync();
  await view.worker.request("flush");
  assert.ok(view.worker.commands.some(command => command.type === "selection"));
  assert.equal(view.worker.commands.filter(command => command.type === "viewport").length, 2);
  assert.ok(view.worker.commands.some(command => command.type === "ack" && command.revision === 2));
  assert.ok(view.worker.commands.some(command => command.type === "resync"));
});

test("Automatic resizing is suppressed initially and cancelled on revocation, without changing primary role or sizing", async t => {
  const observers = [];
  const workers = browser(t, {
    ResizeObserver: class {
      constructor(callback) { observers.push(callback); }
      observe() {}
      disconnect() {}
    }
  });
  t.mock.timers.enable({ apis: ["setTimeout"] });
  const view = await mounting(t, workers, { readOnly: true });
  observers[0]([{ contentRect: { width: 1000, height: 600 } }]);
  await view.worker.request("open");
  await present(view, { columns: 80, rows: 24 });
  const terminal = await view.promise;
  const sizing = terminal.sizing;
  t.mock.timers.tick(50);
  await view.worker.request("flush");
  assert.equal(view.worker.commands.some(command => command.type === "resize"), false);
  terminal.setReadOnly(false);
  terminal.setReadOnly(true);
  t.mock.timers.tick(50);
  await view.worker.request("flush");
  assert.equal(view.worker.commands.some(command => command.type === "resize"), false);
  terminal.setReadOnly(false);
  t.mock.timers.tick(50);
  await view.worker.request("flush");
  assert.deepEqual(view.worker.commands.filter(command => command.type === "resize"), [
    { type: "resize", columns: 100, rows: 30 }
  ]);
  assert.equal(terminal.peer.isPrimary, true);
  assert.deepEqual(terminal.sizing, sizing);
  assert.equal(workers.length, 1);
});
