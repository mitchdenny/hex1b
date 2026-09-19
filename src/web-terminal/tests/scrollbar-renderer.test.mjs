import assert from "node:assert/strict";
import { test } from "node:test";
import { createDefaultScrollbarRenderer, renderDefaultScrollbar } from "../.build/scrollbar-renderer.js";

const colors = { track: "#111111", thumb: "#999999", marker: "#0088ff", error: "#ff0000" };

function context() {
  return {
    globalAlpha: 1, fillStyle: "#123456", strokeStyle: "#654321", lineWidth: 3,
    stack: [], paints: [], path: [],
    save() {
      this.stack.push({
        globalAlpha: this.globalAlpha, fillStyle: this.fillStyle,
        strokeStyle: this.strokeStyle, lineWidth: this.lineWidth
      });
    },
    restore() { Object.assign(this, this.stack.pop()); },
    beginPath() { this.path = []; },
    roundRect(...args) { this.path.push(args); },
    fillRect(...args) {
      this.paints.push({ kind: "rect", args, color: this.fillStyle, alpha: this.globalAlpha });
    },
    fill() {
      this.paints.push({ kind: "fill", args: this.path[0], color: this.fillStyle, alpha: this.globalAlpha });
    },
    stroke() {
      this.paints.push({
        kind: "stroke", args: this.path[0], color: this.strokeStyle,
        alpha: this.globalAlpha, lineWidth: this.lineWidth
      });
    }
  };
}

function frame(overrides = {}) {
  return {
    context: context(), canvas: {}, layout: {}, viewport: {}, pendingTarget: null,
    track: { left: 198, top: 20, width: 12, height: 200 },
    thumb: { left: 198, top: 100, width: 12, height: 40 },
    markers: [], interaction: { focused: false }, now: 0, opacity: 0.5, colors,
    ...overrides
  };
}

function marker(exitCode, color) {
  return { marker: { exitCode, color }, bounds: { left: 198, top: 60, width: 12, height: 3 } };
}

function installGlobal(t, name, value) {
  const previous = Object.getOwnPropertyDescriptor(globalThis, name);
  Object.defineProperty(globalThis, name, { configurable: true, value });
  t.after(() => {
    if (previous) Object.defineProperty(globalThis, name, previous);
    else delete globalThis[name];
  });
}

// Canvas ignores invalid assignments. This stub deliberately models that behavior,
// including canonical serialization, rather than pretending every string is a color.
function colorCanvas(t, { offscreen = true, valid = [] } = {}) {
  let requests = 0;
  const accepted = new Set([
    "#010203", "#040506", "#111111", "#222222", "#333333", "#444444",
    "red", "transparent", "rebeccapurple", "rgb(1 2 3 / 50%)", "hsl(120 50% 40%)",
    ...valid
  ]);
  const canvas = () => {
    requests++;
    let fillStyle = "#000000";
    return {
      get fillStyle() { return fillStyle; },
      set fillStyle(value) {
        if (accepted.has(value)) fillStyle = value === "red" ? "#ff0000" : value;
      }
    };
  };
  installGlobal(t, "OffscreenCanvas", offscreen ? class {
    constructor(width, height) { assert.deepEqual([width, height], [1, 1]); }
    getContext(kind) { assert.equal(kind, "2d"); return canvas(); }
  } : undefined);
  installGlobal(t, "document", {
    createElement(tag) {
      assert.equal(tag, "canvas");
      return { getContext(kind) { assert.equal(kind, "2d"); return canvas(); } };
    }
  });
  return () => requests;
}

test("default painter uses a translucent track, inset capsule and exact composed alpha", () => {
  const value = frame({ markers: [marker(0)], interaction: { focused: true } });
  value.context.globalAlpha = 0.4;
  assert.equal(renderDefaultScrollbar(value), undefined);
  assert.deepEqual(value.context.paints, [
    { kind: "rect", args: [198, 20, 12, 200], color: colors.track, alpha: 0.4 * 0.5 * 0.35 },
    { kind: "fill", args: [200, 100, 8, 40, 4], color: colors.thumb, alpha: 0.2 },
    { kind: "rect", args: [198, 60, 12, 3], color: colors.marker, alpha: 0.2 },
    { kind: "stroke", args: [198.5, 100.5, 11, 39, 5.5], color: colors.thumb, alpha: 0.2, lineWidth: 1 }
  ]);
  assert.equal(value.context.globalAlpha, 0.4);
  assert.equal(value.context.fillStyle, "#123456");
  assert.equal(value.context.strokeStyle, "#654321");
  assert.equal(value.context.lineWidth, 3);
  assert.equal(value.context.stack.length, 0);
});

