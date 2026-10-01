import assert from "node:assert/strict";
import { test } from "node:test";
import {
  normalizeScrollbar, scrollbarGeometry, scrollbarMarkerAt, scrollbarOpacity,
  renderDefaultScrollbar, ScrollbarController
} from "../.build/scrollbar.js";

const layout = {
  width: 220, height: 240,
  content: { left: 10, top: 20, width: 200, height: 200 },
  scrollbar: { left: 198, top: 20, width: 12, height: 200 },
  padding: { top: 20, right: 10, bottom: 20, left: 10 },
  cellWidth: 10, cellHeight: 10
};
const viewport = {
  available: true, generation: "1", buffer: "main", totalRows: 100, liveTop: 80, top: 40,
  requestId: 1, rowIds: ["41", "42"], revision: 1,
  following: false, pending: false, followTail: true, offset: 40
};
const marker = (id, row, extra = {}) => ({
  id, row, column: 0, buffer: "main", source: "custom", ...extra
});

test("normalizeScrollbar freezes defaults and rejects invalid runtime configuration", () => {
  assert.equal(normalizeScrollbar(false), false);
  assert.deepEqual(normalizeScrollbar(), {
    placement: "overlay", width: 12, proximity: 24, hideDelay: 900, fadeDuration: 300, markers: true
  });
  assert.ok(Object.isFrozen(normalizeScrollbar({ placement: "beside" })));
  for (const input of [null, true, [], "overlay", { placement: "left" }, { render: true },
    { markers: "false" }, { width: 3 }, { width: 65 }, { width: NaN }, { width: Infinity },
    { width: null }, { proximity: -1 }, { hideDelay: -1 }, { fadeDuration: Infinity },
    { render: async () => {} }, { tooltip: true }, { tooltip: null }, { tooltip: async () => null }])
    assert.throws(() => normalizeScrollbar(input));
  assert.equal(normalizeScrollbar({ fadeDuration: 0 }).fadeDuration, 0);
});

test("geometry accounts for content offsets, minimum thumb size, endpoints and tiny tracks", () => {
  const geometry = scrollbarGeometry(layout, viewport, []);
  assert.deepEqual(geometry.track, layout.scrollbar);
  assert.deepEqual(geometry.thumb, { left: 198, top: 100, width: 12, height: 40 });
  assert.equal(scrollbarGeometry(layout, viewport, [], -100).thumb.top, 20);
  assert.equal(scrollbarGeometry(layout, viewport, [], 1000).thumb.top, 180);
  assert.equal(scrollbarGeometry(layout, { ...viewport, totalRows: 100000, liveTop: 99980 }, []).thumb.height, 24);
  assert.equal(scrollbarGeometry({
    ...layout, scrollbar: { ...layout.scrollbar, height: 8 }
  }, viewport, []).thumb.height, 8);
  for (const value of [{ ...viewport, buffer: "alternate" }, { available: false },
    { ...viewport, liveTop: 0 }]) assert.equal(scrollbarGeometry(layout, value, []), null);
  assert.equal(scrollbarGeometry({ ...layout, scrollbar: null }, viewport, []), null);
  assert.equal(scrollbarGeometry({ ...layout, scrollbar: { ...layout.scrollbar, height: 0 } }, viewport, []), null);
});

test("marker geometry excludes unavailable and prompt marks and resolves overlap deterministically", () => {
  const markers = [
    marker("z", 50), marker("a", 50), marker("near", 52), marker("missing", null),
    marker("outside", 101), marker("negative", -1), marker("alt", 50, { buffer: "alternate" }),
    marker("prompt", 50, { source: "command", phase: "prompt" }),
    marker("start", 20, { source: "command", phase: "executing" }),
    marker("fallback", 10, { source: "command", phase: "commandLine" })
  ];
  const geometry = scrollbarGeometry(layout, viewport, markers);
  assert.deepEqual(geometry.markers.map(tick => tick.marker.id), ["z", "a", "near", "start", "fallback"]);
  const center = geometry.markers[0].bounds.top + 2.5;
  assert.equal(scrollbarMarkerAt(geometry.markers, 204, center, geometry.track).id, "a");
  assert.equal(scrollbarMarkerAt([...geometry.markers].reverse(), 204, center, geometry.track).id, "a");
  assert.equal(scrollbarMarkerAt(geometry.markers, 150, center), undefined);
  assert.ok(Object.isFrozen(geometry.markers));
  assert.ok(Object.isFrozen(geometry.markers[0].marker));
});

test("marks move sideways around either end of the thumb without stretching or moving vertically", () => {
  const marks = [marker("approach", 10)];
  const circle = scrollbarGeometry(layout, viewport, marks).markers[0].bounds;
  assert.deepEqual(circle, { left: 201.5, top: 20 + 195 * 10 / 99, width: 5, height: 5 });
  for (const below of [false, true]) {
    const row = 50;
    const top = 20 + 195 * row / 99;
    const targetForDistance = distance => (below ? top + 5 + distance - 20 : top - distance - 40 - 20) / 2;
    let previousLeft = Infinity;
    for (const distance of [24, 12, 8, 6, 4, 2, 0]) {
      const tick = scrollbarGeometry(layout, viewport, [marker("approach", row)],
        targetForDistance(distance)).markers[0].bounds;
      assert.equal(tick.top, top);
      assert.equal(tick.height, 5);
      assert.equal(tick.width, 5);
      assert.ok(tick.left <= previousLeft);
      assert.ok(tick.left >= 191);
      if (distance >= 8) assert.ok(Math.abs(tick.left - 201.5) < 1e-10);
      if (distance === 4) assert.ok(Math.abs(tick.left - (201.5 + 191) / 2) < 1e-10);
      if (distance === 0) assert.equal(tick.left, 191);
      previousLeft = tick.left;
      assert.ok(Object.isFrozen(tick));
    }
    const departed = scrollbarGeometry(layout, viewport, [marker("approach", row)],
      targetForDistance(24)).markers[0].bounds;
    assert.equal(departed.left, 201.5);
  }
  const overlap = scrollbarGeometry(layout, viewport, marks, 0).markers[0].bounds;
  assert.equal(overlap.left, 191);
});

