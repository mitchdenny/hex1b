import assert from "node:assert/strict";
import { test } from "node:test";
import { setImmediate as nextTurn } from "node:timers/promises";
import { WebTerminal } from "../.build/web-terminal.js";
import { createWebSocketTransport } from "../.build/websocket-transport.js";
import { browser, Element, frame, mounting, present } from "./fixtures/browser.mjs";

for (const options of [{}, { url: "/ws", transport: { connect() {} } },
  { transport: null }, { transport: {} }, { url: "" }]) {
  test(`Invalid live transport options reject before creating a worker: ${JSON.stringify(options)}`, async t => {
    const workers = browser(t);
    await assert.rejects(WebTerminal.mount(new Element(), options), /transport|URL/);
    assert.equal(workers.length, 0);
  });
}

test("URL convenience and explicit WebSocket transport use the same worker configuration", async t => {
  const workers = browser(t);
  for (const options of [{ url: "/ws" }, { url: undefined, transport: createWebSocketTransport("/ws") }]) {
    const notices = [];
    const view = await mounting(t, workers, { ...options, onClose: details => notices.push(details) });
    const init = view.worker.inputs.find(input => input.type === "init");
    assert.deepEqual(init.transport, { type: "websocket", url: "wss://example.test/ws" });
    await view.worker.request("open");
    await present(view, { title: "same transport" });
    const terminal = await view.promise;
    assert.equal(terminal.title, "same transport");
    assert.ok(view.worker.commands.some(command => command.type === "ack"));
    await view.worker.request("disconnect", { details: { code: 4001, reason: "same close", wasClean: true } });
    assert.deepEqual(notices, [{ code: 4001, reason: "same close", wasClean: true }]);
    assert.equal(view.worker.inputs.some(message => message.type.startsWith("transport")), false,
      "WebSocket data never bridges through the host");
    terminal.dispose();
  }
});

async function customView(t, options = {}, connect) {
  const workers = browser(t);
  const attached = Promise.withResolvers();
  const commands = [];
  const waiters = [];
  let context;
  let disposals = 0;
  const connection = {
    send: control => {
      const command = JSON.parse(control);
      commands.push(command);
      for (const waiter of waiters) if (waiter.type === command.type) waiter.resolve(command);
    },
    dispose: () => { disposals++; }
  };
  const transport = { connect(value) {
    context = value;
    attached.resolve();
    return connect ? connect(value, connection) : connection;
  } };
  const view = await mounting(t, workers, { url: undefined, transport, ...options });
  await attached.promise;
  return { ...view, view, commands, context, get disposals() { return disposals; },
    waitControl(type) {
      const command = commands.find(command => command.type === type);
      if (command) return Promise.resolve(command);
      const pending = Promise.withResolvers();
      waiters.push({ type, resolve: pending.resolve });
      return pending.promise;
    }
  };
}

test("Custom live transport bridges controls and frames without a WebSocket; ACK waits for presentation", async t => {
  const live = await customView(t);
  await live.worker.request("hold");
  const buffer = frame({ title: "native host" });
  await live.context.onFrame(buffer);
  assert.equal(buffer.byteLength, 0, "owned ArrayBuffers transfer to the real worker");
  await live.worker.request("draw");
  assert.equal(live.view.settled, false);
  assert.equal(live.commands.some(command => command.type === "ack"), false);
  await live.worker.request("release");
  const terminal = await live.promise;
  await live.waitControl("ack");
  assert.equal(terminal.title, "native host");
  assert.ok(live.commands.some(command => command.type === "ack" && command.revision === 1));
  terminal.paste("hello");
  terminal.resync();
  await live.waitControl("resync");
  assert.deepEqual(live.commands.filter(command => ["paste", "resync"].includes(command.type)),
    [{ type: "paste", text: "hello" }, { type: "resync" }]);
  terminal.dispose();
  assert.equal(live.disposals, 1);
  assert.equal(live.context.signal.aborted, true);
  await assert.rejects(live.context.onFrame(frame()), /disposed|closed|abort/i);
});

test("Custom close reports no fabricated WebSocket status, including before mounting", async t => {
  const notices = [];
  const live = await customView(t, { onClose: details => notices.push(details) });
  live.context.onClose({ reason: "Host detached" });
  await assert.rejects(live.promise, /Host detached/);
  assert.deepEqual(notices, [{ reason: "Host detached" }]);
  assert.equal(live.disposals, 1);
});

test("Custom async attach can deliver one frame before resolving readiness", async t => {
  const ready = Promise.withResolvers();
  let delivered;
  const live = await customView(t, {}, (context, connection) => {
    delivered = context.onFrame(frame({ title: "early" }));
    return ready.promise.then(() => connection);
  });
  assert.equal(live.view.settled, false);
  ready.resolve();
  await delivered;
  await live.worker.request("draw");
  assert.equal((await live.promise).title, "early");
});

test("Abort during async attach disposes a late connection and suppresses notifications", async t => {
  const ready = Promise.withResolvers();
  const abort = new AbortController();
  const notices = [];
  const live = await customView(t, { signal: abort.signal, onClose: details => notices.push(details) },
    (_context, connection) => ready.promise.then(() => connection));
  abort.abort();
  await assert.rejects(live.promise, { name: "AbortError" });
  ready.resolve();
  await nextTurn();
  assert.equal(live.disposals, 1);
  live.context.onClose({ reason: "late" });
  live.context.onError(new Error("late"));
  assert.deepEqual(notices, []);
});

