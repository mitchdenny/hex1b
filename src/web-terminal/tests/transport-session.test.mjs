import assert from "node:assert/strict";
import { test } from "node:test";
import { setImmediate as nextTurn } from "node:timers/promises";
import { TransportSession } from "../.build/transport-session.js";

function session(t, connect, onFrame = async () => {}) {
  const errors = [];
  const ready = Promise.withResolvers();
  const value = new TransportSession({
    onReady: ready.resolve, onFrame, onError: error => errors.push(error), onClose() {}
  });
  t.after(() => value.dispose());
  value.start({ connect });
  return { value, errors, ready: ready.promise };
}

test("Transport close waits for the last accepted frame, without aborting its delivery", async t => {
  const release = Promise.withResolvers();
  const closed = Promise.withResolvers();
  const order = [];
  let context;
  const value = new TransportSession({
    onReady() {},
    async onFrame(buffer) {
      await release.promise;
      assert.equal(context.signal.aborted, false);
      assert.deepEqual([...new Uint8Array(buffer)], [1, 2, 3, 255]);
      order.push("frame");
    },
    onClose(details) { order.push("close"); closed.resolve(details); },
    onError(error) { closed.reject(error); }
  });
  t.after(() => value.dispose());
  value.start({ connect(ctx) { context = ctx; return { send() {}, dispose() {} }; } });
  await nextTurn();
  const received = context.onFrame(new Uint8Array([1, 2, 3, 255]));
  context.onClose({ reason: "producer exited" });
  assert.deepEqual(order, []);
  release.resolve();
  await received;
  assert.deepEqual(await closed.promise, { reason: "producer exited" });
  assert.deepEqual(order, ["frame", "close"]);
});

test("Controls wait for async completion, preserve order, and propagate send failure", async t => {
  const first = Promise.withResolvers();
  const controls = [];
  const s = session(t, () => ({
    send(control) {
      controls.push(control);
      if (control === "one") return first.promise;
      throw new Error("send failed");
    },
    dispose() {}
  }));
  await s.ready;
  const one = s.value.send("one");
  const two = s.value.send("two");
  const three = s.value.send("three");
  const rejected = Promise.all([assert.rejects(two, /send failed/), assert.rejects(three, /send failed/)]);
  await nextTurn();
  assert.deepEqual(controls, ["one"]);
  first.resolve();
  await one;
  await rejected;
  assert.deepEqual(controls, ["one", "two"]);
  assert.equal(s.errors[0].message, "send failed");
});

test("Closing cancels outbound controls without failing the accepted final frame", async t => {
  const releaseFrame = Promise.withResolvers();
  const activeSend = Promise.withResolvers();
  const closed = Promise.withResolvers();
  const controls = [];
  const errors = [];
  let context;
  const value = new TransportSession({
    onReady() {},
    async onFrame() {
      await releaseFrame.promise;
      assert.equal(context.signal.aborted, false);
    },
    onClose: closed.resolve,
    onError: error => { errors.push(error); closed.reject(error); }
  });
  t.after(() => { releaseFrame.resolve(); value.dispose(); });
  value.start({ connect(ctx) {
    context = ctx;
    return { send(control) { controls.push(control); return activeSend.promise; }, dispose() {} };
  } });
  await nextTurn();
  const active = assert.rejects(value.send("active"), { name: "AbortError" });
  const queued = assert.rejects(value.send("queued"), { name: "AbortError" });
  await nextTurn();
  const received = context.onFrame(new ArrayBuffer(1));
  context.onClose({ reason: "producer exited" });
  const late = assert.rejects(value.send("late"), { name: "AbortError" });
  activeSend.reject(new Error("transport closed during send"));
  await late;
  await nextTurn();
  assert.deepEqual(errors, []);
  releaseFrame.resolve();
  await received;
  assert.deepEqual(await closed.promise, { reason: "producer exited" });
  await Promise.all([active, queued]);
  assert.deepEqual(controls, ["active"]);
});

test("Control queue is bounded while a send stalls", async t => {
  const s = session(t, () => ({ send: () => new Promise(() => {}), dispose() {} }));
  await s.ready;
  const pending = Array.from({ length: 257 }, () => s.value.send("x").catch(error => error));
  const errors = await Promise.all(pending);
  assert.ok(errors.every(error => error instanceof Error));
  assert.match(s.errors[0].message, /bounded capacity/);
});

test("Control queue permits exactly 1 MiB including the active send, then fails", async t => {
  const s = session(t, () => ({ send: () => new Promise(() => {}), dispose() {} }));
  await s.ready;
  const pending = Array.from({ length: 16 }, () => s.value.send("x".repeat(64 * 1024)).catch(error => error));
  await nextTurn();
  assert.deepEqual(s.errors, []);
  pending.push(s.value.send("x").catch(error => error));
  assert.ok((await Promise.all(pending)).every(error => /bounded capacity/.test(error.message)));
  assert.equal(s.errors.length, 1);
});

for (const data of [new Uint16Array(2), new ArrayBuffer(0), new Uint8Array(new SharedArrayBuffer(4)), "text"]) {
  test(`Invalid frame ownership/shape fails the connection: ${data.constructor.name}`, async t => {
    let rejected;
    const s = session(t, context => {
      rejected = assert.rejects(context.onFrame(data), /frame|ArrayBuffer/);
      return { send() {}, dispose() {} };
    });
    await rejected;
    assert.equal(s.errors.length, 1);
  });
}

test("Typed-array delivery copies only its slice before readiness and never detaches the host buffer", async t => {
  const connection = Promise.withResolvers();
  let delivered;
  const bytes = new Uint8Array([9, 1, 2, 9]);
  const received = [];
  const s = session(t, context => {
    delivered = context.onFrame(bytes.subarray(1, 3));
    return connection.promise;
  }, async buffer => received.push([...new Uint8Array(buffer)]));
  bytes.fill(7);
  connection.resolve({ send() {}, dispose() {} });
  await delivered;
  assert.deepEqual(received, [[1, 2]]);
  assert.equal(bytes.byteLength, 4);
  assert.deepEqual(s.errors, []);
});

test("Concurrent early frames fail instead of creating an unbounded queue", async t => {
  let first;
  let second;
  const s = session(t, context => {
    first = context.onFrame(new ArrayBuffer(1));
    second = context.onFrame(new ArrayBuffer(1));
    return { send() {}, dispose() {} };
  });
  await Promise.all([assert.rejects(first), assert.rejects(second, /Concurrent/)]);
  assert.equal(s.errors.length, 1);
});

test("Async connect rejection is surfaced once, with cancellation", async t => {
  let signal;
  const s = session(t, context => {
    signal = context.signal;
    return Promise.reject(new Error("attach failed"));
  });
  await nextTurn();
  assert.equal(signal.aborted, true);
  assert.deepEqual(s.errors.map(error => error.message), ["attach failed"]);
});
