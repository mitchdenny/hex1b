import assert from "node:assert/strict";
import { test } from "node:test";
import { browser, frame, mounting, present } from "./fixtures/browser.mjs";

test("Activity arrives after presentation, before mount, with both getters set and isolated from callbacks", async t => {
  const workers = browser(t);
  const progressNotices = [];
  const shellNotices = [];
  const initialProgress = { state: "indeterminate", percentage: null };
  const initialShell = { phase: "executing", lastExitCode: -1 };
  const view = await mounting(t, workers, {
    onProgressChange(progress) {
      assert.deepEqual(view.handle.progress, progress);
      assert.deepEqual(view.handle.shellIntegration, initialShell);
      progressNotices.push({ ...progress });
      progress.percentage = 99;
    },
    onShellIntegrationChange(shell) {
      assert.deepEqual(view.handle.progress, initialProgress);
      assert.deepEqual(view.handle.shellIntegration, shell);
      shellNotices.push({ ...shell });
      shell.phase = "finished";
    }
  });
  await view.worker.request("open");
  await view.worker.request("hold");
  await view.worker.request("frame", { buffer: frame({ progress: initialProgress, shellIntegration: initialShell }) });
  await view.worker.request("draw");
  assert.deepEqual(progressNotices, []);
  assert.deepEqual(shellNotices, []);
  assert.equal(view.settled, false);
  await view.worker.request("release");
  const terminal = await view.promise;
  assert.deepEqual(progressNotices, [initialProgress]);
  assert.deepEqual(shellNotices, [initialShell]);
  assert.deepEqual(terminal.progress, initialProgress);
  assert.deepEqual(terminal.shellIntegration, initialShell);
  terminal.progress.state = "error";
  terminal.shellIntegration.lastExitCode = 123;
  assert.deepEqual(terminal.progress, initialProgress);
  assert.deepEqual(terminal.shellIntegration, initialShell);
  assert.deepEqual(view.worker.errors, []);
});

test("Activity callbacks deliver defaults then distinct states, never infer missing intermediate markers", async t => {
  const workers = browser(t);
  const progressNotices = [];
  const shellNotices = [];
  const view = await mounting(t, workers, {
    onProgressChange(value) { progressNotices.push(value); },
    onShellIntegrationChange(value) { shellNotices.push(value); }
  });
  await view.worker.request("open");
  await present(view, {});
  const terminal = await view.promise;
  assert.deepEqual(progressNotices, [{ state: "none", percentage: null }]);
  assert.deepEqual(shellNotices, [{ phase: "unknown", lastExitCode: null }]);
  let revision = 1;
  for (const [state, percentage] of [["normal", 0], ["normal", 100], ["warning", 50], ["error", 25],
    ["indeterminate", null], ["none", null]]) {
    await present(view, { revision: ++revision, progress: { state, percentage },
      shellIntegration: { phase: "finished", lastExitCode: 1 } });
  }
  assert.equal(progressNotices.length, 7);
  assert.deepEqual(shellNotices, [{ phase: "unknown", lastExitCode: null }, { phase: "finished", lastExitCode: 1 }]);
  await present(view, { revision: ++revision, shellIntegration: { phase: "commandLine", lastExitCode: 1 } });
  terminal.resync();
  await present(view, { revision: ++revision, full: true, shellIntegration: { phase: "commandLine", lastExitCode: 1 } });
  assert.equal(shellNotices.length, 3);
  assert.equal(progressNotices.length, 7);
  assert.deepEqual(terminal.shellIntegration, { phase: "commandLine", lastExitCode: 1 });
  assert.equal(terminal.title, "");
  assert.deepEqual(view.worker.errors, []);
});

test("Activity ignores connecting replicas, dropped and malformed frames, and retains last state on disconnect", async t => {
  const workers = browser(t);
  const notices = [];
  const view = await mounting(t, workers, { onProgressChange(value) { notices.push(value); } });
  await view.worker.request("open");
  await present(view, { peer: { id: null, primaryId: null, isPrimary: false } });
  assert.deepEqual(notices, []);
  assert.equal(view.settled, false);
  const peer = { id: "replica", primaryId: "producer", isPrimary: false };
  const progress = { state: "warning", percentage: 40 };
  await present(view, { revision: 2, peer, progress });
  const terminal = await view.promise;
  await present(view, { revision: 3, baseRevision: 99, peer });
  assert.deepEqual(terminal.progress, progress);
  await present(view, { revision: 4, full: true, peer, progress });
  await present(view, { revision: 5, peer, progress: { state: "normal", percentage: 101 } });
  assert.equal(terminal.connected, false);
  assert.deepEqual(terminal.progress, progress);
  assert.deepEqual(notices, [progress]);
  terminal.dispose();
  assert.deepEqual(terminal.progress, progress);
  const remount = await mounting(t, workers, { onProgressChange(value) { notices.push(value); } });
  await remount.worker.request("open");
  await present(remount, { peer, progress });
  await remount.promise;
  assert.deepEqual(notices, [progress, progress]);
});