test("factory defaults match the built-in painter and work without DOM globals", async t => {
  installGlobal(t, "OffscreenCanvas", undefined);
  installGlobal(t, "document", undefined);
  const imported = await import(`../.build/scrollbar-renderer.js?no-dom=${Date.now()}`);
  const a = frame(), b = frame(), c = frame();
  imported.renderDefaultScrollbar(a);
  imported.createDefaultScrollbarRenderer()(b);
  imported.createDefaultScrollbarRenderer({ track: {}, thumb: { opacity: 1 }, markers: {} })(c);
  assert.deepEqual(a.context.paints, b.context.paints);
  assert.deepEqual(a.context.paints, c.context.paints);
});

test("options are snapshotted while omitted colors follow each frame's theme", t => {
  const requests = colorCanvas(t);
  const appearance = { track: { color: "#111111", opacity: 0.2 }, thumb: { opacity: 0.7 },
    markers: { color: "#333333", errorColor: "#444444", opacity: 0.3 } };
  const renderer = createDefaultScrollbarRenderer(appearance);
  appearance.track.color = "#222222";
  appearance.track.opacity = 1;
  appearance.thumb.opacity = 0;
  appearance.markers.color = "red";
  appearance.markers.errorColor = "red";
  appearance.markers.opacity = 1;
  appearance.thumb = { color: "red" };
  const a = frame({ markers: [marker(0), marker(1)] });
  renderer(a);
  const b = frame({ colors: { track: "new-track", thumb: "new-thumb", marker: "new-marker", error: "new-error" },
    markers: [marker(0), marker(1)] });
  renderer(b);
  assert.deepEqual(a.context.paints.map(p => [p.color, p.alpha]), [
    ["#111111", 0.1], [colors.thumb, 0.35], ["#333333", 0.15], ["#444444", 0.15]
  ]);
  assert.deepEqual(b.context.paints.map(p => [p.color, p.alpha]), [
    ["#111111", 0.1], ["new-thumb", 0.35], ["#333333", 0.15], ["#444444", 0.15]
  ]);
  assert.equal(requests(), 1, "validation context is reused at creation and never needed for painting");
  const themed = frame({ colors: b.colors, markers: [marker(0), marker(1)], interaction: { focused: true } });
  createDefaultScrollbarRenderer()(themed);
  assert.deepEqual(themed.context.paints.map(p => p.color),
    ["new-track", "new-thumb", "new-marker", "new-error", "new-thumb"]);
});

test("per-marker colors override configured success and error colors", t => {
  colorCanvas(t);
  const renderer = createDefaultScrollbarRenderer({ markers: { color: "#333333", errorColor: "#444444" } });
  const value = frame({ markers: [
    marker(undefined), marker(null), marker(0), marker(1), marker(-1),
    marker(0, "override-success"), marker(1, "override-error")
  ] });
  renderer(value);
  assert.deepEqual(value.context.paints.slice(2).map(p => p.color),
    ["#333333", "#333333", "#333333", "#444444", "#444444", "override-success", "override-error"]);
  const fallback = frame({ markers: [marker(0), marker(1)] });
  createDefaultScrollbarRenderer({ markers: { color: "#333333" } })(fallback);
  assert.deepEqual(fallback.context.paints.slice(2).map(p => p.color), ["#333333", colors.error]);
});

test("invalid per-marker colors preserve the appropriate safe fallback", t => {
  colorCanvas(t);
  const value = frame({ markers: [marker(0, "not-a-color"), marker(1, "not-a-color")] });
  let style = value.context.fillStyle;
  Object.defineProperty(value.context, "fillStyle", {
    get: () => style,
    set: color => { if (color !== "not-a-color") style = color; }
  });
  createDefaultScrollbarRenderer({ markers: { color: "#333333", errorColor: "#444444" } })(value);
  assert.deepEqual(value.context.paints.slice(2).map(p => p.color), ["#333333", "#444444"]);
});

