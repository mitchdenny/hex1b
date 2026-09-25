import assert from "node:assert/strict";
import { test } from "node:test";
import { ScrollbarTooltip, renderDefaultScrollbarTooltip } from "../.build/scrollbar-tooltip.js";

function installGlobal(t, name, value) {
  const previous = Object.getOwnPropertyDescriptor(globalThis, name);
  Object.defineProperty(globalThis, name, { configurable: true, value });
  t.after(() => {
    if (previous) Object.defineProperty(globalThis, name, previous);
    else delete globalThis[name];
  });
}

function dom(t) {
  class Element {
    style = {};
    attributes = new Map();
    children = [];
    parentElement = null;
    textContent = "";
    offsetWidth = 100;
    offsetHeight = 40;
    setAttribute(name, value) { this.attributes.set(name, value); }
    getAttribute(name) { return this.attributes.get(name) ?? null; }
    set innerHTML(_value) { assert.fail("tooltip text must not be interpreted as HTML"); }
    append(element) {
      element.remove();
      this.children.push(element);
      element.parentElement = this;
    }
    replaceChildren(...elements) {
      for (const child of this.children) child.parentElement = null;
      this.children = [];
      for (const element of elements) this.append(element);
    }
    remove() {
      if (!this.parentElement) return;
      const parent = this.parentElement;
      parent.children.splice(parent.children.indexOf(this), 1);
      this.parentElement = null;
    }
  }
  const observers = [];
  class Observer {
    disconnected = false;
    constructor(callback) { this.callback = callback; observers.push(this); }
    observe(element) { this.element = element; }
    disconnect() { this.disconnected = true; }
    resize() { if (!this.disconnected) this.callback([]); }
  }
  installGlobal(t, "HTMLElement", Element);
  installGlobal(t, "document", {
    createElement(tag) { assert.equal(tag, "div"); return new Element(); }
  });
  installGlobal(t, "ResizeObserver", Observer);
  return { Element, observers };
}

function layout(width = 320, height = 200) {
  return Object.freeze({
    width, height, cellWidth: 8, cellHeight: 16,
    content: Object.freeze({ left: 10, top: 12, width: width - 32, height: height - 24 }),
    scrollbar: Object.freeze({ left: width - 22, top: 12, width: 12, height: height - 24 }),
    padding: Object.freeze({ left: 10, right: 10, top: 12, bottom: 12 })
  });
}

function tick(id = "a", changes = {}, bounds = {}) {
  return Object.freeze({
    marker: Object.freeze({
      id, source: "command", buffer: "main", row: 10, column: 0,
      phase: "executing", exitCode: null, ...changes
    }),
    bounds: Object.freeze({ left: 298, top: 90, width: 12, height: 4, ...bounds })
  });
}

function details(rawParameters = "cmdline_url=echo%20hello", changes = {}) {
  return { phase: "finished", exitCode: 0, rawParameters, ...changes };
}

function context(marker, changes = {}) {
  return Object.freeze({
    marker: marker.marker, anchor: marker.bounds, layout: layout(),
    details: null, loading: false, error: null, signal: new AbortController().signal,
    ...changes
  });
}

function deferred() {
  let resolve, reject;
  const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
}

// Yield through the microtask queue without a timing-dependent sleep.
function settle() { return new Promise(resolve => setImmediate(resolve)); }

function fixture(t) {
  const { Element, observers } = dom(t);
  const overlay = new Element();
  const errors = [];
  const requests = [];
  const tooltip = new ScrollbarTooltip({
    overlay,
    getDetails(id) {
      const request = { id, ...deferred() };
      requests.push(request);
      return request.promise;
    },
    reportError: error => errors.push(error)
  });
  t.after(() => tooltip.dispose());
  return { tooltip, overlay, errors, requests, Element, observers };
}

