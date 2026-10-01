import assert from "node:assert/strict";
import { test } from "node:test";
import { setImmediate as nextTurn } from "node:timers/promises";
import { WebTerminal } from "../dist/index.js";
import { browser, Element, frame } from "./fixtures/browser.mjs";

async function mountRecording(workers, options = {}) {
  const container = new Element();
  const promise = WebTerminal.mountRecording(container, {
    url: "/recordings/demo.hwt.json", renderer: "webgl2", ...options,
  });
  promise.catch(() => {}); // Assertions observe rejection after driving the worker.
  await nextTurn();
  const worker = workers.at(-1);
  await worker.request("flush");
  return { container, worker, promise };
}

function recording(metadata = {}) {
  return JSON.stringify({
    format: "hex1b-hwt-recording", version: 1, durationMs: 10000,
    frames: [{ timeMs: 0, data: Buffer.from(frame(metadata)).toString("base64") }],
  });
}

test("Mounted recording fetches over HTTP, waits for GPU presentation, and never creates a socket", async t => {
  const workers = browser(t);
  const states = [];
  const view = await mountRecording(workers, { onPlaybackChange: state => states.push(state) });
  await view.worker.request("hold");
  await view.worker.request("recording", { details: { body: recording() } });
  await view.worker.request("draw");
  assert.equal(states.length, 0);
  await view.worker.request("release");
  const player = await view.promise;
  view.worker.disposeView = () => player.dispose();
  assert.deepEqual(view.worker.requests, ["https://example.test/recordings/demo.hwt.json"]);
  assert.deepEqual(view.worker.sockets, []);
  assert.deepEqual(view.worker.commands, []);
  assert.equal(player.screenText, "A");
  assert.equal(player.playback.status, "paused");
  assert.equal(player.playback.frameIndex, 0);
  assert.equal(player.stats.connected, false);
  assert.equal("paste" in player, false);
  player.playback.frameIndex = 999;
  assert.equal(player.playback.frameIndex, 0);
  player.restart();
  await view.worker.request("flush");
  await view.worker.request("draw");
  assert.equal(states.length, 2);
  assert.equal(player.playback.frameIndex, 0);
  player.restart();
  player.play();
  player.pause();
  await view.worker.request("flush");
  await view.worker.request("draw");
  await view.worker.request("flush");
  assert.ok(states.some(state => state.status === "playing"), "Controls queue behind restart presentation");
  assert.equal(player.playback.status, "paused");
  assert.deepEqual(view.worker.sockets, []);
  assert.deepEqual(view.worker.errors, []);
  player.dispose();
  assert.equal(view.container.children.length, 0);
  assert.throws(() => player.play(), /disposed/);
});

test("A failed recording fetch rejects mount, reports the error and removes the view", async t => {
  const workers = browser(t);
  const errors = [];
  const view = await mountRecording(workers, { onStatus: (message, level) => {
    if (level === "error") errors.push(message);
  } });
  await view.worker.request("recording", { details: { body: "missing", status: 404 } });
  await assert.rejects(view.promise, /404/);
  assert.ok(errors.some(message => message.includes("404")));
  assert.equal(view.container.children.length, 0);
  assert.deepEqual(view.worker.sockets, []);
});

test("Recording mounting rejects socket URLs rather than silently connecting", async t => {
  browser(t);
  await assert.rejects(WebTerminal.mountRecording(new Element(), { url: "wss://example.test/ws" }), /HTTP/);
  await assert.rejects(WebTerminal.mountRecording(new Element(), {}), /HTTP/);
});

test("Recording renders indexed colors without negotiating a live transport", async t => {
  const workers = browser(t);
  const view = await mountRecording(workers);
  await view.worker.request("recording", { details: { body: recording({
    colorEncodings: ["indexed-v1"], colorEncoding: "indexed-v1",
    cells: [{ index: 0, text: "A", foreground: 0x01000002, background: 0x03000000, underlineColor: 0x04000000 }],
  }) } });
  await view.worker.request("draw");
  const player = await view.promise;
  view.worker.disposeView = () => player.dispose();
  assert.equal(await view.worker.request("renderedForeground"), 0xff82ae8d);
  assert.deepEqual(view.worker.commands, []);
  assert.deepEqual(view.worker.sockets, []);
  assert.equal(view.worker.outputs.some(message => message.type === "transportConnect"), false);
  assert.deepEqual(view.worker.errors, []);
});

test("Recording presents frames without waiting for producer-backed marker pages", async t => {
  const workers = browser(t);
  const view = await mountRecording(workers);
  await view.worker.request("recording", { details: { body: recording({
    history: {
      generation: "1", buffer: "main", totalRows: 5, liveTop: 4, top: 4, following: true,
      requestId: 0, rowIds: ["5"],
      selection: { requestId: 0, status: "none", mode: "character", ranges: [], text: null },
      copy: null, markers: [{ id: "command:1", source: "command", buffer: "main", row: 0, column: 0,
        phase: "executing", exitCode: null }],
      markerResult: null, markerPage: { revision: "1", offset: 0, total: 2 },
    },
  }) } });
  await view.worker.request("draw");
  assert.ok(view.worker.outputs.some(message => message.type === "playback"));
  const player = await view.promise;
  view.worker.disposeView = () => player.dispose();
  assert.equal(player.screenText, "A");
  assert.equal(player.playback.frameIndex, 0);
  const geometry = view.worker.outputs.find(message => message.type === "geometry");
  assert.equal(geometry.history, null);
  assert.deepEqual(view.worker.commands, []);
  assert.deepEqual(view.worker.errors, []);
});