test("Disposing in a progress callback suppresses the paired shell callback and queued messages", async t => {
  const workers = browser(t);
  const notices = [];
  const view = await mounting(t, workers, {
    onProgressChange(value) {
      notices.push(value);
      if (value.state === "error") view.handle.dispose();
    },
    onShellIntegrationChange(value) { notices.push(value); }
  });
  await view.worker.request("open");
  await present(view, {});
  const terminal = await view.promise;
  const progress = { state: "error", percentage: 25 };
  const shellIntegration = { phase: "finished", lastExitCode: 1 };
  const geometry = view.worker.outputs.find(message => message.type === "geometry");
  // Disposal terminates the worker, so do not wait for its draw acknowledgement.
  view.worker.deliver({ ...geometry, revision: 2, progress, shellIntegration });
  view.worker.deliver({ ...geometry, revision: 3 });
  assert.deepEqual(notices, [
    { state: "none", percentage: null }, { phase: "unknown", lastExitCode: null }, progress
  ]);
  assert.deepEqual(terminal.progress, progress);
  assert.deepEqual(terminal.shellIntegration, shellIntegration);
  assert.deepEqual(view.worker.errors, []);
});

for (const initial of ["", "shell; 世界 😀"]) {
  test(`Mount publishes initial title ${JSON.stringify(initial)} only after presentation, then distinct changes`, async t => {
    const workers = browser(t);
    const view = await mounting(t, workers);
    await view.worker.request("open");
    await view.worker.request("hold");
    await view.worker.request("frame", { buffer: frame({ title: initial }) });
    assert.deepEqual(view.notices, []);
    await view.worker.request("draw");
    assert.deepEqual(view.notices, []);
    assert.equal(view.settled, false);
    await view.worker.request("release");
    const terminal = await view.promise;
    assert.deepEqual(view.notices, [{ title: initial, settled: false }]);
    assert.equal(terminal.title, initial);
    assert.throws(() => { terminal.title = "not writable"; }, TypeError);

    await present(view, { title: initial, revision: 2 });
    terminal.resync();
    await present(view, { title: initial, revision: 3, full: true });
    await view.worker.request("pulse");
    await view.worker.request("input", { message: { type: "viewport", width: 100, height: 100 } });
    await view.worker.request("draw");
    assert.equal(view.notices.length, 1);
    assert.equal(view.worker.outputs.filter(message => message.type === "geometry").length, 3);

    const literal = '<img src=x onerror="alert(1)">\u202e title';
    await present(view, { title: literal, revision: 4 });
    await present(view, { title: "", revision: 5 });
    await present(view, { title: "", revision: 6 });
    assert.deepEqual(view.notices.map(notice => notice.title), [initial, literal, ""]);
    assert.equal(terminal.title, "");
    assert.equal(document.title, "Host document");
    assert.equal(terminal.element.shadowRoot.querySelector("textarea").getAttribute("aria-label"), "Host input label");
    assert.equal(view.container.textContent, "");
    assert.deepEqual(view.worker.errors, []);
    terminal.dispose();
  });
}

test("Relay waits for authoritative peer state, independent views and remount deliver their own initial titles", async t => {
  const workers = browser(t);
  const relay = await mounting(t, workers);
  await relay.worker.request("open");
  const disconnected = { id: null, primaryId: null, isPrimary: false };
  await present(relay, { title: "", peer: disconnected });
  await relay.worker.request("pulse");
  assert.deepEqual(relay.notices, []);
  assert.equal(relay.settled, false);
  const peer = { id: "relay", primaryId: "producer", isPrimary: false };
  await present(relay, { title: "already running", revision: 2, peer });
  const terminal = await relay.promise;
  assert.deepEqual(relay.notices, [{ title: "already running", settled: false }]);

  const independent = await mounting(t, workers);
  await independent.worker.request("open");
  await present(independent, { title: "different workload" });
  const other = await independent.promise;
  assert.equal(other.title, "different workload");
  assert.equal(terminal.title, "already running");

  await relay.worker.request("disconnect");
  assert.equal(terminal.connected, false);
  assert.equal(terminal.title, "already running");
  terminal.dispose();
  assert.equal(terminal.title, "already running");
  assert.equal(relay.notices.length, 1);
  const remount = await mounting(t, workers);
  await remount.worker.request("open");
  await present(remount, { title: "already running", peer });
  await remount.promise;
  assert.deepEqual(remount.notices, [{ title: "already running", settled: false }]);
});

