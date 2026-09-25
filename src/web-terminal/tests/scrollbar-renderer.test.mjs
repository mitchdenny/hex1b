import assert from "node:assert/strict";
import { test } from "node:test";
import { createDefaultScrollbarRenderer, renderDefaultScrollbar } from "../.build/scrollbar-renderer.js";

const colors = { track: "#111111", thumb: "#999999", marker: "#0088ff", error: "#ff0000" };

class Gradient {
  constructor(...coordinates) { this.coordinates = coordinates; this.stops = []; }
  addColorStop(...stop) { this.stops.push(stop); }
}

const paintColor = paint => paint.color instanceof Gradient ? paint.color.stops.at(-1)[1] : paint.color;

function context() {
  return {
    globalAlpha: 1, fillStyle: "#123456", strokeStyle: "#654321", lineWidth: 3,
    stack: [], paints: [], paths: [], path: [],
    save() {
      this.stack.push({
        globalAlpha: this.globalAlpha, fillStyle: this.fillStyle,
        strokeStyle: this.strokeStyle, lineWidth: this.lineWidth
      });
    },
    restore() { Object.assign(this, this.stack.pop()); },
    beginPath() { this.path = []; },
    createLinearGradient(...coordinates) { return new Gradient(...coordinates); },
    rect(...args) { this.path.push(args); },
    moveTo(...args) { this.path.push(["moveTo", ...args]); },
    lineTo(...args) { this.path.push(["lineTo", ...args]); },
    quadraticCurveTo(...args) { this.path.push(["quadraticCurveTo", ...args]); },
    closePath() { this.path.push(["closePath"]); },
    roundRect(...args) { this.path.push(args); },
    fillRect(...args) {
      this.paints.push({ kind: "rect", args, color: this.fillStyle, alpha: this.globalAlpha });
    },
    fill() {
      this.paths.push([...this.path]);
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
  return { marker: { exitCode, color }, bounds: { left: 201.5, top: 60, width: 5, height: 5 } };
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
    { kind: "fill", args: [198, 20, 12, 200, 6], color: colors.track, alpha: 0.4 * 0.5 * 0.35 },
    { kind: "fill", args: [201.5, 60, 5, 5, 2.5], color: colors.marker, alpha: 0.2 },
    { kind: "fill", args: [200, 100, 8, 40, 4], color: colors.thumb, alpha: 0.2 }
  ]);
  assert.equal(value.context.globalAlpha, 0.4);
  assert.equal(value.context.fillStyle, "#123456");
  assert.equal(value.context.strokeStyle, "#654321");
  assert.equal(value.context.lineWidth, 3);
  assert.equal(value.context.stack.length, 0);
});

test("track shadow uses a bounded Bezier contour and one fill around floated marks", () => {
  for (const width of [4, 12, 64]) {
    for (const left of [0, 1, 200]) {
      const track = Object.freeze({ left, top: 20, width, height: 200 });
      const a = frame({ track }), b = frame({ track, markers: [
        { marker: {}, bounds: { left: Math.max(0, left - 7), top: 110, width: 5, height: 5 } }
      ] });
      renderDefaultScrollbar(a);
      renderDefaultScrollbar(b);
      assert.deepEqual(a.context.paints[0], {
        kind: "fill", args: [left, 20, width, 200, width / 2], color: colors.track, alpha: 0.175
      });
      const paint = b.context.paints[0];
      if (left === 0) assert.equal(paint.color, colors.track);
      else {
        const start = Math.max(0, left - 13);
        assert.equal(paint.kind, "fill");
        assert.deepEqual(paint.color.coordinates, [start, 0, left, 0]);
        assert.deepEqual(paint.color.stops, [[0, "transparent"], [1, colors.track]]);
        const path = b.context.paths[0], curves = path.filter(command => command[0] === "quadraticCurveTo");
        const radius = width / 2, right = left + width;
        assert.deepEqual(path.slice(0, 3), [
          ["moveTo", right - radius, 20], ["lineTo", left + radius, 20],
          ["quadraticCurveTo", left, 20, left, 20 + radius]
        ]);
        assert.deepEqual(path.slice(-7), [
          ["lineTo", left, 220 - radius],
          ["quadraticCurveTo", left, 220, left + radius, 220],
          ["lineTo", right - radius, 220],
          ["quadraticCurveTo", right, 220, right, 220 - radius],
          ["lineTo", right, 20 + radius],
          ["quadraticCurveTo", right, 20, right - radius, 20], ["closePath"]
        ]);
        assert.ok(curves.length > 0 && curves.length < 24);
        for (const [, x, y, endX, endY] of curves.slice(1, -3)) {
          assert.ok(x >= start && x <= left && endX >= start && endX <= left);
          assert.ok(y >= 20 && y <= 220 && endY >= 20 && endY <= 220);
        }
      }
      assert.deepEqual(track, { left, top: 20, width, height: 200 });
    }
  }
});