test("circle geometry is bounded for narrow tracks, short surfaces and history endpoints", () => {
  for (const width of [0.2, 1, 4, 12, 64])
    for (const height of [0.1, 1, 8, 200])
      for (const left of [0, 2, 20]) {
        const small = {
          ...layout, width: left + width, height,
          scrollbar: { left, top: 0, width, height }
        };
        const marks = [marker("first", 0), marker("middle", 50), marker("last", 99)];
        for (const target of [0, 40, 80]) {
          const geometry = scrollbarGeometry(small, viewport, marks, target);
          const paintedThumbWidth = width - Math.min(2, width / 4) * 2;
          for (const { bounds } of geometry.markers) {
            assert.ok(Object.values(bounds).every(Number.isFinite));
            assert.ok(bounds.height > 0 && bounds.height < paintedThumbWidth);
            assert.ok(bounds.height <= 5 && bounds.width === bounds.height);
            assert.ok(bounds.left >= 0 && bounds.left + bounds.width <= small.width + 1e-12);
            assert.ok(bounds.top >= 0 && bounds.top + bounds.height <= height + 1e-12);
          }
          assert.equal(geometry.markers[0].bounds.top, 0);
          const last = geometry.markers[2].bounds;
          assert.ok(Math.abs(last.top + last.height - height) < 1e-12);
        }
      }
});

test("circle hit testing excludes corners and the former capsule corridor outside the forgiving track", () => {
  const geometry = scrollbarGeometry(layout, viewport, [marker("near", 50)]);
  const { bounds } = geometry.markers[0];
  assert.equal(scrollbarMarkerAt(geometry.markers, 193.5, bounds.top + 2.5, geometry.track).id, "near");
  assert.equal(scrollbarMarkerAt(geometry.markers, bounds.left + 0.1, bounds.top + 0.1, geometry.track), undefined);
  for (const x of [188, 196.5, 197.5])
    assert.equal(scrollbarMarkerAt(geometry.markers, x, bounds.top + 2.5, geometry.track), undefined);
  assert.equal(scrollbarMarkerAt(geometry.markers, 193.5, bounds.top - 1, geometry.track), undefined);
  assert.equal(scrollbarMarkerAt(geometry.markers, 209, bounds.top - 1, geometry.track).id, "near");
});

test("extreme scrollback maps exact row fractions across the whole track independent of the thumb", () => {
  for (const totalRows of [1000, 1_000_000, 2_147_483_647]) {
    const liveTop = totalRows - 24;
    const rows = [0, 1, Math.floor((totalRows - 1) / 4), Math.floor((totalRows - 1) / 2),
      totalRows - 25, totalRows - 1];
    const marks = rows.map((row, i) => marker(`mark-${i}`, row));
    for (const target of [0, Math.floor(liveTop / 2), liveTop]) {
      const geometry = scrollbarGeometry(layout, { ...viewport, totalRows, liveTop }, marks, target);
      for (let i = 0; i < rows.length; i++) {
        const { bounds } = geometry.markers[i];
        assert.equal(bounds.top, 20 + 195 * rows[i] / (totalRows - 1));
        assert.equal(bounds.width, 5);
        assert.equal(bounds.height, 5);
        assert.ok(Object.values(bounds).every(Number.isFinite));
        if (i) assert.ok(bounds.top > geometry.markers[i - 1].bounds.top);
      }
      assert.equal(geometry.markers[0].bounds.top, 20);
      assert.equal(geometry.markers.at(-1).bounds.top + 5, 220);
      assert.ok(Math.abs(geometry.markers[2].bounds.top - 68.75) < 0.2);
      assert.ok(Math.abs(geometry.markers[3].bounds.top - 117.5) < 0.2);
    }
  }
});

test("fade timing is deterministic and reduced motion removes animation but preserves delay", () => {
  const configuration = normalizeScrollbar();
  assert.equal(scrollbarOpacity(899, 0, false, configuration, false), 1);
  assert.equal(scrollbarOpacity(900, 0, false, configuration, false), 1);
  assert.equal(scrollbarOpacity(1050, 0, false, configuration, false), 0.5);
  assert.equal(scrollbarOpacity(1200, 0, false, configuration, false), 0);
  assert.equal(scrollbarOpacity(5000, 0, true, configuration, false), 1);
  assert.equal(scrollbarOpacity(899, 0, false, configuration, true), 1);
  assert.equal(scrollbarOpacity(900, 0, false, configuration, true), 0);
  assert.equal(scrollbarOpacity(900, 0, false, normalizeScrollbar({ fadeDuration: 0 }), false), 0);
});

test("beside opacity stays visible regardless of inactivity, fade timings or reduced motion", () => {
  for (const reducedMotion of [false, true])
    for (const hideDelay of [0, 900])
      for (const fadeDuration of [0, 300])
        for (const now of [0, 900, 1200, 1_000_000])
          assert.equal(scrollbarOpacity(now, 0, false,
            normalizeScrollbar({ placement: "beside", hideDelay, fadeDuration }), reducedMotion), 1);
});

class Element extends EventTarget {
  attributes = new Map();
  style = {};
  captures = new Set();
  hidden = false;
  title = "";
  tabIndex = -1;
  bounds = { left: 100, top: 50, width: 110, height: 120 };
  setAttribute(name, value) { this.attributes.set(name, value); }
  getAttribute(name) { return this.attributes.get(name); }
  getBoundingClientRect() { return this.bounds; }
  setPointerCapture(id) { this.captures.add(id); }
  hasPointerCapture(id) { return this.captures.has(id); }
  releasePointerCapture(id) { this.captures.delete(id); }
  focus() { this.dispatchEvent(new Event("focus")); }
}

function context() {
  return {
    globalAlpha: 1, fillStyle: "", strokeStyle: "", lineWidth: 1,
    stack: [], rectangles: [], fills: [], outlines: [], clears: [], transforms: [], path: [],
    save() { this.stack.push({
      globalAlpha: this.globalAlpha, fillStyle: this.fillStyle, strokeStyle: this.strokeStyle, lineWidth: this.lineWidth
    }); },
    restore() { Object.assign(this, this.stack.pop()); },
    setTransform(...args) { this.transforms.push(args); },
    clearRect(...args) { this.clears.push(args); },
    createLinearGradient(...coordinates) {
      return { coordinates, stops: [], addColorStop(...stop) { this.stops.push(stop); } };
    },
    fillRect(...args) { this.rectangles.push({ args, color: this.fillStyle, alpha: this.globalAlpha }); },
    beginPath() { this.path = []; },
    rect(...args) { this.path.push(args); },
    moveTo(...args) { this.path.push(["moveTo", ...args]); },
    lineTo(...args) { this.path.push(["lineTo", ...args]); },
    quadraticCurveTo(...args) { this.path.push(["quadraticCurveTo", ...args]); },
    closePath() { this.path.push(["closePath"]); },
    roundRect(...args) { this.path.push(args); },
    fill() { this.fills.push({ path: this.path, color: this.fillStyle, alpha: this.globalAlpha }); },
    stroke() { this.outlines.push(this.path); },
    strokeRect(...args) { this.outlines.push(args); }
  };
}