test("part opacity composes independently and cannot hide the focus outline", t => {
  colorCanvas(t);
  const value = frame({ markers: [marker(1)], interaction: { focused: true } });
  value.context.globalAlpha = 0.4;
  createDefaultScrollbarRenderer({
    track: { opacity: 0.6 }, thumb: { opacity: 0 }, markers: { color: "#333333", opacity: 0 }
  })(value);
  assert.deepEqual(value.context.paints.map(p => p.alpha), [0.12, 0, 0, 0.2]);
  assert.equal(value.context.paints.at(-1).color, colors.thumb);
  assert.equal(value.context.globalAlpha, 0.4);
  value.context.fillRect(1, 2, 3, 4);
  assert.deepEqual(value.context.paints.at(-1), {
    kind: "rect", args: [1, 2, 3, 4], color: "#123456", alpha: 0.4
  });
});

test("focused dragging draws the capsule without an outline, then restores neutral focus feedback", () => {
  const dragging = frame({ interaction: { focused: true, dragging: true } });
  renderDefaultScrollbar(dragging);
  assert.equal(dragging.context.paints.filter(paint => paint.kind === "stroke").length, 0);
  assert.equal(dragging.context.paints.filter(paint => paint.kind === "fill").length, 1);
  const focused = frame({ interaction: { focused: true, dragging: false } });
  renderDefaultScrollbar(focused);
  assert.equal(focused.context.paints.at(-1).kind, "stroke");
  assert.equal(focused.context.paints.at(-1).color, colors.thumb);
});

test("resolved marker shades are defaults, with factory and per-marker overrides taking precedence", t => {
  colorCanvas(t);
  const markers = [
    { ...marker(0), color: "#999999" },
    { ...marker(1), color: "#eeeeee" },
    { ...marker(0, "red"), color: "#bbbbbb" }
  ];
  const defaults = frame({ markers });
  renderDefaultScrollbar(defaults);
  assert.deepEqual(defaults.context.paints.slice(2).map(p => p.color), ["#999999", "#eeeeee", "red"]);
  const configured = frame({ markers });
  createDefaultScrollbarRenderer({ markers: { color: "#333333", errorColor: "#444444" } })(configured);
  assert.deepEqual(configured.context.paints.slice(2).map(p => p.color), ["#333333", "#444444", "red"]);
});

test("drawing state is restored even when a canvas operation throws", () => {
  const value = frame();
  value.context.roundRect = () => { throw new Error("paint failed"); };
  assert.throws(() => renderDefaultScrollbar(value), /paint failed/);
  assert.equal(value.context.globalAlpha, 1);
  assert.equal(value.context.fillStyle, "#123456");
  assert.equal(value.context.stack.length, 0);
});

test("zero frame opacity performs no drawing or state changes", () => {
  const value = frame({ opacity: 0 });
  value.context.save = () => { throw new Error("unexpected save"); };
  renderDefaultScrollbar(value);
  assert.deepEqual(value.context.paints, []);
});

test("tiny thumbs have bounded insets, capsule radii and focus outlines", () => {
  for (const [width, height, inset, radius] of [
    [4, 24, 1, 1], [12, 1, 2, 0.5], [1, 1, 0.25, 0.25], [0.2, 0.1, 0.05, 0.05]
  ]) {
    const thumb = { left: 10, top: 20, width, height };
    const value = frame({ track: thumb, thumb, interaction: { focused: true } });
    renderDefaultScrollbar(value);
    assert.deepEqual(value.context.paints[1].args, [10 + inset, 20, width - inset * 2, height, radius]);
    const outline = value.context.paints[2];
    assert.ok(outline.args.every(Number.isFinite));
    assert.ok(outline.args[2] > 0 && outline.args[3] > 0 && outline.args[4] > 0);
    assert.equal(outline.args[4], Math.min(outline.args[2], outline.args[3]) / 2);
    assert.ok(outline.lineWidth <= Math.min(width, height));
    assert.deepEqual(thumb, { left: 10, top: 20, width, height });
  }
  for (const [width, height] of [[0, 1], [1, 0], [-1, 1], [1, -1]]) {
    const bounds = { left: 0, top: 0, width, height };
    const value = frame({ track: bounds, thumb: bounds, interaction: { focused: true },
      markers: [{ marker: {}, bounds }] });
    renderDefaultScrollbar(value);
    assert.deepEqual(value.context.paints, []);
  }
});