test("retracting circles shrink their shadow smoothly and centered marks do not extend it", () => {
  let previousLeft = -Infinity;
  for (const left of [191, 194, 197, 197.9, 198]) {
    const value = frame({ markers: [{ marker: {}, bounds: { left, top: 110, width: 5, height: 5 } }] });
    renderDefaultScrollbar(value);
    if (left === 198) assert.deepEqual(value.context.paints[0].args, [198, 20, 12, 200, 6]);
    else {
      const shadowLeft = value.context.paints[0].color.coordinates[0];
      assert.ok(shadowLeft > previousLeft);
      previousLeft = shadowLeft;
    }
  }
});

test("shadow contour clips to the track and ignores offscreen or empty marks", () => {
  const marks = [
    { marker: {}, bounds: { left: 191, top: 20, width: 5, height: 5 } },
    { marker: {}, bounds: { left: 191, top: 215, width: 5, height: 5 } },
    { marker: {}, bounds: { left: 191, top: 0, width: 5, height: 5 } },
    { marker: {}, bounds: { left: 191, top: 220, width: 5, height: 5 } },
    { marker: {}, bounds: { left: 191, top: 110, width: 0, height: 5 } }
  ];
  const value = frame({ markers: marks }), valid = frame({ markers: marks.slice(0, 2) });
  renderDefaultScrollbar(value);
  renderDefaultScrollbar(valid);
  const path = value.context.paths[0];
  assert.deepEqual(path, valid.context.paths[0]);
  assert.deepEqual(path[1], ["lineTo", 191, 20]);
  assert.deepEqual(path[2], ["quadraticCurveTo", 185, 20, 185, 26]);
  assert.deepEqual(path.at(-7), ["lineTo", 185, 214]);
  for (const command of path)
    for (let i = 2; i < command.length; i += 2)
      assert.ok(command[i] >= 20 && command[i] <= 220);
});

test("nearby marks share a smooth bulge while distant groups return to the track", () => {
  const mark = top => ({ marker: {}, bounds: { left: 191, top, width: 5, height: 5 } });
  const near = frame({ markers: [mark(110), mark(126)] });
  renderDefaultScrollbar(near);
  const path = near.context.paths[0];
  assert.equal(path.filter(command => command[0] === "moveTo").length, 1);
  assert.equal(path.filter(command => command[0] === "closePath").length, 1);
  assert.ok(path.some(([kind, x, y]) => kind === "lineTo" && x === 185 && y >= 131));
  assert.ok(!path.some(([kind, , y]) => kind === "quadraticCurveTo" && y >= 110 && y <= 131),
    "Nearby marks should not each get their own scallop");

  const far = frame({ markers: [mark(110), mark(170)] });
  renderDefaultScrollbar(far);
  assert.ok(far.context.paths[0].some(([kind, x, y]) =>
    kind === "lineTo" && x === 198 && y > 133 && y < 153));
});