function harness(t, options = {}, handlers = {}) {
  let now = 0, id = 0;
  const frames = new Map(), timers = new Map(), ctx = context();
  const reducedMotion = Object.assign(new EventTarget(), { matches: false });
  const globals = {
    performance: { now: () => now },
    requestAnimationFrame: callback => { const key = ++id; frames.set(key, callback); return key; },
    cancelAnimationFrame: key => frames.delete(key),
    setTimeout: (callback, delay) => { const key = ++id; timers.set(key, { callback, at: now + delay }); return key; },
    clearTimeout: key => timers.delete(key),
    devicePixelRatio: 5,
    getComputedStyle: () => ({ getPropertyValue: name => ({
      "--cp-view-surface": "#111111", "--cp-view-text-muted": "#999999",
      "--cp-view-accent": "#0088ff", "--cp-view-danger": "#ff0000"
    })[name] ?? "" }),
    matchMedia: () => reducedMotion
  };
  const saved = new Map(Object.keys(globals).map(key => [key, Object.getOwnPropertyDescriptor(globalThis, key)]));
  for (const [key, value] of Object.entries(globals))
    Object.defineProperty(globalThis, key, { configurable: true, writable: true, value });
  const element = new Element(), accessibility = new Element(), canvas = new Element();
  canvas.width = canvas.height = 0;
  canvas.contextRequests = 0;
  canvas.getContext = () => { canvas.contextRequests++; return ctx; };
  const state = {
    connected: true, layout, viewport: { ...viewport }, markers: [], configuration: normalizeScrollbar(),
    ...options
  };
  const rows = [], jumps = [], errors = [], hovers = [];
  let lives = 0;
  const controller = new ScrollbarController({
    element, canvas, accessibility, getState: () => state,
    scrollToRow: row => rows.push(row),
    scrollToLive: () => { lives++; },
    scrollToMarker: handlers.scrollToMarker ?? (async id => { jumps.push(id); }),
    reportError: error => errors.push(error),
    onMarkerHover: marker => hovers.push(marker)
  });
  t.after(() => {
    controller.dispose();
    for (const [key, descriptor] of saved) {
      if (descriptor) Object.defineProperty(globalThis, key, descriptor);
      else delete globalThis[key];
    }
  });
  function emit(type, properties = {}, target = element) {
    const event = new Event(type, { cancelable: true });
    Object.assign(event, {
      pointerId: 1, button: 0, buttons: 0, clientX: 202, clientY: 110,
      deltaX: 0, deltaY: 1, deltaMode: 1, ...properties
    });
    target.dispatchEvent(event);
    return event;
  }
  function flush() {
    const pending = [...frames.values()];
    frames.clear();
    for (const callback of pending) callback(now);
  }
  function advance(time) {
    now = time;
    for (const [key, timer] of [...timers]) {
      if (timer.at <= now) { timers.delete(key); timer.callback(); }
    }
    flush();
  }
  return {
    controller, state, element, canvas, accessibility, ctx, rows, jumps, errors, hovers,
    frames, timers, emit, flush, advance, reducedMotion, get lives() { return lives; }
  };
}

test("controller paints a separate DPR-capped canvas using immutable CSS-coordinate snapshots and theme tokens", t => {
  let snapshot;
  const h = harness(t, { configuration: normalizeScrollbar({ render: frame => {
    snapshot = frame;
    assert.equal(frame.context.globalAlpha, 1);
    frame.context.globalAlpha = 0.25;
    renderDefaultScrollbar(frame);
  } }) });
  h.flush();
  assert.equal(h.canvas.width, 660);
  assert.equal(h.canvas.height, 720);
  assert.deepEqual(snapshot.track, layout.scrollbar);
  assert.deepEqual(snapshot.colors, { track: "#202020", thumb: "#999999", marker: "#aaaaaa", error: "#eeeeee" });
  assert.equal(snapshot.pendingTarget, null);
  assert.ok(Object.isFrozen(snapshot.layout.content));
  assert.ok(Object.isFrozen(snapshot.viewport.rowIds));
  assert.equal(h.ctx.globalAlpha, 1);
  assert.equal(h.ctx.stack.length, 0);
  assert.ok(h.ctx.transforms.some(transform => transform[0] === 3 && transform[3] === 3));
  h.controller.activity();
  h.flush();
  assert.equal(h.ctx.stack.length, 0);
});

test("disconnected construction and unavailable history do not access Canvas2D until usable", t => {
  const h = harness(t, { connected: false });
  h.flush();
  assert.equal(h.canvas.contextRequests, 0);
  assert.equal(h.accessibility.hidden, true);
  h.state.connected = true;
  h.state.viewport = { available: false };
  h.controller.refresh();
  h.flush();
  assert.equal(h.canvas.contextRequests, 0);
  h.state.viewport = viewport;
  h.controller.refresh();
  h.flush();
  assert.equal(h.canvas.contextRequests, 1);
});

test("controller uses a single delay timer, animates the fade, then stops idle and wakes on invisible proximity", t => {
  const h = harness(t);
  h.flush();
  assert.equal(h.frames.size, 0);
  assert.equal(h.timers.size, 1);
  h.advance(900);
  assert.equal(h.frames.size, 1);
  h.advance(1050);
  assert.equal(h.ctx.fills.at(-1).alpha, 0.5);
  h.advance(1200);
  assert.equal(h.frames.size, 0);
  assert.equal(h.timers.size, 0);
  h.emit("pointermove", { clientX: 193 }); // x=186, in proximity, outside track.
  h.flush();
  assert.equal(h.ctx.fills.at(-1).alpha, 1);
  assert.equal(h.frames.size, 0);
  assert.equal(h.timers.size, 0);
  h.emit("pointerleave");
  h.flush();
  assert.equal(h.timers.size, 1);
});

test("reduced motion hides at the delay boundary without an animation loop", t => {
  const h = harness(t);
  h.reducedMotion.matches = true;
  h.flush();
  h.advance(900);
  assert.equal(h.frames.size, 0);
  assert.equal(h.timers.size, 0);
});

test("beside remains painted without idle timers or animation loops and still updates on output", t => {
  const h = harness(t, { configuration: normalizeScrollbar({ placement: "beside" }) });
  h.flush();
  const before = h.ctx.fills.at(-1).path;
  assert.equal(h.ctx.fills.at(-1).alpha, 1);
  for (const time of [900, 1200, 10000]) {
    h.advance(time);
    assert.equal(h.ctx.fills.at(-1).alpha, 1);
    assert.equal(h.frames.size, 0);
    assert.equal(h.timers.size, 0);
  }
  h.state.viewport = { ...viewport, top: 0 };
  h.controller.refresh();
  h.advance(10120);
  assert.notDeepEqual(h.ctx.fills.at(-1).path, before);
  assert.equal(h.ctx.fills.at(-1).alpha, 1);
  assert.equal(h.frames.size, 0);
  assert.equal(h.timers.size, 0);
});