test("appearance and nested parts reject malformed runtime shapes", () => {
  const invalid = [null, false, true, [], 0, "", () => {}, new Date(), new Map()];
  for (const value of invalid) {
    assert.throws(() => createDefaultScrollbarRenderer(value), TypeError);
    for (const name of ["track", "thumb", "markers"])
      assert.throws(() => createDefaultScrollbarRenderer({ [name]: value }), TypeError);
  }
  assert.doesNotThrow(() => createDefaultScrollbarRenderer(Object.create(null)));
});

test("every part requires finite numeric opacity in the inclusive range zero to one", () => {
  for (const name of ["track", "thumb", "markers"]) {
    for (const opacity of [null, "0.5", false, NaN, Infinity, -Infinity, -0.001, 1.001, {}, []])
      assert.throws(() => createDefaultScrollbarRenderer({ [name]: { opacity } }), RangeError);
    for (const opacity of [0, -0, 0.5, 1, undefined])
      assert.doesNotThrow(() => createDefaultScrollbarRenderer({ [name]: { opacity } }));
  }
});

test("configured colors reject malformed or context-dependent CSS before canvas validation", t => {
  installGlobal(t, "OffscreenCanvas", class {
    constructor() { throw new Error("unexpected canvas allocation"); }
  });
  for (const color of [null, false, 42, {}, [], "", " \t\n", "r".repeat(257),
    "var(--accent)", "VAR(--accent, red)", "inherit", "initial", "unset", "revert", "revert-layer",
    "currentColor", "rgb(from currentColor r g b)", "CanvasText", "ButtonFace",
    "light-dark(white, black)", "env(accent)", "attr(data-color type(<color>))",
    "color-mix(in srgb, red, Highlight)", "r\\65 d", "red/**/", "color(--brand 1 0 0)"]) {
    for (const [part, property] of [["track", "color"], ["thumb", "color"],
      ["markers", "color"], ["markers", "errorColor"]]) {
      assert.throws(() => createDefaultScrollbarRenderer({ [part]: { [property]: color } }), TypeError);
    }
  }
});

test("browser parsing rejects invalid colors rather than silently accepting canvas fallbacks", t => {
  colorCanvas(t);
  for (const color of ["not-a-color", "#12", "#gggggg", "rgb(nope)", "red; background: blue", "url(x)"]) {
    for (const [part, property] of [["track", "color"], ["thumb", "color"],
      ["markers", "color"], ["markers", "errorColor"]])
      assert.throws(() => createDefaultScrollbarRenderer({ [part]: { [property]: color } }), TypeError);
  }
});

test("concrete colors, both sentinels, trimming and the length boundary are accepted", t => {
  const longest = `rgb(${ " ".repeat(244) }1 2 3)`;
  assert.equal(longest.length, 254);
  const boundary = `${longest}  `;
  assert.equal(boundary.length, 256);
  colorCanvas(t, { valid: [longest] });
  for (const color of ["red", "transparent", "rebeccapurple", "#010203", "#040506",
    "rgb(1 2 3 / 50%)", "hsl(120 50% 40%)", " red ", boundary]) {
    const value = frame({ markers: [marker(1)] });
    createDefaultScrollbarRenderer({
      track: { color }, thumb: { color }, markers: { color, errorColor: color }
    })(value);
    assert.deepEqual(value.context.paints.map(p => p.color), Array(3).fill(color.trim()));
  }
  assert.throws(() => createDefaultScrollbarRenderer({ track: { color: `${boundary} ` } }), TypeError);
});

test("validation falls back to an HTML canvas when OffscreenCanvas is unavailable", t => {
  const requests = colorCanvas(t, { offscreen: false });
  assert.doesNotThrow(() => createDefaultScrollbarRenderer({ track: { color: "red" } }));
  assert.equal(requests(), 1);
});

test("supplied colors fail explicitly when no color parser is available", t => {
  installGlobal(t, "OffscreenCanvas", undefined);
  installGlobal(t, "document", undefined);
  assert.throws(() => createDefaultScrollbarRenderer({ thumb: { color: "red" } }), /requires a browser canvas/);
  installGlobal(t, "OffscreenCanvas", class { getContext() { return null; } });
  assert.throws(() => createDefaultScrollbarRenderer({ thumb: { color: "red" } }), /requires a browser canvas/);
});
