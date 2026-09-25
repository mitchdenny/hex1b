import test from "node:test";
import assert from "node:assert/strict";
import { Worker as NodeWorker } from "node:worker_threads";
import { LinkDetection } from "../.build/link-detection.js";
import { scanLinks } from "../.build/link-detection-worker.js";
import { LINK_LIMITS } from "../.build/link-options.js";

function snapshot(text = " foo ", revision = 1) {
  return { revision, columns: text.length, rows: 1, hyperlinks: [],
    cells: Array.from(text, (char, index) => ({ index, text: char, width: 1, attributes: 0,
      foreground: 0, background: 0, underlineColor: 0, underlineStyle: 0 })) };
}
const rule = (id = "foo", extra = {}) => ({ id, pattern: /foo/g, kind: "custom", action: "open", ...extra });
class ControlledWorker {
  static instances = [];
  constructor(url, options) { this.url = url; this.options = options; this.messages = []; ControlledWorker.instances.push(this); }
  addEventListener(type, listener) { this[`on${type}`] = listener; }
  postMessage(message) { this.messages.push(message); }
  terminate() { this.terminated = true; }
  flush() { const request = this.messages.shift(); this.onmessage?.({ data: scanLinks(request) }); }
}
function setup(t, rules = [rule()]) {
  t.mock.method(globalThis, "Worker", ControlledWorker);
  ControlledWorker.instances = [];
  const changes = [], errors = [];
  const detector = new LinkDetection({ actions: new Set(["open", "other"]),
    onChange: (revision, links) => changes.push({ revision, links }), onError: error => errors.push(error) });
  t.after(() => detector.dispose());
  detector.configure({ rules });
  return { detector, changes, errors, last: () => changes.at(-1), worker: () => ControlledWorker.instances.at(-1) };
}
// Node has no browser Worker global, so install a descriptor for test-scoped replacement.
if (!("Worker" in globalThis)) globalThis.Worker = ControlledWorker;

test("presets recognize lexical targets and reject substring/drive ambiguities", () => {
  const scan = (builtin, text) => scanLinks({ id: 1, rule: { builtin }, chunks: [{ key: "a", text }] }).results[0].matches.map(m => m.text);
  assert.deepEqual(scan("url", " (https://example.org/a(b)). http://例え.test/道, https://x.test! "),
    ["https://example.org/a(b)", "http://例え.test/道", "https://x.test"]);
  assert.deepEqual(scan("uri", " mailto:a@b.test custom:opaque C:\\Code\\foo C:/Code/foo "),
    ["mailto:a@b.test", "custom:opaque"]);
  assert.deepEqual(scan("absolutePath", " /usr/bin C:\\Code\\foo https://x.test/path relative/foo ~/home "),
    ["/usr/bin", "C:\\Code\\foo"]);
  assert.deepEqual(scan("homePath", " ~/src/code relative~/bad "), ["~/src/code"]);
});

test("worker scans cloned nonglobal regexes, captures and empty unicode matches safely", () => {
  const response = scanLinks({ id: 7, rule: { source: "(?<word>foo)", flags: "u" },
    chunks: [{ key: "a", text: " foo foo " }] });
  assert.equal(response.results[0].matches.length, 2);
  assert.deepEqual(response.results[0].matches[0].groups, { word: "foo" });
  assert.deepEqual(response.results[0].matches[0].captures, ["foo"]);
  const empty = scanLinks({ id: 8, rule: { source: "(?:)", flags: "u" }, chunks: [{ key: "a", text: "😀" }] });
  assert.deepEqual(empty.results[0].matches, []);
  assert.equal(scanLinks({ id: 9, rule: { source: ".", flags: "" },
    chunks: [{ key: "a", text: "x".repeat(LINK_LIMITS.matches + 1) }] }).error, "limit");
  const amplified = scanLinks({ id: 10, rule: { source: "(".repeat(100) + "x+" + ")".repeat(100), flags: "" },
    chunks: [{ key: "a", text: "x".repeat(3000) }] });
  assert.equal(amplified.error, "limit");
  assert.match(amplified.message, /result text/u);
  const emptyCaptures = scanLinks({ id: 11, rule: { source: "()".repeat(1000) + "x", flags: "" },
    chunks: [{ key: "a", text: "x".repeat(1000) }] });
  assert.equal(emptyCaptures.error, "limit");
  assert.match(emptyCaptures.message, /result text/u);
});