test("placement changes cancel overlay fading and restore it only when returning to overlay", t => {
  const opacities = [];
  const render = frame => { opacities.push(frame.opacity); renderDefaultScrollbar(frame); };
  const h = harness(t, { configuration: normalizeScrollbar({ render }) });
  h.flush();
  assert.equal(h.timers.size, 1);
  h.state.configuration = normalizeScrollbar({ placement: "beside", render });
  h.controller.refresh();
  h.flush();
  assert.equal(h.timers.size, 0);
  h.advance(2000);
  assert.equal(h.ctx.fills.at(-1).alpha, 1);
  assert.equal(h.frames.size, 0);
  h.state.configuration = normalizeScrollbar({ placement: "overlay", render });
  h.controller.refresh();
  h.flush();
  assert.equal(h.timers.size, 1);
  h.advance(3200);
  assert.equal(h.frames.size, 0);
  assert.equal(h.timers.size, 0);
  assert.equal(opacities.at(-1), 0);
  assert.ok(h.ctx.clears.length > 0);
});

test("fully faded painters stay dormant through terminal output and geometry refresh until activity", t => {
  const snapshots = [];
  const h = harness(t, { configuration: normalizeScrollbar({ render: frame => {
    snapshots.push(frame);
    renderDefaultScrollbar(frame);
  } }) });
  h.flush();
  h.advance(1200);
  assert.equal(snapshots.at(-1).opacity, 0);
  const calls = snapshots.length;
  for (let revision = 2; revision <= 5; revision++) {
    h.state.viewport = { ...viewport, revision, liveTop: 80 + revision, totalRows: 100 + revision };
    h.controller.refresh();
    h.flush();
  }
  h.state.layout = { ...layout, width: 300 };
  h.controller.refresh();
  h.flush();
  assert.equal(h.frames.size, 0);
  assert.equal(snapshots.length, calls);
  assert.equal(h.accessibility.getAttribute("aria-valuemax"), "85");
  h.controller.activity();
  h.flush();
  assert.equal(snapshots.length, calls + 1);
  assert.equal(snapshots.at(-1).layout.width, 300);
  assert.equal(snapshots.at(-1).opacity, 1);
});

test("custom painters can request frames, and faults release gestures and disable only that configuration", t => {
  let calls = 0, shouldThrow = false;
  const h = harness(t, { configuration: normalizeScrollbar({
    render() { calls++; if (shouldThrow) throw new Error("painter"); return true; }
  }) });
  h.flush();
  assert.equal(calls, 1);
  assert.equal(h.frames.size, 1);
  h.emit("pointerdown", { buttons: 1 });
  assert.equal(h.element.captures.size, 1);
  shouldThrow = true;
  h.flush();
  assert.equal(h.errors.length, 1);
  assert.equal(h.element.captures.size, 0);
  assert.equal(h.ctx.stack.length, 0);
  h.controller.refresh();
  h.flush();
  assert.equal(calls, 2);
  assert.equal(h.frames.size, 0);
  h.state.configuration = normalizeScrollbar({ render() { calls++; } });
  h.controller.refresh();
  h.flush();
  assert.equal(calls, 3);
});

test("explicit invalidation repaints a dormant custom painter without fabricating activity", t => {
  const snapshots = [];
  const h = harness(t, { configuration: normalizeScrollbar({
    render: frame => { snapshots.push(frame); }
  }) });
  h.flush();
  h.advance(1200);
  const calls = snapshots.length;
  h.controller.invalidate();
  h.flush();
  assert.equal(snapshots.length, calls + 1);
  assert.equal(snapshots.at(-1).opacity, 0);
  assert.equal(h.frames.size, 0);
  assert.equal(h.timers.size, 0);
});

test("missing Canvas2D context reports a local error without retrying every frame", t => {
  const h = harness(t);
  h.canvas.getContext = () => null;
  h.flush();
  assert.equal(h.errors.length, 1);
  assert.match(h.errors[0].message, /Canvas2D context is unavailable/);
  h.controller.invalidate();
  h.flush();
  assert.equal(h.errors.length, 1);
  assert.equal(h.frames.size, 0);
});

test("promise-returning painters report invalid returns and observe rejected promises", async t => {
  const h = harness(t, { configuration: normalizeScrollbar({
    render: () => Promise.reject(new Error("asynchronous failure"))
  }) });
  h.flush();
  await Promise.resolve();
  assert.deepEqual(h.errors.map(error => error.message), [
    "scrollbar.render must synchronously return boolean or undefined", "asynchronous failure"
  ]);
  assert.equal(h.ctx.stack.length, 0);
  assert.equal(h.frames.size, 0);
});

test("marker navigation rejected after disposal does not call the host error handler", async t => {
  const pending = Promise.withResolvers();
  const h = harness(t, { markers: [marker("earlier", 10)] }, {
    scrollToMarker: () => pending.promise
  });
  h.emit("keydown", { key: "ArrowUp", altKey: true }, h.accessibility);
  h.controller.dispose();
  pending.reject(new Error("Terminal disconnected before the marker operation completed."));
  await Promise.resolve();
  assert.deepEqual(h.errors, []);
});

test("reentrant painter disposal does not recreate fade timers", t => {
  const h = harness(t, { configuration: normalizeScrollbar({
    render: () => h.controller.dispose()
  }) });
  h.flush();
  assert.equal(h.frames.size, 0);
  assert.equal(h.timers.size, 0);
});

test("the thumb remains draggable when a marker overlaps it", t => {
  const h = harness(t, { markers: [marker("overlap", 50)] });
  h.flush();
  h.emit("pointerdown", { buttons: 1 });
  h.emit("pointermove", { buttons: 1, clientY: 130 });
  h.flush();
  assert.deepEqual(h.jumps, []);
  assert.deepEqual(h.rows, [60]);
});

test("displaced circles support hover, clicks and compatibility events without paging", t => {
  const h = harness(t, { markers: [marker("z", 50), marker("a", 50)] });
  h.flush();
  const clientY = 50 + (20 + 195 * 50 / 99 + 2.5) / 2;
  const wing = { clientX: 196.75, clientY }; // local x=193.5, the displaced circle's center.
  assert.equal(h.emit("pointermove", wing).defaultPrevented, true);
  assert.equal(h.hovers.at(-1).marker.id, "a");
  assert.equal(h.hovers.at(-1).bounds.left, 191);
  for (const type of ["pointerdown", "mousedown", "pointerup", "mouseup", "click", "dblclick", "contextmenu"])
    assert.equal(h.emit(type, wing).defaultPrevented, true, type);
  h.flush();
  assert.deepEqual(h.jumps, ["a"]);
  assert.deepEqual(h.rows, []);
  assert.equal(h.lives, 0);
  assert.equal(h.element.captures.size, 0);
});

