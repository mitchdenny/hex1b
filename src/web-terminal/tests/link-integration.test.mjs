import assert from "node:assert/strict";
import { test } from "node:test";
import { setImmediate as nextTurn } from "node:timers/promises";
import { linkAction } from "../.build/index.js";
import { extractLinkText } from "../.build/link-text.js";
import { browser, mounting, present } from "./fixtures/browser.mjs";

function screen(text, columns = 60, rows = 3, start = columns + 1) {
  const cells = Array.from({ length: columns * rows }, (_, index) => ({ index, text: " ", width: 1 }));
  for (const [offset, character] of [...text].entries()) cells[start + offset].text = character;
  return { columns, rows, cells, cursor: { visible: false, x: 0, y: 0, shape: 1 } };
}

test("Only the last cell's SoftWrap flag joins rows, matching the producer's text model", () => {
  const state = screen("a", 10, 3);
  state.cells[11].attributes = 1024;
  const snapshot = { ...state, revision: 1, hyperlinks: [] };
  assert.equal(extractLinkText(snapshot, "logicalLine").length, 3);
  state.cells[19].attributes = 1024;
  assert.equal(extractLinkText(snapshot, "logicalLine").length, 2);
});

function pointer(view, type, x = 2, y = 1, extra = {}) {
  const canvas = view.handle.element.shadowRoot.querySelector("canvas");
  const event = new Event(type, { cancelable: true });
  Object.assign(event, {
    pointerId: 1, pointerType: "mouse", button: 0, buttons: type === "pointerup" || type === "pointermove" ? 0 : 1,
    clientX: (x + .5) * 100 / view.handle.geometry.columns,
    clientY: (y + .5) * 100 / view.handle.geometry.rows,
    ctrlKey: true, metaKey: false, altKey: false, shiftKey: false,
    ...extra
  });
  canvas.dispatchEvent(event);
  return canvas;
}

async function linksPresented(view, workers, target, x = 2, y = 1) {
  for (let attempt = 0; attempt < 40; attempt++) {
    for (const worker of [...workers]) if (!worker.terminated) await worker.request("flush");
    await view.worker.request("draw");
    const canvas = pointer(view, "pointermove", x, y);
    if (canvas.title.startsWith(`${target}\n`)) return;
  }
  assert.fail(`Link was not presented: ${target}; worker errors: ${workers.flatMap(w => w.errors).join(", ")}`);
}

test("Detection is opt-in and detected links dispatch immutable consumer actions only after release", async t => {
  const workers = browser(t);
  const activations = [];
  const view = await mounting(t, workers, {
    linkDetectionWorkerUrl: "/custom/link-detection-worker.js",
    actions: { inspect: linkAction((context, link, input) => activations.push({ context, link, input })) }
  });
  await view.worker.request("open");
  await present(view, screen("https://example.test/docs"));
  await view.promise;
  assert.equal(pointer(view, "pointermove").title, "");
  assert.equal(workers.length, 1);
  view.handle.setLinks({ detection: { rules: [{ id: "web", builtin: "url", action: "inspect" }] } });
  await linksPresented(view, workers, "https://example.test/docs");
  assert.equal(workers[1].url, "https://example.test/custom/link-detection-worker.js");
  pointer(view, "pointerdown");
  assert.equal(activations.length, 0);
  pointer(view, "pointerup");
  assert.equal(activations.length, 1);
  assert.equal(activations[0].link.target, "https://example.test/docs");
  assert.equal(activations[0].link.source, "detected");
  assert.equal(activations[0].link.ruleId, "web");
  assert.equal(activations[0].link.revision, 1);
  assert.equal(activations[0].input.type, "pointer");
  assert.ok(Object.isFrozen(activations[0].link));
  assert.ok(Object.isFrozen(activations[0].link.ranges));
  assert.equal(view.worker.commands.filter(command => command.type === "ack").length, 1);
  assert.equal(view.worker.commands.some(command => command.type === "mouse"), false);
});