test("worker is lazy; advance rebases in-flight and cached matches without regex runs", t => {
  const state = setup(t);
  assert.equal(ControlledWorker.instances.length, 0);
  state.detector.update(snapshot());
  const worker = state.worker();
  state.detector.advance(2);
  worker.flush();
  const link = state.last().links[0];
  assert.equal(link.activation.revision, 2);
  assert.equal(link.activation.target, "foo");
  assert.ok(Object.isFrozen(link.activation));
  assert.ok(Object.isFrozen(link.activation.ranges[0]));
  state.detector.advance(3);
  assert.equal(state.last().links[0].id, link.id);
  assert.equal(state.last().links[0].activation.revision, 3);
  assert.equal(worker.messages.length, 0);
  state.detector.update(snapshot(" foo ", 4));
  assert.equal(state.last().links.length, 1);
  assert.equal(worker.messages.length, 0);
});

test("OSC occupancy blocks all detected candidates even unsafe schemes", t => {
  const state = setup(t);
  const source = snapshot();
  source.hyperlinks = [{ row: 0, startColumn: 2, endColumn: 3, uri: "javascript:blocked" }];
  state.detector.update(source);
  state.worker().flush();
  assert.equal(state.last().links.length, 0);
  state.detector.update({ ...source, revision: 2, hyperlinks: [] });
  assert.equal(state.last().links.length, 1);
  assert.equal(state.worker().messages.length, 0);
});

test("resolver rejection, overrides, frozen inputs and rule precedence", t => {
  const data = { consumerOwned: true };
  const state = setup(t, [rule("reject", { resolve: () => null }),
    rule("accept", { resolve: match => {
      assert.ok(Object.isFrozen(match)); assert.ok(Object.isFrozen(match.chunk));
      assert.ok(Object.isFrozen(match.captures)); assert.ok(Object.isFrozen(match.groups));
      return { target: "resolved", action: "other", data };
    } }), rule("overlap")]);
  state.detector.update(snapshot());
  state.worker().flush(); state.worker().flush(); state.worker().flush();
  assert.equal(state.last().links.length, 1);
  assert.equal(state.last().links[0].activation.ruleId, "accept");
  assert.equal(state.last().links[0].activation.target, "resolved");
  assert.equal(state.last().links[0].action, "other");
  assert.equal(state.last().links[0].activation.data, data);
  assert.equal(Object.isFrozen(data), false);
});

test("bad resolvers disable the entire rule until configuration replacement", t => {
  let calls = 0;
  const state = setup(t, [rule("bad", { resolve: () => { calls++; return Promise.resolve({ target: "bad" }); } }), rule("good")]);
  state.detector.update(snapshot(" foo foo "));
  state.worker().flush(); state.worker().flush();
  assert.equal(state.errors[0].code, "resolver");
  assert.equal(calls, 1);
  assert.equal(state.last().links.length, 2);
  state.detector.update(snapshot(" foo ", 2));
  state.worker().flush();
  assert.equal(calls, 1);
  assert.equal(state.last().links.length, 1);
});

test("runtime action override is validated and invalid configure leaves old rule running", t => {
  const state = setup(t, [rule("bad", { resolve: () => ({ target: "foo", action: "copySelection" }) })]);
  state.detector.update(snapshot());
  assert.throws(() => state.detector.configure({ rules: [rule("bad-config", { action: "missing" })] }));
  state.worker().flush();
  assert.equal(state.errors[0].code, "resolver");
  assert.equal(state.last().links.length, 0);
});