test("seated marks add no control points and marker order or duplicates do not change the contour", () => {
  const floated = [
    { marker: {}, bounds: { left: 191, top: 110, width: 5, height: 5 } },
    { marker: {}, bounds: { left: 194, top: 126, width: 5, height: 5 } }
  ];
  const seated = Array.from({ length: 10000 }, (_, i) => ({
    marker: {}, bounds: { left: 201.5, top: 20 + i % 195, width: 5, height: 5 }
  }));
  const base = frame({ markers: floated });
  renderDefaultScrollbar(base);
  for (const marks of [[...seated, ...floated], [...floated].reverse(), [...floated, ...floated]]) {
    const value = frame({ markers: marks });
    renderDefaultScrollbar(value);
    assert.deepEqual(value.context.paths[0], base.context.paths[0]);
    assert.deepEqual(value.context.paints[0], base.context.paints[0]);
  }
  const onlySeated = frame({ markers: seated });
  onlySeated.context.createLinearGradient = () => { throw new Error("Seated marks must use the plain track"); };
  renderDefaultScrollbar(onlySeated);
  assert.deepEqual(onlySeated.context.paints[0].args, [198, 20, 12, 200, 6]);
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

test("dense displaced circles stay circular and paint behind the thumb without a focus outline", () => {
  const marks = Array.from({ length: 100 }, (_, index) => ({
    marker: { exitCode: index % 2 },
    bounds: { left: 191, top: 110 + index / 10, width: 5, height: 5 }
  }));
  marks.unshift({ marker: {}, bounds: { left: 201.5, top: 20, width: 5, height: 5 } });
  const value = frame({ markers: marks, interaction: { focused: true }, opacity: 1 });
  renderDefaultScrollbar(value);
  const paints = value.context.paints;
  assert.equal(paints.length, marks.length + 2);
  assert.deepEqual(paints[1].args, [201.5, 20, 5, 5, 2.5]);
  assert.equal(paints[0].alpha, 0.35);
  assert.ok(value.context.paths[0].filter(command => command[0] === "quadraticCurveTo").length < 24,
    "Dense marks share a small number of contour controls");
  assert.ok(paints.slice(1, -1).every(paint =>
    paint.kind === "fill" && paint.args[2] === 5 && paint.args[3] === 5 && paint.args[4] === 2.5));
  assert.deepEqual(paints.at(-1), {
    kind: "fill", args: [200, 100, 8, 40, 4], color: colors.thumb, alpha: 1
  });
  assert.ok(paints.every(paint => paint.kind !== "stroke"));
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
  assert.deepEqual(a.context.paints.map(p => [paintColor(p), p.alpha]), [
    ["#111111", 0.1], ["#333333", 0.15], ["#444444", 0.15], [colors.thumb, 0.35]
  ]);
  assert.deepEqual(b.context.paints.map(p => [paintColor(p), p.alpha]), [
    ["#111111", 0.1], ["#333333", 0.15], ["#444444", 0.15], ["new-thumb", 0.35]
  ]);
  assert.equal(requests(), 1, "validation context is reused at creation and never needed for painting");
  const themed = frame({ colors: b.colors, markers: [marker(0), marker(1)], interaction: { focused: true } });
  createDefaultScrollbarRenderer()(themed);
  assert.deepEqual(themed.context.paints.map(paintColor),
    ["new-track", "new-marker", "new-error", "new-thumb"]);
});

test("per-marker colors override configured success and error colors", t => {
  colorCanvas(t);
  const renderer = createDefaultScrollbarRenderer({ markers: { color: "#333333", errorColor: "#444444" } });
  const value = frame({ markers: [
    marker(undefined), marker(null), marker(0), marker(1), marker(-1),
    marker(0, "override-success"), marker(1, "override-error")
  ] });
  renderer(value);
  assert.deepEqual(value.context.paints.slice(1, -1).map(p => p.color),
    ["#333333", "#333333", "#333333", "#444444", "#444444", "override-success", "override-error"]);
  const fallback = frame({ markers: [marker(0), marker(1)] });
  createDefaultScrollbarRenderer({ markers: { color: "#333333" } })(fallback);
  assert.deepEqual(fallback.context.paints.slice(1, -1).map(p => p.color), ["#333333", colors.error]);
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
  assert.deepEqual(value.context.paints.slice(1, -1).map(p => p.color), ["#333333", "#444444"]);
});

test("part opacity composes independently without adding focus paint", t => {
  colorCanvas(t);
  const value = frame({ markers: [marker(1)], interaction: { focused: true } });
  value.context.globalAlpha = 0.4;
  createDefaultScrollbarRenderer({
    track: { opacity: 0.6 }, thumb: { opacity: 0 }, markers: { color: "#333333", opacity: 0 }
  })(value);
  assert.deepEqual(value.context.paints.map(p => p.alpha), [0.12, 0, 0]);
  assert.equal(value.context.paints.at(-1).color, colors.thumb);
  assert.equal(value.context.globalAlpha, 0.4);
  value.context.fillRect(1, 2, 3, 4);
  assert.deepEqual(value.context.paints.at(-1), {
    kind: "rect", args: [1, 2, 3, 4], color: "#123456", alpha: 0.4
  });
});

test("focus and dragging never add an outline or change the default paint", () => {
  const idle = frame();
  renderDefaultScrollbar(idle);
  for (const focused of [false, true])
    for (const dragging of [false, true]) {
      const value = frame({ interaction: { focused, dragging } });
      renderDefaultScrollbar(value);
      assert.deepEqual(value.context.paints, idle.context.paints);
      assert.ok(value.context.paints.every(paint => paint.kind !== "stroke"));
    }
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
  assert.deepEqual(defaults.context.paints.slice(1, -1).map(p => p.color), ["#999999", "#eeeeee", "red"]);
  const configured = frame({ markers });
  createDefaultScrollbarRenderer({ markers: { color: "#333333", errorColor: "#444444" } })(configured);
  assert.deepEqual(configured.context.paints.slice(1, -1).map(p => p.color), ["#333333", "#444444", "red"]);
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

test("tiny tracks and thumbs have bounded capsule radii without focus outlines", () => {
  for (const [width, height, inset, radius] of [
    [4, 24, 1, 1], [12, 1, 2, 0.5], [1, 1, 0.25, 0.25], [0.2, 0.1, 0.05, 0.05]
  ]) {
    const thumb = { left: 10, top: 20, width, height };
    const value = frame({ track: thumb, thumb, interaction: { focused: true } });
    renderDefaultScrollbar(value);
    assert.deepEqual(value.context.paints[1].args, [10 + inset, 20, width - inset * 2, height, radius]);
    assert.deepEqual(value.context.paints[0].args, [10, 20, width, height, Math.min(width, height) / 2]);
    assert.equal(value.context.paints.length, 2);
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

test("rounded shadow end caps stay bounded on short and fractional tracks", () => {
  for (const width of [0.2, 4, 12, 64])
    for (const height of [0.1, 1, 8, 20]) {
      const track = { left: 20, top: 5, width, height };
      const value = frame({ track, thumb: { ...track, width: 0 }, markers: [
        { marker: {}, bounds: { left: 13, top: 5, width: 5, height: Math.min(5, height) } },
        { marker: {}, bounds: { left: 13, top: 5 + height - Math.min(5, height), width: 5, height: Math.min(5, height) } }
      ] });
      renderDefaultScrollbar(value);
      const path = value.context.paths[0];
      assert.equal(path.filter(command => command[0] === "quadraticCurveTo").length, 4);
      for (const command of path)
        for (let i = 1; i < command.length; i++) {
          assert.ok(Number.isFinite(command[i]));
          if (i % 2 === 0) assert.ok(command[i] >= 5 && command[i] <= 5 + height);
          else assert.ok(command[i] >= 0 && command[i] <= 20 + width);
        }
    }
});

test("wide track end shadows round within their padding rather than cutting into floated marks", () => {
  const value = frame({ track: { left: 198, top: 20, width: 64, height: 200 }, markers: [
    { marker: {}, bounds: { left: 191, top: 20, width: 5, height: 5 } },
    { marker: {}, bounds: { left: 191, top: 215, width: 5, height: 5 } }
  ] });
  renderDefaultScrollbar(value);
  const path = value.context.paths[0];
  assert.deepEqual(path[2], ["quadraticCurveTo", 185, 20, 185, 26]);
  assert.deepEqual(path.at(-6), ["quadraticCurveTo", 185, 220, 191, 220]);
  assert.deepEqual(path.at(-4), ["quadraticCurveTo", 262, 220, 262, 188]);
  assert.deepEqual(path.at(-2), ["quadraticCurveTo", 262, 20, 230, 20]);
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
    assert.deepEqual(value.context.paints.map(paintColor), Array(3).fill(color.trim()));
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