test("A soft-wrapped URL activates its complete target across visible rows", async t => {
  const workers = browser(t);
  const targets = [];
  const target = "https://example.test/really-long-path";
  const state = screen(target, 40, 4, 70);
  state.cells[79].attributes = 1024;
  const view = await mounting(t, workers, {
    actions: { inspect: linkAction((_, link) => targets.push(link.target)) },
    links: { detection: { rules: [{ id: "web", builtin: "url", action: "inspect" }] } }
  });
  await view.worker.request("open");
  await present(view, state);
  await view.promise;
  await linksPresented(view, workers, target, 2, 2);
  pointer(view, "pointerdown", 2, 2);
  pointer(view, "pointerup", 2, 2);
  assert.deepEqual(targets, [target]);
});

test("Hover mode decorates all wrapped spans and clears them on departure without rescanning", async t => {
  const workers = browser(t);
  const target = "https://example.test/really-long-path";
  const state = screen(target, 40, 4, 70);
  state.cells[79].attributes = 1024;
  let resolutions = 0;
  const view = await mounting(t, workers, {
    actions: { inspect: linkAction(() => {}) },
    links: { detection: { decoration: "hover", underlineStyle: "dashed", rules: [{
      id: "web", builtin: "url", action: "inspect",
      resolve: match => { resolutions++; return { target: match.text }; }
    }] } }
  });
  await view.worker.request("open");
  await present(view, state);
  await view.promise;
  await linksPresented(view, workers, target, 2, 2);
  const decorations = () => view.worker.inputs.filter(input => input.type === "linkDecorations").at(-1);
  assert.equal(decorations().ranges.length, 2);
  assert.equal(decorations().underlineStyle, "dashed");
  const before = resolutions;
  pointer(view, "pointermove", 35, 3);
  assert.deepEqual(decorations().ranges, []);
  assert.equal(resolutions, before);
  pointer(view, "pointermove", 31, 1);
  assert.equal(decorations().ranges.length, 2);
  assert.equal(resolutions, before);
  view.handle.setLinks({ detection: { underlineStyle: "solid",
    rules: [{ id: "web", builtin: "url", action: "inspect" }] } });
  await linksPresented(view, workers, target, 2, 2);
  assert.equal(decorations().underlineStyle, "solid");
});

test("Plain-click and decoration-none detection work in read-only views and can be disabled atomically", async t => {
  const workers = browser(t);
  const targets = [];
  const options = { detection: { activation: "click", decoration: "none",
    rules: [{ id: "paths", builtin: "absolutePath", action: "inspect" }] } };
  const view = await mounting(t, workers, {
    readOnly: true,
    actions: { inspect: linkAction((_, link) => targets.push(link.target)) },
    links: options
  });
  await view.worker.request("open");
  await present(view, { ...screen("/home/remote/file"), mouseTracking: 1003 });
  await view.promise;
  await linksPresented(view, workers, "/home/remote/file");
  pointer(view, "pointerdown", 2, 1, { ctrlKey: false });
  pointer(view, "pointerup", 2, 1, { ctrlKey: false });
  assert.deepEqual(targets, ["/home/remote/file"]);
  assert.throws(() => view.handle.setLinks({ detection: {
    rules: [{ id: "invalid", builtin: "url", action: "notRegistered" }]
  } }), /action/i);
  pointer(view, "pointerdown");
  view.handle.setLinks(false);
  pointer(view, "pointerup");
  assert.equal(targets.length, 1);
  assert.equal(pointer(view, "pointermove").title, "");
  view.handle.setLinks(options);
  await linksPresented(view, workers, "/home/remote/file");
  assert.equal(view.worker.commands.some(command => command.type === "mouse"), false);
});