test("latest-work coalescing discards old results and scans only latest pending snapshot", t => {
  const state = setup(t);
  state.detector.update(snapshot(" foo ", 1));
  const worker = state.worker();
  state.detector.update(snapshot(" foo bar ", 2));
  state.detector.update(snapshot(" foo baz ", 3));
  assert.equal(worker.messages.length, 1);
  worker.flush();
  assert.equal(worker.messages.length, 1);
  assert.equal(worker.messages[0].chunks[0].text, " foo baz ");
  assert.equal(state.last().links.length, 0);
  worker.flush();
  assert.equal(state.last().revision, 3);
  assert.equal(state.last().links.length, 1);
});

test("clear and disable cancel work; worker failure does not restart in a loop", t => {
  const state = setup(t);
  state.detector.update(snapshot());
  const old = state.worker(), callback = old.onmessage, response = scanLinks(old.messages[0]);
  state.detector.clear();
  assert.equal(old.terminated, true);
  callback({ data: response });
  assert.equal(state.last().links.length, 0);
  state.detector.update(snapshot(" foo ", 2));
  const worker = state.worker();
  worker.onerror({ preventDefault() {}, message: "failed" });
  assert.equal(state.errors[0].code, "worker");
  state.detector.update(snapshot(" foo ", 3));
  assert.equal(ControlledWorker.instances.length, 2);
  state.detector.configure(false);
  state.detector.update(snapshot(" foo ", 4));
  assert.equal(ControlledWorker.instances.length, 2);
});

test("configure false emits an empty result and never reports feature errors", t => {
  const state = setup(t);
  state.detector.update(snapshot());
  const worker = state.worker();
  const lateError = worker.onerror;
  state.detector.configure(false);
  assert.equal(state.last().revision, 1);
  assert.deepEqual(state.last().links, []);
  assert.equal(worker.terminated, true);
  lateError({ preventDefault() {}, message: "late worker error" });
  state.detector.update(snapshot(" foo ", 2));
  state.detector.advance(3);
  assert.equal(state.last().revision, 3);
  assert.deepEqual(state.errors, []);
  assert.equal(ControlledWorker.instances.length, 1);
});

test("row cache rescans only changed rows and logical cache invalidates a whole wrapped line", t => {
  const state = setup(t, [rule("rows", { text: "physicalRow" }), rule("logical")]);
  const source = snapshot(" foo  foo ");
  source.columns = 5; source.rows = 2;
  source.cells[4].attributes = 1024;
  state.detector.update(source);
  assert.equal(state.worker().messages[0].chunks.length, 2);
  state.worker().flush();
  assert.equal(state.worker().messages[0].chunks.length, 1);
  assert.equal(state.worker().messages[0].chunks[0].text, " foo  foo ");
  state.worker().flush();
  const next = structuredClone(source);
  next.revision = 2; next.cells[7].text = "a";
  state.detector.update(next);
  assert.equal(state.worker().messages[0].chunks.length, 1);
  assert.equal(state.worker().messages[0].chunks[0].text, " fao ");
  state.worker().flush();
  assert.equal(state.worker().messages[0].chunks[0].text, " foo  fao ");
  state.worker().flush();
});

test("missing worker and malformed worker response fail closed without inline regex fallback", t => {
  const state = setup(t);
  state.detector.update(snapshot());
  const worker = state.worker();
  worker.onmessage({ data: { id: worker.messages[0].id, results: [null] } });
  assert.equal(state.errors[0].code, "worker");
  assert.equal(state.last().links.length, 0);
  assert.equal(worker.terminated, true);
});