test("the thumb owns hover as well as clicks while displaced circles remain discoverable", t => {
  const h = harness(t, { markers: [marker("overlap", 50)] });
  h.flush();
  const clientY = 50 + (20 + 195 * 50 / 99 + 2.5) / 2;
  h.emit("pointermove", { clientX: 196.75, clientY });
  assert.equal(h.hovers.at(-1).marker.id, "overlap");
  h.emit("pointermove", { clientY });
  assert.equal(h.hovers.at(-1), null);
  h.emit("pointerdown", { clientY, buttons: 1 });
  h.emit("pointermove", { clientY: clientY + 10, buttons: 1 });
  h.flush();
  assert.deepEqual(h.jumps, []);
  assert.deepEqual(h.rows, [50]);
});

test("displaced circles never claim rounded corners, empty content or an existing selection", t => {
  const h = harness(t, { markers: [marker("overlap", 50)] });
  h.flush();
  const top = 20 + 195 * 50 / 99;
  const corner = { clientX: 100 + 191.1 / 2, clientY: 50 + (top + 0.1) / 2 };
  for (const type of ["pointermove", "contextmenu", "wheel", "pointerdown", "pointerup"])
    assert.equal(h.emit(type, corner).defaultPrevented, false, type);
  assert.equal(h.hovers.at(-1), null);
  const gap = { clientX: 198.5, clientY: 50 + (top + 2.5) / 2 };
  for (const type of ["pointermove", "contextmenu", "wheel", "pointerdown", "pointerup"])
    assert.equal(h.emit(type, gap).defaultPrevented, false, type);
  const wing = { clientX: 196.75, clientY: 50 + (top + 2.5) / 2 };
  assert.equal(h.emit("pointerdown", { clientX: 130, buttons: 1 }).defaultPrevented, false);
  for (const type of ["pointermove", "mousedown", "wheel", "pointerup", "mouseup", "click"])
    assert.equal(h.emit(type, { ...wing, buttons: 1 }).defaultPrevented, false, type);
  assert.deepEqual(h.jumps, []);
});

test("wheel events over a displaced circle scroll rows without navigating to the marker", t => {
  const h = harness(t, { markers: [marker("overlap", 50)] });
  h.flush();
  const wing = { clientX: 196.75, clientY: 50 + (20 + 195 * 50 / 99 + 2.5) / 2 };
  assert.equal(h.emit("wheel", { ...wing, deltaY: 2 }).defaultPrevented, true);
  h.flush();
  assert.deepEqual(h.rows, [42]);
  assert.deepEqual(h.jumps, []);
});

test("faded circles leave content alone until track proximity reveals them", t => {
  const h = harness(t, { markers: [marker("overlap", 50)] });
  h.flush();
  h.advance(1200);
  const wing = { clientX: 196.75, clientY: 50 + (20 + 195 * 50 / 99 + 2.5) / 2 };
  for (const type of ["wheel", "contextmenu", "pointerdown", "pointerup", "click"])
    assert.equal(h.emit(type, wing).defaultPrevented, false, type);
  assert.deepEqual(h.jumps, []);
  assert.equal(h.emit("pointermove", wing).defaultPrevented, true);
  h.flush();
  assert.equal(h.hovers.at(-1).marker.id, "overlap");
  assert.equal(h.emit("pointerdown", wing).defaultPrevented, true);
  assert.deepEqual(h.jumps, ["overlap"]);
});

test("circle hover keeps the scrollbar visible without proximity and clears as geometry retracts", t => {
  const snapshots = [];
  const h = harness(t, {
    markers: [marker("near", 50)],
    configuration: normalizeScrollbar({ proximity: 0, render: frame => {
      snapshots.push(frame);
      renderDefaultScrollbar(frame);
    } })
  });
  h.flush();
  const wing = { clientX: 196.75, clientY: 50 + (20 + 195 * 50 / 99 + 2.5) / 2 };
  h.emit("pointermove", wing);
  h.advance(2000);
  assert.equal(snapshots.at(-1).opacity, 1);
  assert.equal(snapshots.at(-1).interaction.hovered, true);
  assert.equal(h.timers.size, 0);
  h.state.viewport = { ...viewport, top: 0 };
  h.controller.refresh();
  h.advance(2120);
  assert.equal(h.hovers.at(-1), null);
  assert.equal(snapshots.at(-1).interaction.hovered, false);
  h.advance(3320);
  assert.equal(snapshots.at(-1).opacity, 0);
  h.state.viewport = viewport;
  h.controller.refresh();
  assert.equal(h.emit("pointermove", wing).defaultPrevented, false);
  assert.equal(h.emit("pointerdown", wing).defaultPrevented, false);
});

test("pending drag targets displace circles before the producer responds", t => {
  const snapshots = [];
  const h = harness(t, {
    markers: [marker("near-start", 10)],
    configuration: normalizeScrollbar({ render: frame => { snapshots.push(frame); } })
  });
  h.flush();
  assert.equal(snapshots.at(-1).markers[0].bounds.width, 5);
  h.emit("pointerdown", { buttons: 1 });
  h.emit("pointermove", { buttons: 1, clientY: 70 });
  h.flush();
  assert.equal(snapshots.at(-1).pendingTarget, 0);
  assert.equal(snapshots.at(-1).markers[0].bounds.left, 191);
  assert.equal(snapshots.at(-1).markers[0].bounds.width, 5);
  assert.equal(h.state.viewport.top, 40);
});

test("scaled thumb dragging coalesces targets and ignores stale server frames through release", t => {
  const snapshots = [];
  const h = harness(t, { configuration: normalizeScrollbar({ render: frame => { snapshots.push(frame); } }) });
  h.flush();
  const down = h.emit("pointerdown", { buttons: 1 }); // local (204,120), thumb offset 20.
  assert.equal(down.defaultPrevented, true);
  assert.equal(h.element.captures.has(1), true);
  h.emit("pointermove", { buttons: 1, clientY: 120 }); // local y=140 => target 50.
  h.emit("pointermove", { buttons: 1, clientY: 130 }); // local y=160 => target 60.
  assert.deepEqual(h.rows, []);
  h.flush();
  assert.deepEqual(h.rows, [60]);
  assert.equal(snapshots.at(-1).thumb.top, 140);
  h.state.viewport = { ...viewport, top: 30 };
  h.controller.refresh();
  h.flush();
  assert.equal(snapshots.at(-1).thumb.top, 140);
  h.emit("pointerup", { clientY: 130 });
  h.controller.refresh();
  h.flush();
  assert.equal(snapshots.at(-1).pendingTarget, 60);
  h.state.viewport = { ...viewport, top: 60, requestId: 2 };
  h.controller.refresh();
  h.flush();
  assert.equal(snapshots.at(-1).pendingTarget, null);
  assert.equal(h.element.captures.size, 0);
});