test("Custom failures reject mounting and release the connection", async t => {
  const live = await customView(t);
  live.context.onError(new Error("Bridge failed"));
  await assert.rejects(live.promise, /Bridge failed/);
  assert.equal(live.disposals, 1);
});

test("Custom attach rejection rejects mount without manufacturing close details", async t => {
  const notices = [];
  const live = await customView(t, { onClose: details => notices.push(details) },
    async () => { throw new Error("Attach rejected"); });
  await assert.rejects(live.promise, /Attach rejected/);
  assert.equal(live.context.signal.aborted, true);
  assert.deepEqual(notices, []);
});

test("Custom close before async readiness cleans up the late connection", async t => {
  const ready = Promise.withResolvers();
  const live = await customView(t, {}, (_context, connection) => ready.promise.then(() => connection));
  live.context.onClose({ reason: "closed while attaching" });
  await assert.rejects(live.promise, /closed while attaching/);
  ready.resolve();
  await nextTurn();
  assert.equal(live.disposals, 1);
});

test("Custom first-frame timeout cancels pending attachment and disposes late connection", async t => {
  t.mock.timers.enable({ apis: ["setTimeout"] });
  const ready = Promise.withResolvers();
  const live = await customView(t, {}, (_context, connection) => ready.promise.then(() => connection));
  t.mock.timers.tick(30000);
  await assert.rejects(live.promise, /Timed out waiting/);
  ready.resolve();
  await nextTurn();
  assert.equal(live.disposals, 1);
  assert.equal(live.context.signal.aborted, true);
});

for (const close of [false, true]) {
  test(`Custom ${close ? "close" : "dispose"} during GPU presentation never acknowledges the frame`, async t => {
    const notices = [];
    const live = await customView(t, { onClose: details => notices.push(details) });
    await live.worker.request("hold");
    await live.context.onFrame(frame());
    await live.worker.request("draw");
    if (close) live.context.onClose({ reason: "gone during render" });
    else live.view.handle.dispose();
    await assert.rejects(live.promise, close ? /gone during render/ : { name: "AbortError" });
    assert.equal(live.commands.some(command => command.type === "ack"), false);
    assert.equal(live.disposals, 1);
    assert.equal(notices.length, close ? 1 : 0);
  });
}

test("A second custom state frame before presentation ACK fails without queueing deltas", async t => {
  const live = await customView(t);
  await live.worker.request("hold");
  await live.context.onFrame(frame());
  await live.worker.request("draw");
  const second = live.context.onFrame(frame({ revision: 2 }));
  const rejected = assert.rejects(second);
  await assert.rejects(live.promise, /second state frame before acknowledgement/);
  await rejected;
  assert.equal(live.disposals, 1);
  assert.equal(live.commands.some(command => command.type === "ack"), false);
});

test("Custom async control failure disconnects an already mounted view and suppresses later controls", async t => {
  const failed = Promise.withResolvers();
  const notices = [];
  const live = await customView(t, {
    onStatus(message, level) { if (level === "error") failed.resolve(message); },
    onClose: details => notices.push(details)
  }, (_context, connection) => ({
    ...connection,
    async send(control) {
      if (JSON.parse(control).type === "paste") throw new Error("Native write failed");
      connection.send(control);
    }
  }));
  await live.context.onFrame(frame());
  await live.worker.request("draw");
  const terminal = await live.promise;
  terminal.paste("failure");
  terminal.resync();
  assert.match(await failed.promise, /Native write failed/);
  assert.equal(terminal.connected, false);
  assert.equal(live.commands.some(command => command.type === "resync"), false);
  assert.equal(live.disposals, 1);
  assert.deepEqual(notices, []);
});

test("Synchronous producer reply to a mismatch ACK does not race the receipt bridge", async t => {
  let reply;
  const live = await customView(t, {}, (context, connection) => ({
    ...connection,
    send(control) {
      connection.send(control);
      if (JSON.parse(control).type === "ack" && JSON.parse(control).revision === 3) {
        reply = context.onFrame(frame({ revision: 4, full: true, title: "fresh" }));
      }
    }
  }));
  await live.context.onFrame(frame());
  await live.worker.request("draw");
  await live.promise;
  await live.context.onFrame(frame({ revision: 3, baseRevision: 2 }));
  await live.waitControl("resync");
  await reply;
  await live.worker.request("draw");
  assert.equal(live.view.handle.title, "fresh");
  assert.equal(live.view.handle.connected, true);
});

test("Custom frames retain revision mismatch ACK-before-resync behavior", async t => {
  const live = await customView(t);
  await live.context.onFrame(frame());
  await live.worker.request("draw");
  await live.promise;
  await live.context.onFrame(frame({ revision: 3, baseRevision: 2 }));
  await live.waitControl("resync");
  const controls = live.commands.filter(command => command.type === "ack" || command.type === "resync");
  assert.deepEqual(controls, [{ type: "ack", revision: 1 }, { type: "ack", revision: 3 }, { type: "resync" }]);
  await live.context.onFrame(frame({ revision: 4, full: true, title: "baseline" }));
  await live.worker.request("draw");
  assert.equal(live.view.handle.title, "baseline");
});