test("oversized snapshot and worker match budget report feature-local limit diagnostics", t => {
  const state = setup(t, [rule("all", { pattern: /x/g })]);
  const oversized = snapshot(" x ");
  oversized.cells[1].text = "x".repeat(LINK_LIMITS.chunk + 1);
  state.detector.update(oversized);
  assert.equal(state.errors[0].code, "limit");
  assert.equal(ControlledWorker.instances.length, 0);
  state.detector.update(snapshot(` ${"x".repeat(LINK_LIMITS.matches + 1)} `, 2));
  state.worker().flush();
  assert.equal(state.errors[1].code, "limit");
  assert.equal(state.errors[1].ruleId, "all");
  assert.equal(state.last().links.length, 0);
  state.detector.update(snapshot(" x ", 3));
  assert.equal(state.worker().messages.length, 0);
  state.detector.configure({ rules: [rule("all", { pattern: /x/g })] });
  state.detector.update(snapshot(" x ", 4));
  state.worker().flush();
  assert.equal(state.last().links.length, 1);
});

test("cache eviction accounts for captured result payload rather than keys alone", t => {
  const pattern = new RegExp("(".repeat(60) + "x+" + ")".repeat(60));
  const state = setup(t, Array.from({ length: 8 }, (_, index) => rule(`captures-${index}`, { pattern })));
  const text = ` ${"x".repeat(3000)} `;
  state.detector.update(snapshot(text));
  for (let index = 0; index < 8; index++) state.worker().flush();
  assert.equal(state.last().links.length, 1);
  assert.deepEqual(state.errors, []);
  state.detector.update(snapshot(text, 2));
  assert.equal(state.worker().messages.length, 1, "Captured payload should have forced cache eviction");
});

test("real isolated worker terminates stalled scanner and recovers unrelated rules", async t => {
  const workers = [];
  const stallPattern = /__hex1b_timeout__/g;
  class BrowserWorker {
    constructor(url) {
      this.worker = new NodeWorker(new URL(`data:text/javascript,${encodeURIComponent(`
        import { parentPort, workerData } from "node:worker_threads";
        globalThis.self = globalThis;
        globalThis.postMessage = data => parentPort.postMessage(data);
        let onmessage;
        globalThis.addEventListener = (type, listener) => { if (type === "message") onmessage = listener; };
        const ready = import(workerData);
        parentPort.on("message", async data => {
          await ready;
          if (data?.rule?.source === ${JSON.stringify(stallPattern.source)}) {
            Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 1000);
          }
          onmessage({ data });
        });
      `)}`), { workerData: url.href, execArgv: [] });
      workers.push(this);
      this.worker.on("message", data => this.onmessage?.({ data }));
      this.worker.on("error", error => this.onerror?.({ preventDefault() {}, message: error.message }));
      this.worker.unref();
    }
    addEventListener(type, listener) { this[`on${type}`] = listener; }
    postMessage(message) { this.worker.postMessage(message); }
    terminate() {
      this.terminated = true;
      return this.termination ??= this.worker.terminate();
    }
  }
  const previousWorker = globalThis.Worker;
  globalThis.Worker = BrowserWorker;
  const completion = Promise.withResolvers();
  const errors = [];
  const detector = new LinkDetection({ actions: new Set(["open"]),
    workerUrl: new URL("../.build/link-detection-worker.js", import.meta.url),
    onChange: (_, links) => { if (links.length) completion.resolve(links); },
    onError: error => {
      errors.push(error);
      if (error.code !== "timeout" || error.ruleId !== "evil") {
        completion.reject(new Error(`Unexpected detector error: ${JSON.stringify(error)}`));
      }
    } });
  const timer = setTimeout(() => completion.reject(
    new Error(`Detection recovery timed out: ${JSON.stringify(errors)}`)), 3000);
  try {
    detector.configure({ rules: [rule("evil", { pattern: stallPattern }), rule("good")] });
    detector.update(snapshot(" foo "));
    const links = await completion.promise;
    assert.equal(errors[0].code, "timeout");
    assert.equal(errors[0].ruleId, "evil");
    assert.equal(workers[0].terminated, true);
    assert.equal(links[0].activation.ruleId, "good");
  } finally {
    clearTimeout(timer);
    detector.dispose();
    globalThis.Worker = previousWorker;
    completion.resolve([]);
    await Promise.all(workers.map(worker => worker.terminate()));
  }
});