test("short-history dragging paints fractional positions while navigation and accessibility stay on rows", t => {
  const frames = [];
  const h = harness(t, {
    viewport: { ...viewport, totalRows: 25, liveTop: 5, top: 0 },
    markers: [marker("near", 20)],
    configuration: normalizeScrollbar({ placement: "beside", render: frame => { frames.push(frame); } })
  });
  h.flush();
  const initial = frames.at(-1);
  h.emit("pointerdown", { buttons: 1 });
  for (const offset of [0.25, 0.5, 0.75]) {
    h.emit("pointermove", { buttons: 1, clientY: 110 + offset });
    h.flush();
    const frame = frames.at(-1);
    assert.equal(frame.thumb.top, 20 + offset * 2);
    assert.equal(frame.pendingTarget, 0);
    assert.equal(frame.markers[0].bounds.top, initial.markers[0].bounds.top);
    assert.ok(frame.markers[0].bounds.left < initial.markers[0].bounds.left);
    assert.equal(h.accessibility.getAttribute("aria-valuenow"), "0");
    assert.deepEqual(h.rows, []);
  }
  h.emit("pointermove", { buttons: 1, clientY: 112.5 });
  h.flush();
  h.emit("pointermove", { buttons: 1, clientY: 113 });
  h.flush();
  assert.equal(frames.at(-1).thumb.top, 26);
  assert.deepEqual(h.rows, [1]);
  assert.equal(h.accessibility.getAttribute("aria-valuenow"), "1");
  h.state.viewport = { ...h.state.viewport, top: 1, requestId: 2 };
  h.controller.refresh();
  h.flush();
  assert.equal(frames.at(-1).thumb.top, 26, "Acknowledgement must not snap an active drag");
  h.emit("pointerup", { clientY: 113 });
  h.advance(60);
  assert.ok(frames.at(-1).thumb.top > 26 && frames.at(-1).thumb.top < 28);
  h.advance(120);
  assert.equal(frames.at(-1).thumb.top, 28);
  assert.equal(h.frames.size, 0);
  assert.deepEqual(h.rows, [1], "Settling must not send intermediate navigation requests");
});

for (const input of ["wheel", "keyboard", "viewport"]) {
  test(`${input} row steps animate thumb and circle displacement over multiple frames`, t => {
    const frames = [];
    const h = harness(t, {
      viewport: { ...viewport, totalRows: 25, liveTop: 5, top: 0 },
      markers: [marker("near", 21)],
      configuration: normalizeScrollbar({ placement: "beside", render: frame => { frames.push(frame); } })
    });
    h.flush();
    if (input === "wheel") h.emit("wheel", { deltaY: 1 });
    else if (input === "keyboard") h.emit("keydown", { key: "ArrowDown" }, h.accessibility);
    else {
      h.state.viewport = { ...h.state.viewport, top: 1 };
      h.controller.refresh();
    }
    h.flush();
    assert.equal(frames.at(-1).thumb.top, 20);
    const tops = [], lefts = [];
    for (const time of [20, 40, 60, 80, 100, 120]) {
      h.advance(time);
      const frame = frames.at(-1);
      tops.push(frame.thumb.top);
      lefts.push(frame.markers[0].bounds.left);
      assert.equal(frame.markers[0].bounds.top, frames[0].markers[0].bounds.top);
      assert.equal(frame.markers[0].bounds.width, 5);
    }
    assert.ok(tops.every((top, i) => top > (i ? tops[i - 1] : 20) && top <= 28));
    assert.ok(lefts[0] < frames[0].markers[0].bounds.left);
    assert.ok(lefts[0] > lefts.at(-1), "Marks must visibly move at intermediate positions");
    assert.equal(tops.at(-1), 28);
    assert.equal(h.frames.size, 0);
    assert.equal(h.timers.size, 0);
    assert.deepEqual(h.rows, input === "viewport" ? [] : [1]);
  });
}

test("reversing motion starts at the displayed position and hit testing follows animated circles", t => {
  const frames = [];
  const h = harness(t, {
    viewport: { ...viewport, totalRows: 25, liveTop: 5, top: 0 },
    markers: [marker("near", 21)],
    configuration: normalizeScrollbar({ placement: "beside", render: frame => { frames.push(frame); } })
  });
  h.flush();
  h.emit("wheel", { deltaY: 1 });
  h.advance(60);
  const frame = frames.at(-1), bounds = frame.markers[0].bounds;
  assert.ok(bounds.left < frame.track.left);
  h.emit("pointermove", {
    clientX: 100 + (bounds.left + bounds.width / 4) / 2,
    clientY: 50 + (bounds.top + bounds.height / 2) / 2
  });
  assert.equal(h.hovers.at(-1).marker.id, "near");
  h.emit("wheel", { deltaY: -1 });
  h.flush();
  assert.equal(frames.at(-1).thumb.top, frame.thumb.top);
  h.advance(80);
  assert.ok(frames.at(-1).thumb.top < frame.thumb.top && frames.at(-1).thumb.top > 20);
  h.advance(180);
  assert.equal(frames.at(-1).thumb.top, 20);
  assert.equal(h.hovers.at(-1), null, "Stationary pointer must not retain hover on a receded circle");
  assert.deepEqual(h.rows, [1, 0]);
  assert.equal(h.frames.size, 0);
});

test("reduced motion snaps automatic transitions but preserves direct fractional dragging", t => {
  const frames = [];
  const h = harness(t, {
    viewport: { ...viewport, totalRows: 25, liveTop: 5, top: 0 },
    configuration: normalizeScrollbar({ placement: "beside", render: frame => { frames.push(frame); } })
  });
  h.flush();
  h.emit("wheel", { deltaY: 1 });
  h.advance(20);
  assert.ok(frames.at(-1).thumb.top > 20 && frames.at(-1).thumb.top < 28);
  h.reducedMotion.matches = true;
  h.reducedMotion.dispatchEvent(new Event("change"));
  h.flush();
  assert.equal(frames.at(-1).thumb.top, 28);
  assert.equal(h.frames.size, 0);
  h.emit("pointerdown", { buttons: 1 });
  h.emit("pointermove", { buttons: 1, clientY: 110.25 });
  h.flush();
  assert.equal(frames.at(-1).thumb.top, 28.5);
  h.emit("pointerup");
  h.flush();
  assert.equal(frames.at(-1).thumb.top, 28);
  assert.equal(h.frames.size, 0);
});

