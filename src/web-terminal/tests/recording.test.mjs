import assert from "node:assert/strict";
import { test } from "node:test";
import { parseRecording, RecordingPlayer, fetchRecording } from "../.build/recording.js";
import { frame } from "./fixtures/browser.mjs";

function recording(overrides = {}) {
  return {
    format: "hex1b-hwt-recording", version: 1, durationMs: 300,
    frames: [1, 2, 3].map((revision, index) => ({
      timeMs: index * 100, data: Buffer.from(frame({ revision })).toString("base64"),
    })),
    ...overrides,
  };
}

test("Recording validates baseline, ordered deltas, timestamps and same-build container", () => {
  const result = parseRecording(JSON.stringify(recording()));
  assert.equal(result.durationMs, 300);
  assert.equal(result.frames.length, 3);
  assert.deepEqual(result.frames[0].data, frame());
  for (const patch of [
    { version: 2 }, { format: "asciicast" }, { durationMs: -1 }, { durationMs: 600001 },
    { durationMs: 150 }, { frames: [] },
    { frames: [{ timeMs: 10, data: recording().frames[0].data }] },
    { frames: [{ timeMs: 0, data: "broken" }] },
    { frames: [{ timeMs: 0, data: Buffer.from(frame({ revision: 2 })).toString("base64") }] },
  ]) assert.throws(() => parseRecording(JSON.stringify(recording(patch))));
  const brokenChain = recording();
  brokenChain.frames[1].data = Buffer.from(frame({ revision: 2, baseRevision: 99 })).toString("base64");
  assert.throws(() => parseRecording(JSON.stringify(brokenChain)), /delta chain/);
  const backwards = recording();
  backwards.frames[2].timeMs = 50;
  assert.throws(() => parseRecording(JSON.stringify(backwards)), /timestamp/);
});

test("Recording cannot refer to image payloads outside the file", () => {
  const missing = recording({ frames: [{
    timeMs: 0, data: Buffer.from(frame({ retainedImages: ["absent"] })).toString("base64"),
  }] });
  assert.throws(() => parseRecording(JSON.stringify(missing)), /Missing recording image/);
});

test("Recording color encoding changes require a full baseline", () => {
  const source = recording();
  source.frames[1].data = Buffer.from(frame({ revision: 2, colorEncoding: "indexed-v1" })).toString("base64");
  assert.throws(() => parseRecording(JSON.stringify(source)), /delta chain/);
  source.frames[1].data = Buffer.from(frame({ revision: 2, full: true, colorEncoding: "indexed-v1",
    cells: [{ index: 0, text: "A", foreground: 0x02000000, background: 0x03000000, underlineColor: 0x04000000 }],
  })).toString("base64");
  assert.throws(() => parseRecording(JSON.stringify(source)), /delta chain/);
  source.frames[2].data = Buffer.from(frame({ revision: 3, colorEncoding: "indexed-v1" })).toString("base64");
  assert.equal(parseRecording(JSON.stringify(source)).frames.length, 3);
});

test("Recording fetch rejects failed responses and oversized declared bodies", async t => {
  const original = globalThis.fetch;
  t.after(() => { globalThis.fetch = original; });
  globalThis.fetch = async () => new Response("not found", { status: 404 });
  await assert.rejects(fetchRecording(new URL("https://example.test/demo"), new AbortController().signal), /404/);
  globalThis.fetch = async () => new Response("", { headers: { "content-length": "999999999" } });
  await assert.rejects(fetchRecording(new URL("https://example.test/demo"), new AbortController().signal), /64 MiB/);
  globalThis.fetch = async () => new Response(JSON.stringify(recording()));
  assert.equal((await fetchRecording(new URL("https://example.test/demo"), new AbortController().signal)).frames.length, 3);
  await assert.rejects(fetchRecording(new URL("file:///demo"), new AbortController().signal), /HTTP/);
});

test("Recording fetch enforces its limit even without a Content-Length header", async t => {
  const original = globalThis.fetch;
  t.after(() => { globalThis.fetch = original; });
  const chunk = new Uint8Array(1024 * 1024).fill(32);
  let cancelled = false;
  globalThis.fetch = async () => new Response(new ReadableStream({
    pull(controller) { controller.enqueue(chunk); },
    cancel() { cancelled = true; },
  }));
  await assert.rejects(fetchRecording(new URL("https://example.test/demo"), new AbortController().signal), /64 MiB/);
  assert.equal(cancelled, true);
});