test("Dropped deltas and invalid frames never publish their titles or clear the last known value", async t => {
  const workers = browser(t);
  const view = await mounting(t, workers);
  await view.worker.request("open");
  await present(view, { title: "accepted" });
  const terminal = await view.promise;
  await present(view, { title: "discarded", revision: 2, baseRevision: 99 });
  assert.equal(terminal.title, "accepted");
  assert.deepEqual(view.worker.commands.slice(-2), [{ type: "ack", revision: 2 }, { type: "resync" }]);
  await present(view, { title: "accepted", revision: 3, full: true });
  await present(view, { title: "invalid\u0007", revision: 4 });
  assert.equal(terminal.title, "accepted");
  assert.equal(terminal.connected, false);
  assert.deepEqual(view.notices.map(notice => notice.title), ["accepted"]);
  assert.equal(view.worker.outputs.filter(message => message.type === "geometry").length, 2);
  assert.ok(view.worker.outputs.some(message => message.type === "status" && message.level === "error"));
});

test("Disposal and abort retain titles and ignore already queued geometry messages", async t => {
  const workers = browser(t);
  for (const abort of [false, true]) {
    const controller = new AbortController();
    const view = await mounting(t, workers, { signal: controller.signal });
    await view.worker.request("open");
    await present(view, { title: "retained" });
    const terminal = await view.promise;
    await view.worker.request("hold");
    await view.worker.request("frame", { buffer: frame({ title: "never presented", revision: 2 }) });
    await view.worker.request("draw");
    if (abort) controller.abort();
    else terminal.dispose();
    const geometry = view.worker.outputs.find(message => message.type === "geometry");
    view.worker.deliver({ ...geometry, title: "queued after disposal", revision: 3 });
    assert.equal(terminal.title, "retained");
    assert.deepEqual(view.notices.map(notice => notice.title), ["retained"]);
    assert.equal(view.container.children.length, 0);
  }
});

test("Aborting before initial presentation rejects mount without a title notification", async t => {
  const workers = browser(t);
  const controller = new AbortController();
  const view = await mounting(t, workers, { signal: controller.signal });
  await view.worker.request("open");
  await view.worker.request("frame", { buffer: frame({ title: "not presented" }) });
  controller.abort();
  await assert.rejects(view.promise, { name: "AbortError" });
  assert.deepEqual(view.notices, []);
  assert.equal(view.handle.title, "");
});

test("A host callback can abort during geometry delivery without a subsequent title callback", async t => {
  const workers = browser(t);
  const controller = new AbortController();
  const view = await mounting(t, workers, {
    signal: controller.signal, onGeometry() { controller.abort(); }
  });
  await view.worker.request("open");
  // Deliver the same geometry message shape the real worker emits, reentrantly aborting mounting.
  view.worker.deliver({
    type: "geometry", title: "not notified", revision: 1, text: "", history: null, hyperlinks: [],
    columns: 1, rows: 1, cellWidth: 10, cellHeight: 20, mouseTracking: 0,
    peer: { id: null, primaryId: null, isPrimary: true }
  });
  await assert.rejects(view.promise, { name: "AbortError" });
  assert.deepEqual(view.notices, []);
});

test("Title callback errors reach the host without retrying a delivered value", async t => {
  const workers = browser(t);
  const failure = new Error("Host callback failed");
  const view = await mounting(t, workers, { onTitleChange() { throw failure; } });
  await view.worker.request("open");
  await present(view, { title: "delivered" });
  const terminal = await view.promise;
  assert.equal(terminal.title, "delivered");
  assert.deepEqual(view.worker.errors, [failure]);
  await present(view, { title: "delivered", revision: 2 });
  assert.equal(view.notices.length, 1);
  assert.deepEqual(view.worker.errors, [failure]);
});