test("OSC 8 defaults remain allowlisted and custom actions can receive other absolute schemes", async t => {
  const workers = browser(t);
  const opened = [];
  window.open = (...args) => opened.push(args);
  const actions = [];
  const view = await mounting(t, workers, {
    actions: { inspect: linkAction((_, link) => actions.push(link)) }
  });
  await view.worker.request("open");
  const state = screen("label");
  await present(view, { ...state, hyperlinks: [
    { row: 1, startColumn: 1, endColumn: 6, uri: "https://example.test/" }
  ] });
  await view.promise;
  pointer(view, "pointerdown");
  pointer(view, "pointerup");
  assert.deepEqual(opened, [["https://example.test/", "_blank", "noopener,noreferrer"]]);
  await present(view, { ...state, revision: 2, full: true, hyperlinks: [
    { row: 1, startColumn: 1, endColumn: 6, uri: "custom:remote-resource" }
  ] });
  assert.equal(pointer(view, "pointermove").title, "");
  view.handle.setLinks({ osc8: { action: "inspect" } });
  await linksPresented(view, workers, "custom:remote-resource");
  pointer(view, "pointerdown");
  pointer(view, "pointerup");
  assert.equal(actions.length, 1);
  assert.equal(actions[0].target, "custom:remote-resource");
  assert.equal(actions[0].text, "label");
  assert.equal(actions[0].source, "osc8");
  assert.equal(opened.length, 1);
  assert.equal(workers.length, 1, "custom OSC 8 needs no regex worker");
});

test("Blocked and disabled OSC 8 spans reserve text against inferred targets", async t => {
  const workers = browser(t);
  const view = await mounting(t, workers, {
    actions: { inspect: linkAction(() => assert.fail("Reserved cells activated")) },
    links: { osc8: false, detection: { rules: [{ id: "web", builtin: "url", action: "inspect" }] } }
  });
  await view.worker.request("open");
  await present(view, { ...screen("https://example.test/"), hyperlinks: [
    { row: 1, startColumn: 1, endColumn: 22, uri: "javascript:alert(1)" }
  ] });
  await view.promise;
  for (let i = 0; i < 5; i++) {
    for (const worker of [...workers]) if (!worker.terminated) await worker.request("flush");
    await view.worker.request("draw");
  }
  assert.equal(pointer(view, "pointermove").title, "");
});

test("Unchanged frame content rebases links without resolver work and stale gestures are cancelled", async t => {
  const workers = browser(t);
  let resolutions = 0;
  let activations = 0;
  const view = await mounting(t, workers, {
    actions: { inspect: linkAction(() => { activations++; }) },
    links: { detection: { rules: [{
      id: "web", builtin: "url", action: "inspect",
      resolve: match => { resolutions++; return { target: match.text }; }
    }] } }
  });
  await view.worker.request("open");
  await present(view, screen("https://example.test/"));
  await view.promise;
  await linksPresented(view, workers, "https://example.test/");
  const before = resolutions;
  for (let x = 2; x < 12; x++) pointer(view, "pointermove", x);
  assert.equal(resolutions, before);
  pointer(view, "pointerdown");
  await present(view, { columns: 60, rows: 3, revision: 2, title: "metadata only" });
  await linksPresented(view, workers, "https://example.test/");
  pointer(view, "pointerup");
  assert.equal(activations, 0);
  assert.equal(resolutions, before);
  pointer(view, "pointerdown");
  pointer(view, "pointerup");
  assert.equal(activations, 1);
  await nextTurn();
  assert.deepEqual(view.worker.commands.filter(command => command.type === "ack").map(command => command.revision), [1, 2]);
});

test("A pathological regex times out without blocking terminal output or future detection", { timeout: 10000 }, async t => {
  const workers = browser(t);
  const diagnostic = Promise.withResolvers();
  const view = await mounting(t, workers, {
    actions: { inspect: linkAction(() => {}) },
    links: { detection: { rules: [
      { id: "pathological", pattern: /(a+)+$/u, kind: "custom", action: "inspect" }
    ] } },
    onLinkDetectionError: diagnostic.resolve
  });
  await view.worker.request("open");
  await present(view, screen(`${"a".repeat(45)}!`));
  await view.promise;
  await present(view, { columns: 60, rows: 3, revision: 2, title: "output continues" });
  assert.equal(view.handle.title, "output continues");
  assert.equal(view.handle.connected, true);
  const error = await diagnostic.promise;
  assert.equal(error.code, "timeout");
  assert.equal(error.ruleId, "pathological");
  assert.equal(view.handle.connected, true);
  view.handle.setLinks({ detection: { rules: [{ id: "web", builtin: "url", action: "inspect" }] } });
  await present(view, { ...screen("https://example.test/"), revision: 3, full: true });
  await linksPresented(view, workers, "https://example.test/");
});