function clock() {
  let now = 0;
  let serial = 0;
  const callbacks = new Map();
  return {
    now: () => now,
    schedule(callback, delay) { const id = ++serial; callbacks.set(id, { callback, due: now + delay }); return id; },
    cancel(id) { callbacks.delete(id); },
    async advance(ms) {
      now += ms;
      for (const [id, { callback, due }] of [...callbacks]) {
        if (due <= now) { callbacks.delete(id); callback(); }
      }
      // Settle the nested presentation and timer promises without wall-clock sleeps.
      for (let i = 0; i < 10; i++) await Promise.resolve();
    },
    get pending() { return callbacks.size; },
  };
}

test("Playback starts paused, freezes time on pause, retains final frame and restarts baseline", async () => {
  const time = clock();
  const shown = [];
  const failures = [];
  const states = [];
  const player = new RecordingPlayer(parseRecording(JSON.stringify(recording())),
    async frame => { shown.push(frame.timeMs); }, state => states.push(state), error => failures.push(error), time);
  await player.initialize();
  assert.deepEqual(shown, [0]);
  assert.equal(player.state.status, "paused");
  await time.advance(1000);
  assert.equal(player.state.positionMs, 0);
  player.play();
  await time.advance(100);
  assert.deepEqual(shown, [0, 100]);
  player.pause();
  await time.advance(1000);
  assert.equal(player.state.positionMs, 100);
  assert.deepEqual(shown, [0, 100]);
  player.play();
  await time.advance(100);
  await time.advance(100);
  assert.deepEqual(shown, [0, 100, 200]);
  assert.equal(player.state.status, "ended");
  assert.equal(player.state.positionMs, 300);
  assert.throws(() => player.play(), /Restart/);
  await player.restart();
  assert.deepEqual(shown, [0, 100, 200, 0]);
  assert.equal(player.state.status, "paused");
  assert.equal(player.state.positionMs, 0);
  assert.equal(player.state.frameIndex, 0);
  assert.deepEqual(failures, []);
  player.dispose();
  assert.equal(time.pending, 0);
  assert.throws(() => player.play(), /available/);
});

test("A slow renderer never loses deltas and restart waits for the in-flight presentation", async () => {
  const time = clock();
  const shown = [];
  const gate = Promise.withResolvers();
  const player = new RecordingPlayer(parseRecording(JSON.stringify(recording())),
    async frame => { shown.push(frame.timeMs); if (frame.timeMs === 100) await gate.promise; },
    () => {}, error => { throw error; }, time);
  await player.initialize();
  player.play();
  await time.advance(300);
  await time.advance(300);
  assert.deepEqual(shown, [0, 100]);
  const restarting = player.restart();
  assert.deepEqual(shown, [0, 100]);
  gate.resolve();
  await restarting;
  assert.deepEqual(shown, [0, 100, 0]);
  player.play();
  await time.advance(300);
  await time.advance(0);
  assert.deepEqual(shown, [0, 100, 0, 100, 200]);
  assert.equal(player.state.status, "ended");
  player.dispose();
});

test("Playback reports a rendering failure and cancels subsequent work", async () => {
  const time = clock();
  const failures = [];
  const player = new RecordingPlayer(parseRecording(JSON.stringify(recording())),
    async frame => { if (frame.timeMs > 0) throw new Error("GPU failed"); },
    () => {}, error => failures.push(error.message), time);
  await player.initialize();
  player.play();
  await time.advance(300);
  assert.deepEqual(failures, ["GPU failed"]);
  assert.equal(time.pending, 0);
  assert.equal(player.state.frameIndex, 0);
});

test("Disposal during a pending presentation suppresses further notifications and timers", async () => {
  const time = clock();
  const gate = Promise.withResolvers();
  const states = [];
  const shown = [];
  const player = new RecordingPlayer(parseRecording(JSON.stringify(recording())),
    async frame => { shown.push(frame.timeMs); if (frame.timeMs > 0) await gate.promise; },
    state => states.push(state), error => { throw error; }, time);
  await player.initialize();
  player.play();
  await time.advance(100);
  const notifications = states.length;
  player.dispose();
  gate.resolve();
  await time.advance(1000);
  assert.equal(states.length, notifications);
  assert.deepEqual(shown, [0, 100]);
  assert.equal(time.pending, 0);
});