test("new history and disconnect discard in-flight visual motion", t => {
  const frames = [];
  const h = harness(t, {
    configuration: normalizeScrollbar({ placement: "beside", render: frame => { frames.push(frame); } })
  });
  h.flush();
  h.emit("wheel", { deltaY: 1 });
  h.advance(40);
  assert.equal(h.frames.size, 1);
  h.state.viewport = { ...viewport, generation: "2", top: 0 };
  h.controller.refresh();
  h.flush();
  assert.equal(frames.at(-1).thumb.top, 20);
  assert.equal(h.frames.size, 0);
  h.emit("wheel", { deltaY: 1 });
  h.advance(60);
  h.state.connected = false;
  h.controller.refresh();
  assert.equal(h.frames.size, 0);
  h.state.connected = true;
  h.controller.refresh();
  h.flush();
  assert.equal(frames.at(-1).thumb.top, 20);
  assert.equal(h.frames.size, 0);
});

test("track paging and wheel fractions use locally desired targets rather than accumulating stale deltas", t => {
  const h = harness(t);
  h.flush();
  h.emit("pointerdown", { buttons: 1, clientY: 65 }); // above thumb, page 40 -> 20.
  h.emit("pointerup", { clientY: 65 });
  h.flush();
  assert.deepEqual(h.rows, [20]);
  h.emit("wheel", { deltaY: -2 });
  h.emit("wheel", { deltaY: -3 });
  h.flush();
  assert.deepEqual(h.rows, [20, 15]);
  h.emit("wheel", { deltaY: 2, deltaMode: 0 });
  h.emit("wheel", { deltaY: 2, deltaMode: 0 });
  h.flush();
  assert.deepEqual(h.rows, [20, 15]);
  h.emit("wheel", { deltaY: 2, deltaMode: 0 });
  h.flush();
  assert.deepEqual(h.rows, [20, 15, 16]);
  h.emit("wheel", { deltaY: 100, deltaMode: 2 });
  h.flush();
  assert.equal(h.lives, 1);
});

test("capture listeners swallow scrollbar input and compatibility clicks without stealing active content selections", t => {
  const h = harness(t);
  const leaked = [];
  for (const type of ["pointerdown", "pointermove", "pointerup", "mousedown", "mouseup", "click", "wheel", "contextmenu"])
    h.element.addEventListener(type, event => leaked.push(event.type));
  for (const type of ["pointerdown", "mousedown", "pointermove", "wheel", "pointerup", "mouseup", "click", "contextmenu"])
    assert.equal(h.emit(type).defaultPrevented, true, type);
  assert.deepEqual(leaked, []);
  h.emit("pointerdown", { clientX: 130, buttons: 1 });
  assert.equal(h.emit("pointermove", { buttons: 1 }).defaultPrevented, false);
  assert.equal(h.emit("wheel").defaultPrevented, false);
  assert.equal(h.emit("pointerup").defaultPrevented, false);
  assert.deepEqual(leaked, ["pointerdown", "pointermove", "wheel", "pointerup"]);
  assert.equal(h.element.captures.size, 0);
});

test("pointer cancellation, lost capture, disconnect, and disposal release capture and cancel navigation", t => {
  const h = harness(t);
  for (const ending of ["pointercancel", "lostpointercapture"]) {
    h.emit("pointerdown", { buttons: 1 });
    h.emit("pointermove", { buttons: 1, clientY: 130 });
    h.emit(ending);
    h.flush();
    assert.deepEqual(h.rows, []);
    assert.equal(h.element.captures.size, 0);
  }
  h.emit("pointerdown", { buttons: 1 });
  h.emit("pointermove", { buttons: 1, clientY: 130 });
  h.state.connected = false;
  h.controller.refresh();
  assert.equal(h.frames.size, 0);
  assert.equal(h.timers.size, 0);
  assert.equal(h.element.captures.size, 0);
  assert.equal(h.accessibility.hidden, true);
  h.state.connected = true;
  h.controller.refresh();
  h.flush();
  h.controller.dispose();
  assert.equal(h.frames.size, 0);
  assert.equal(h.timers.size, 0);
  assert.equal(h.emit("pointerdown").defaultPrevented, false);
  assert.equal(h.accessibility.tabIndex, -1);
});

test("drag release outside the activation strip still owns its compatibility mouseup and click", t => {
  const h = harness(t);
  h.emit("pointerdown", { buttons: 1 });
  assert.equal(h.emit("pointermove", { buttons: 1, clientX: 130, clientY: 130 }).defaultPrevented, true);
  assert.equal(h.emit("pointerup", { clientX: 130, clientY: 130 }).defaultPrevented, true);
  assert.equal(h.emit("mouseup", { clientX: 130, clientY: 130 }).defaultPrevented, true);
  assert.equal(h.emit("click", { clientX: 130, clientY: 130 }).defaultPrevented, true);
  assert.equal(h.emit("click", { clientX: 130, clientY: 130 }).defaultPrevented, false);
});

test("generation changes cancel old drag targets rather than applying them to a new history", t => {
  const h = harness(t);
  h.emit("pointerdown", { buttons: 1 });
  h.emit("pointermove", { buttons: 1, clientY: 130 });
  h.state.viewport = { ...viewport, generation: "2" };
  h.controller.refresh();
  h.flush();
  assert.equal(h.element.captures.size, 0);
  assert.deepEqual(h.rows, []);
});

test("empty, alternate and disabled history hide accessibility and leave layout gutter unchanged", t => {
  const h = harness(t);
  h.flush();
  for (const next of [{ ...viewport, liveTop: 0 }, { ...viewport, buffer: "alternate" }, { available: false }]) {
    h.state.viewport = next;
    h.controller.refresh();
    assert.equal(h.accessibility.hidden, true);
    assert.equal(h.frames.size, 0);
    assert.deepEqual(h.state.layout.scrollbar, layout.scrollbar);
  }
  h.state.viewport = viewport;
  h.state.configuration = false;
  h.controller.refresh();
  assert.equal(h.accessibility.hidden, true);
  h.state.configuration = normalizeScrollbar({ placement: "beside" });
  h.controller.refresh();
  h.flush();
  assert.equal(h.accessibility.hidden, false);
  assert.equal(h.accessibility.style.left, "198px");
});