test("default content uses safe text, decoded cmdline_url and command status", t => {
  dom(t);
  const label = '<img src=x onerror="bad()">';
  const command = 'echo "<script>alert(1)</script>" && printf café';
  const element = renderDefaultScrollbarTooltip(context(tick("a", {
    label, phase: "finished", exitCode: 7
  }), { details: details(`other=ignored;cmdline_url=${encodeURIComponent(command)}`) }));
  assert.equal(element.textContent, `${label}\nPhase: finished\nExit code: 7\n${command}`);
  assert.equal(element.getAttribute("role"), "tooltip");
  assert.equal(element.className, "hex1b-scrollbar-tooltip");
  assert.match(element.style.cssText, /var\(--cp-view-surface/);
  assert.deepEqual(element.children, []);
});

test("default content explicitly preserves malformed encoding and falls back when text is missing", t => {
  dom(t);
  for (const [raw, expected] of [
    ["cmdline_url=%E0%A4%A", "Command (invalid URL encoding): %E0%A4%A"],
    ["cmdline_url=%", "Command (invalid URL encoding): %"],
    ["other=raw <b>text</b>", "other=raw <b>text</b>"],
    ["", "This shell did not provide command text."],
    [null, "This shell did not provide command text."]
  ]) {
    const element = renderDefaultScrollbarTooltip(context(tick(), { details: details(raw) }));
    assert.equal(element.textContent, `Terminal command\nPhase: executing\n${expected}`);
  }
  assert.equal(renderDefaultScrollbarTooltip(context(tick())).textContent,
    "Terminal command\nPhase: executing");
  assert.equal(renderDefaultScrollbarTooltip(context(tick("a", { phase: undefined }))).textContent,
    "Terminal command\nPhase: unknown");
});

test("default custom labels never expose command-only status or missing-detail messages", t => {
  dom(t);
  for (const [label, expected] of [[undefined, "Bookmark"], ["", "Bookmark"], ["<b>note</b>", "<b>note</b>"]]) {
    const element = renderDefaultScrollbarTooltip(context(tick("custom", {
      source: "custom", label, exitCode: 3
    }), { loading: true, error: "failed", details: details() }));
    assert.equal(element.textContent, expected);
  }
});

test("custom elements mount with terminal-local CSS positioning and resize clamping", t => {
  const { tooltip, overlay, Element, observers, requests } = fixture(t);
  const element = new Element();
  const renderer = () => element;
  tooltip.update(tick("custom", { source: "custom" }), layout(), renderer);
  assert.deepEqual(overlay.children, [element]);
  assert.deepEqual(element.style, {
    position: "absolute", boxSizing: "border-box", margin: "0", pointerEvents: "none",
    maxWidth: "304px", maxHeight: "184px", overflow: "hidden", left: "190px", top: "72px"
  });
  assert.equal(observers[0].element, element);
  element.offsetWidth = 240;
  element.offsetHeight = 160;
  observers[0].resize();
  assert.equal(element.style.left, "50px");
  assert.equal(element.style.top, "12px");
  tooltip.update(tick("custom", { source: "custom" }, { left: 4, top: -50 }), layout(), renderer);
  assert.equal(observers[0].disconnected, true);
  assert.equal(element.style.left, "8px");
  assert.equal(element.style.top, "8px");
  tooltip.update(tick("custom", { source: "custom" }, { left: 1000, top: 1000 }), layout(), renderer);
  assert.equal(element.style.left, "72px");
  assert.equal(element.style.top, "32px");
  assert.equal(requests.length, 0);
});

test("positioning bounds oversized elements and nonnegative constraints in tiny layouts", t => {
  const { tooltip, Element } = fixture(t);
  const element = new Element();
  const renderer = () => element;
  tooltip.update(tick("custom", { source: "custom" }), layout(8, 8), renderer);
  assert.equal(element.style.maxWidth, "0px");
  assert.equal(element.style.maxHeight, "0px");
  assert.equal(element.style.left, "0px");
  assert.equal(element.style.top, "0px");
  tooltip.update(tick("custom", { source: "custom" }), layout(1000, 500), renderer);
  assert.equal(element.style.maxWidth, "360px");
  assert.equal(element.style.maxHeight, "484px");
});

test("left-expanding capsule anchors reposition tooltips without refetching command details", async t => {
  const { tooltip, overlay, requests, Element } = fixture(t);
  const seen = [];
  const renderer = context => { seen.push(context); return new Element(); };
  tooltip.update(tick("a", {}, { left: 302.5, width: 3, height: 3 }), layout(), renderer);
  await settle();
  requests[0].resolve(details());
  await settle();
  const previous = seen.at(-1);
  assert.equal(overlay.children[0].style.left, "194.5px");
  tooltip.update(tick("a", {}, { left: 286, width: 19.5, height: 3 }), layout(), renderer);
  await settle();
  assert.equal(previous.signal.aborted, true);
  assert.equal(seen.at(-1).anchor.left, 286);
  assert.equal(seen.at(-1).details.rawParameters, "cmdline_url=echo%20hello");
  assert.equal(overlay.children[0].style.left, "178px");
  assert.equal(requests.length, 1);
});

test("callback snapshots and command results are immutable without freezing producer objects", async t => {
  const { tooltip, requests } = fixture(t);
  const seen = [];
  const marker = tick();
  const geometry = layout();
  tooltip.update(marker, geometry, value => { seen.push(value); return null; });
  const initial = seen[0];
  assert.equal(initial.marker, marker.marker);
  assert.equal(initial.anchor, marker.bounds);
  assert.equal(initial.layout, geometry);
  assert.equal(initial.loading, true);
  for (const value of [initial, initial.marker, initial.anchor, initial.layout,
    initial.layout.content, initial.layout.scrollbar, initial.layout.padding])
    assert.equal(Object.isFrozen(value), true);
  assert.throws(() => { initial.loading = false; }, TypeError);
  assert.throws(() => { initial.marker.id = "changed"; }, TypeError);
  assert.throws(() => { initial.anchor.top = 1; }, TypeError);
  assert.throws(() => { initial.layout.content.left = 1; }, TypeError);
  await settle();
  const result = details();
  requests[0].resolve(result);
  await settle();
  assert.equal(seen.length, 2);
  assert.equal(initial.signal.aborted, true);
  assert.equal(seen[1].signal.aborted, false);
  assert.equal(seen[1].loading, false);
  assert.equal(Object.isFrozen(seen[1].details), true);
  assert.notEqual(seen[1].details, result);
  assert.throws(() => { seen[1].details.rawParameters = "mutated"; }, TypeError);
  result.rawParameters = "producer changed";
  assert.equal(seen[1].details.rawParameters, "cmdline_url=echo%20hello");
});

test("unchanged presentations preserve the element and lifetime; replacement, hide and dispose abort once", t => {
  const { tooltip, overlay, Element, observers } = fixture(t);
  const seen = [], aborted = [];
  const renderer = value => {
    seen.push(value);
    value.signal.addEventListener("abort", () => aborted.push(value));
    return new Element();
  };
  const marker = tick("custom", { source: "custom" });
  tooltip.update(marker, layout(), renderer);
  const first = overlay.children[0];
  tooltip.update(tick("custom", { source: "custom" }), layout(), renderer);
  assert.equal(seen.length, 1);
  assert.equal(overlay.children[0], first);
  assert.equal(seen[0].signal.aborted, false);
  tooltip.update(marker, layout(400), renderer);
  assert.deepEqual(aborted, [seen[0]]);
  assert.equal(first.parentElement, null);
  assert.equal(observers[0].disconnected, true);
  tooltip.update(null, layout(), renderer);
  assert.deepEqual(aborted, seen);
  assert.deepEqual(overlay.children, []);
  tooltip.update(marker, layout(), renderer);
  tooltip.update(marker, layout(), false);
  assert.deepEqual(aborted, seen);
  tooltip.update(marker, layout(), renderer);
  tooltip.dispose();
  tooltip.dispose();
  tooltip.update(marker, layout(), renderer);
  assert.equal(seen.length, 4);
  assert.deepEqual(aborted, seen);
  assert.deepEqual(overlay.children, []);
  assert.ok(observers.every(observer => observer.disconnected));
});

test("null renderer results support host-owned UI cleanup for every lifetime", t => {
  const { tooltip, overlay, Element, observers } = fixture(t);
  const host = new Element();
  let cleanups = 0;
  const renderer = value => {
    const element = new Element();
    host.append(element);
    value.signal.addEventListener("abort", () => { element.remove(); cleanups++; }, { once: true });
    return null;
  };
  tooltip.update(tick("one", { source: "custom" }), layout(), renderer);
  assert.equal(host.children.length, 1);
  assert.deepEqual(overlay.children, []);
  assert.equal(observers.length, 0);
  tooltip.update(tick("two", { source: "custom" }), layout(), renderer);
  assert.equal(cleanups, 1);
  assert.equal(host.children.length, 1);
  tooltip.update(null, layout(), renderer);
  assert.equal(cleanups, 2);
  assert.equal(host.children.length, 0);
  tooltip.update(tick("three", { source: "custom" }), layout(), renderer);
  tooltip.dispose();
  assert.equal(cleanups, 3);
  assert.equal(host.children.length, 0);
});

test("delayed RPCs stay single-flight, coalesce to latest hover and never paint stale results", async t => {
  const { tooltip, overlay, requests } = fixture(t);
  tooltip.update(tick("a", { label: "A" }), layout());
  assert.match(overlay.children[0].textContent, /Loading command details/);
  await settle();
  assert.deepEqual(requests.map(request => request.id), ["a"]);
  tooltip.update(tick("b", { label: "B" }), layout());
  tooltip.update(tick("c", { label: "C" }), layout());
  await settle();
  assert.deepEqual(requests.map(request => request.id), ["a"]);
  const latestLoadingElement = overlay.children[0];
  requests[0].resolve(details("cmdline_url=STALE"));
  await settle();
  assert.deepEqual(requests.map(request => request.id), ["a", "c"]);
  assert.equal(overlay.children[0], latestLoadingElement);
  assert.doesNotMatch(overlay.children[0].textContent, /STALE/);
  requests[1].resolve(details("cmdline_url=latest%20command"));
  await settle();
  assert.equal(overlay.children[0].textContent, "C\nPhase: executing\nlatest command");
  tooltip.update(tick("c", { label: "C" }), layout());
  await settle();
  assert.equal(requests.length, 2);
});

test("stale rejected RPCs do not replace the latest loading view or report renderer errors", async t => {
  const { tooltip, overlay, requests, errors } = fixture(t);
  tooltip.update(tick("a"), layout());
  await settle();
  tooltip.update(tick("b", { label: "latest" }), layout());
  const loading = overlay.children[0];
  requests[0].reject(new Error("obsolete failure"));
  await settle();
  assert.deepEqual(requests.map(request => request.id), ["a", "b"]);
  assert.equal(overlay.children[0], loading);
  assert.deepEqual(errors, []);
  requests[1].resolve(details());
  await settle();
  assert.match(overlay.children[0].textContent, /echo hello/);
});

for (const action of ["hide", "disable", "dispose"]) {
  for (const outcome of ["resolve", "reject"]) {
    test(`${action} during pending ${outcome} prevents stale UI and any coalesced next fetch`, async t => {
      const { tooltip, overlay, requests, errors } = fixture(t);
      tooltip.update(tick("a"), layout());
      await settle();
      tooltip.update(tick("b"), layout());
      if (action === "dispose") tooltip.dispose();
      else tooltip.update(action === "hide" ? null : tick("b"), layout(), action === "disable" ? false : undefined);
      requests[0][outcome](outcome === "resolve" ? details() : new Error("late failure"));
      await settle();
      assert.deepEqual(requests.map(request => request.id), ["a"]);
      assert.deepEqual(overlay.children, []);
      assert.deepEqual(errors, []);
    });
  }
}

test("switching from an in-flight command to a custom marker never fetches custom details", async t => {
  const { tooltip, overlay, requests } = fixture(t);
  tooltip.update(tick("command"), layout());
  await settle();
  tooltip.update(tick("bookmark", { source: "custom", label: "local note" }), layout());
  const bookmark = overlay.children[0];
  requests[0].resolve(details());
  await settle();
  assert.deepEqual(requests.map(request => request.id), ["command"]);
  assert.equal(overlay.children[0], bookmark);
  assert.equal(bookmark.textContent, "local note");
});

test("command error state replaces loading and is retained for layout changes without refetch", async t => {
  const { tooltip, overlay, requests, errors } = fixture(t);
  tooltip.update(tick(), layout());
  await settle();
  requests[0].reject(new Error("permission denied <b>"));
  await settle();
  assert.equal(overlay.children[0].textContent,
    "Terminal command\nPhase: executing\nCommand details unavailable: permission denied <b>");
  tooltip.update(tick(), layout(400));
  await settle();
  assert.equal(requests.length, 1);
  assert.match(overlay.children[0].textContent, /permission denied <b>/);
  assert.deepEqual(errors, []);
});

test("non-Error RPC rejection is exposed in callback state without a renderer error", async t => {
  const { tooltip, requests, errors } = fixture(t);
  const seen = [];
  tooltip.update(tick(), layout(), value => { seen.push(value); return null; });
  await settle();
  requests[0].reject("transport offline");
  await settle();
  assert.equal(seen.length, 2);
  assert.equal(seen[1].loading, false);
  assert.equal(seen[1].details, null);
  assert.equal(seen[1].error, "transport offline");
  assert.deepEqual(errors, []);
});

test("phase and exit-code updates refresh command details even for the same marker id", async t => {
  const { tooltip, overlay, requests } = fixture(t);
  tooltip.update(tick("a"), layout());
  await settle();
  requests[0].resolve(details("cmdline_url=running"));
  await settle();
  tooltip.update(tick("a", { phase: "finished", exitCode: 0 }), layout());
  assert.match(overlay.children[0].textContent, /Loading command details/);
  assert.doesNotMatch(overlay.children[0].textContent, /running/);
  await settle();
  assert.deepEqual(requests.map(request => request.id), ["a", "a"]);
  requests[1].resolve(details("cmdline_url=finished"));
  await settle();
  assert.equal(overlay.children[0].textContent,
    "Terminal command\nPhase: finished\nExit code: 0\nfinished");
  tooltip.update(tick("a", { phase: "finished", exitCode: 1 }), layout());
  await settle();
  assert.deepEqual(requests.map(request => request.id), ["a", "a", "a"]);
  requests[2].resolve(details("cmdline_url=corrected", { exitCode: 1 }));
  await settle();
  assert.match(overlay.children[0].textContent, /Exit code: 1\ncorrected$/);
});

test("phase changes during an in-flight request discard old details and fetch the latest phase", async t => {
  const { tooltip, overlay, requests } = fixture(t);
  tooltip.update(tick(), layout());
  await settle();
  tooltip.update(tick("a", { phase: "finished", exitCode: 9 }), layout());
  const latest = overlay.children[0];
  requests[0].resolve(details("cmdline_url=stale"));
  await settle();
  assert.equal(overlay.children[0], latest);
  assert.deepEqual(requests.map(request => request.id), ["a", "a"]);
  requests[1].resolve(details("cmdline_url=final"));
  await settle();
  assert.match(overlay.children[0].textContent, /Exit code: 9\nfinal$/);
});

for (const [name, invalid] of [
  ["undefined", () => undefined],
  ["string", () => "<div>bad</div>"],
  ["plain object", () => ({ style: {} })],
  ["resolved Promise", () => Promise.resolve(null)],
  ["rejected Promise", () => Promise.reject(new Error("async failure"))],
  ["async function", async () => null],
  ["throwing function", () => { throw new Error("renderer failure"); }]
]) {
  test(`${name} renderer is isolated, cleaned up and does not start command RPCs`, async t => {
    const { tooltip, overlay, requests, errors, observers } = fixture(t);
    let signal;
    const renderer = value => { signal = value.signal; return invalid(); };
    assert.doesNotThrow(() => tooltip.update(tick(), layout(), renderer));
    await settle();
    assert.equal(errors.length, 1);
    assert.match(errors[0].message, /synchronously return an HTMLElement or null|renderer failure/);
    assert.equal(signal.aborted, true);
    assert.deepEqual(overlay.children, []);
    assert.equal(observers.length, 0);
    assert.equal(requests.length, 0);
    tooltip.update(tick(), layout(400), renderer);
    assert.equal(errors.length, 1, "a failed rendering is not retried for layout-only changes");
    tooltip.update(tick("recovered", { source: "custom", label: "recovered" }), layout());
    assert.equal(overlay.children[0].textContent, "recovered");
  });
}

for (const action of ["hide", "dispose", "replace"]) {
  test(`renderer can reentrantly ${action} without mounting its obsolete return value`, async t => {
    const { tooltip, overlay, Element, requests, errors } = fixture(t);
    const obsolete = new Element();
    let signal;
    tooltip.update(tick(), layout(), value => {
      signal = value.signal;
      if (action === "dispose") tooltip.dispose();
      else if (action === "hide") tooltip.update(null, layout());
      else tooltip.update(tick("replacement", { source: "custom", label: "replacement" }), layout());
      return obsolete;
    });
    await settle();
    assert.equal(signal.aborted, true);
    assert.equal(obsolete.parentElement, null);
    assert.equal(requests.length, 0);
    assert.deepEqual(errors, []);
    if (action === "replace") assert.equal(overlay.children[0].textContent, "replacement");
    else assert.deepEqual(overlay.children, []);
  });
}

test("host cleanup can reentrantly cancel a replacement before its callback or fetch runs", async t => {
  const { tooltip, overlay, requests } = fixture(t);
  tooltip.update(tick("custom", { source: "custom" }), layout(), value => {
    value.signal.addEventListener("abort", () => tooltip.update(null, layout()), { once: true });
    return null;
  });
  let callbacks = 0;
  tooltip.update(tick(), layout(), () => { callbacks++; return null; });
  await settle();
  assert.equal(callbacks, 0);
  assert.equal(requests.length, 0);
  assert.deepEqual(overlay.children, []);
});

for (const action of ["hide", "dispose", "replace"]) {
  test(`${action} before the queued RPC starts skips obsolete work`, async t => {
    const { tooltip, overlay, requests, errors } = fixture(t);
    tooltip.update(tick("obsolete"), layout());
    if (action === "dispose") tooltip.dispose();
    else if (action === "hide") tooltip.update(null, layout());
    else tooltip.update(tick("latest", { label: "latest" }), layout());
    assert.equal(requests.length, 0, "detail retrieval is deferred");
    await settle();
    if (action === "replace") {
      assert.deepEqual(requests.map(request => request.id), ["latest"]);
      requests[0].resolve(details("cmdline_url=current"));
      await settle();
      assert.equal(overlay.children[0].textContent, "latest\nPhase: executing\ncurrent");
    } else {
      assert.deepEqual(requests, []);
      assert.deepEqual(overlay.children, []);
    }
    assert.deepEqual(errors, []);
  });
}

for (const action of ["hide", "dispose"]) {
  for (const kind of ["Promise", "thenable"]) {
    test(`reentrant ${action} still observes a rejected renderer ${kind}`, async t => {
      const { tooltip, overlay, requests, errors } = fixture(t);
      let observed = 0;
      let signal;
      tooltip.update(tick(), layout(), value => {
        signal = value.signal;
        if (action === "dispose") tooltip.dispose();
        else tooltip.update(null, layout());
        const error = new Error("obsolete asynchronous renderer");
        return kind === "Promise" ? Promise.reject(error) : {
          then(_resolve, reject) { observed++; reject(error); }
        };
      });
      await settle();
      if (kind === "thenable") assert.equal(observed, 1);
      assert.equal(signal.aborted, true);
      assert.deepEqual(overlay.children, []);
      assert.deepEqual(requests, []);
      assert.deepEqual(errors, []);
    });
  }
}