test("accessible keyboard scrolling, focus reveal and adjacent marker navigation never reach application handlers", t => {
  const snapshots = [];
  const h = harness(t, { markers: [marker("earlier", 10), marker("later", 70)],
    configuration: normalizeScrollbar({ render: frame => { snapshots.push(frame); } }) });
  const leaked = [];
  h.accessibility.addEventListener("keydown", event => leaked.push(event.key));
  assert.equal(h.accessibility.getAttribute("role"), "scrollbar");
  assert.equal(h.accessibility.getAttribute("aria-valuemax"), "80");
  h.accessibility.focus();
  h.advance(2000);
  assert.equal(snapshots.at(-1).opacity, 1);
  assert.equal(snapshots.at(-1).interaction.focused, true);
  for (const key of ["ArrowUp", "PageUp", "ArrowDown", "PageDown"])
    assert.equal(h.emit("keydown", { key }, h.accessibility).defaultPrevented, true);
  h.flush();
  assert.deepEqual(h.rows, [40]);
  h.emit("keydown", { key: "Home" }, h.accessibility);
  h.flush();
  assert.deepEqual(h.rows, [40, 0]);
  h.emit("keydown", { key: "End" }, h.accessibility);
  h.flush();
  assert.equal(h.lives, 1);
  h.state.viewport = { ...viewport, requestId: 2 };
  h.controller.refresh();
  for (const key of ["ArrowDown", "ArrowUp"])
    h.emit("keydown", { key, altKey: true }, h.accessibility);
  assert.deepEqual(h.jumps, ["later", "earlier"]);
  assert.deepEqual(leaked, []);
  h.emit("keydown", { key: "x" }, h.accessibility);
  assert.deepEqual(leaked, ["x"]);
});

test("pointer dragging and retained keyboard focus never draw a thumb outline", t => {
  const h = harness(t);
  h.flush();
  h.emit("pointerdown", { buttons: 1 });
  h.flush();
  assert.equal(h.accessibility.getAttribute("data-pointer-active"), "true");
  assert.equal(h.ctx.outlines.length, 0);
  h.emit("pointermove", { buttons: 1, clientY: 115 });
  h.flush();
  assert.equal(h.ctx.outlines.length, 0);
  h.emit("pointerup");
  h.flush();
  assert.equal(h.accessibility.getAttribute("data-pointer-active"), "false");
  assert.equal(h.ctx.outlines.length, 0);
  h.emit("pointerdown", { buttons: 1 });
  h.emit("pointercancel");
  assert.equal(h.accessibility.getAttribute("data-pointer-active"), "false");
});

test("controller exposes resolved monochrome marker shades without changing marker metadata", t => {
  let captured;
  const markers = [
    marker("input", 10, { source: "command", phase: "commandLine" }),
    marker("executing", 20, { source: "command", phase: "executing" }),
    marker("success", 30, { source: "command", phase: "finished", exitCode: 0 }),
    marker("failure", 40, { source: "command", phase: "finished", exitCode: 5 }),
    marker("bookmark", 50)
  ];
  const h = harness(t, { markers, configuration: normalizeScrollbar({ render: frame => { captured = frame; } }) });
  h.flush();
  assert.deepEqual(captured.markers.map(tick => tick.color),
    ["#888888", "#bbbbbb", "#999999", "#eeeeee", "#dddddd"]);
  assert.deepEqual(captured.markers.map(tick => tick.marker), markers);
  assert.ok(captured.markers.every(Object.isFrozen));
});

test("marker hover exposes safe labels and stable IDs; default ticks preserve custom and error colors", t => {
  const label = '<img src="x" onerror="alert(1)">';
  const h = harness(t, { markers: [
    marker("z", 10, { label }), marker("a", 10, { label, color: "#123456" }),
    marker("error", 60, { source: "command", phase: "finished", exitCode: 7 })
  ] });
  h.flush();
  assert.ok(h.ctx.fills.some(fill => fill.color === "#123456"));
  assert.ok(h.ctx.fills.some(fill => fill.color === "#eeeeee"));
  const y = 50 + (20 + 195 * 10 / 99 + 2.5) / 2;
  h.emit("pointermove", { clientY: y });
  assert.equal(h.hovers.at(-1).marker.label, label);
  assert.ok(Object.isFrozen(h.hovers.at(-1).marker));
  h.emit("pointerdown", { clientY: y, buttons: 1 });
  assert.deepEqual(h.jumps, ["a"]);
  assert.deepEqual(h.rows, []);
  assert.equal(h.hovers.at(-1), null);
});

test("hover tooltips and painter snapshots clear during gestures, leave, marker removal and disconnect", t => {
  const frames = [];
  const h = harness(t, {
    markers: [marker("hover", 10)],
    configuration: normalizeScrollbar({ render: frame => { frames.push(frame); } })
  });
  const clientY = 50 + (20 + 195 * 10 / 99 + 2.5) / 2;
  const hover = () => { h.emit("pointermove", { clientY }); h.flush(); };
  hover();
  assert.equal(h.hovers.at(-1).marker.id, "hover");
  assert.equal(frames.at(-1).hoveredMarker.marker.id, "hover");
  h.emit("pointerdown", { buttons: 1 });
  h.emit("pointermove", { clientY, buttons: 1 });
  h.flush();
  assert.equal(h.hovers.at(-1), null);
  assert.equal(frames.at(-1).hoveredMarker, null);
  h.emit("pointerup");
  hover();
  h.emit("pointerleave");
  assert.equal(h.hovers.at(-1), null);
  hover();
  h.state.markers = [];
  h.controller.activity();
  h.flush();
  assert.equal(h.hovers.at(-1), null);
  h.state.markers = [marker("hover", 10)];
  hover();
  h.state.connected = false;
  h.controller.refresh();
  assert.equal(h.hovers.at(-1), null);
  h.state.connected = true;
  h.controller.refresh();
  hover();
  h.controller.dispose();
  assert.equal(h.hovers.at(-1), null);
});

test("content drags and externally started drags never expose marker hover", t => {
  const h = harness(t, { markers: [marker("hover", 10)] });
  const clientY = 50 + (20 + 195 * 10 / 99 + 2.5) / 2;
  h.emit("pointermove", { clientY, buttons: 1 });
  assert.equal(h.hovers.at(-1), null);
  h.emit("pointerdown", { clientX: 150, buttons: 1 });
  h.emit("pointermove", { clientY });
  assert.equal(h.hovers.at(-1), null);
  h.emit("pointerup");
  h.emit("pointermove", { clientY });
  assert.equal(h.hovers.at(-1).marker.id, "hover");
});

test("moving over content does not wake a dormant scrollbar painter", t => {
  const h = harness(t);
  h.flush();
  h.advance(1300);
  assert.equal(h.frames.size, 0);
  for (const clientX of [120, 140, 150]) h.emit("pointermove", { clientX });
  assert.equal(h.frames.size, 0);
});
