import assert from "node:assert/strict";
import { test } from "node:test";
import { normalizePalette, normalizeColorMode, compilePalette, defaultDarkPalette, defaultLightPalette,
  foregroundColor, backgroundColor, underlineColor } from "../.build/terminal-palette.js";
import { decodeFrame } from "../.build/protocol.js";
import { TerminalRenderer } from "../.build/renderer.js";
import { browser, mounting, present, frame, Target } from "./fixtures/browser.mjs";

const metadata = { colorEncoding: "indexed-v1", placements: [], cursor: { visible: false, x: 0, y: 0, shape: 2 } };
const cell = { index: 0, text: "A", width: 1, attributes: 0, underlineStyle: 1,
  foreground: 0x01000002, background: 0x03000000, underlineColor: 0x04000000 };
const dark = compilePalette(defaultDarkPalette);
const light = compilePalette(defaultLightPalette);

test("Palette validation copies and freezes JSON values and rejects malformed colors and indices", () => {
  const input = { ...defaultDarkPalette, ansi: [...defaultDarkPalette.ansi], extended: { 42: "#123456" } };
  const normalized = normalizePalette(input);
  input.ansi[2] = "#ffffff";
  input.extended[42] = "#ffffff";
  assert.equal(normalized.ansi[2], "#8dae82");
  assert.equal(normalized.extended[42], "#123456");
  assert.ok(Object.isFrozen(normalized) && Object.isFrozen(normalized.ansi) && Object.isFrozen(normalized.extended));
  for (const invalid of [null, [], false, {}, { ...input, ansi: input.ansi.slice(1) },
    { ...input, ansi: new Array(16) }, { ...input, foreground: "red" },
    { ...input, background: "#12345678" }, { ...input, cursor: "#xyzxyz" },
    { ...input, selectionBackground: "url(bad)" }, { ...input, selectionForeground: "#1234" },
    { ...input, extended: [] },
    ...["-1", "15", "256", "16.5", "016", "nope"].map(key => ({ ...input, extended: { [key]: "#123456" } }))])
    assert.throws(() => normalizePalette(invalid), TypeError);
  assert.equal(normalizeColorMode(), "dark");
  for (const mode of ["light", "dark", "system"]) assert.equal(normalizeColorMode(mode), mode);
  for (const mode of [null, "auto", false]) assert.throws(() => normalizeColorMode(mode));
});

test("Indexed colors use exact xterm cube levels and gray ramp with optional overrides", () => {
  const levels = [0, 95, 135, 175, 215, 255];
  for (let r = 0; r < 6; r++) for (let g = 0; g < 6; g++) for (let b = 0; b < 6; b++) {
    assert.equal(dark.indexed[16 + r * 36 + g * 6 + b],
      (0xff000000 | levels[r] | levels[g] << 8 | levels[b] << 16) >>> 0);
  }
  assert.equal(dark.indexed[232], 0xff080808);
  assert.equal(dark.indexed[255], 0xffeeeeee);
  const override = compilePalette(normalizePalette({ ...defaultDarkPalette, extended: { 42: "#123456" } }));
  assert.equal(override.indexed[42], 0xff563412);
  assert.equal(override.indexed[43], dark.indexed[43]);
});

test("Retained default and indexed colors repaint while explicit RGB stays untouched", () => {
  const source = Object.freeze({ ...cell });
  assert.equal(foregroundColor(source, metadata, dark), 0xff82ae8d);
  assert.equal(foregroundColor(source, metadata, light), 0xff31613e);
  assert.equal(backgroundColor(source, metadata, dark), 0x00323232);
  assert.equal(backgroundColor(source, metadata, light), 0x00c8d0d4);
  for (const palette of [light, dark]) {
    const rgb = { ...source, foreground: 0xfff09f76, background: 0xff604239, underlineColor: 0xff112233 };
    assert.equal(foregroundColor(rgb, metadata, palette), rgb.foreground);
    assert.equal(backgroundColor(rgb, metadata, palette), rgb.background);
    assert.equal(underlineColor(rgb, metadata, palette), rgb.underlineColor);
    assert.equal(foregroundColor({ ...source, foreground: 0x02000000 }, metadata, palette), palette.foreground);
    assert.equal(foregroundColor({ ...source, foreground: 0x0100000a }, metadata, palette), palette.indexed[10]);
  }
  assert.equal(source.foreground, 0x01000002);
});

test("Reverse, dim, underline inheritance, and explicit backgrounds resolve in the correct order", () => {
  const source = { ...cell, attributes: 32 | 2 };
  assert.equal(foregroundColor(source, metadata, dark), 0xff191919);
  assert.equal(backgroundColor(source, metadata, dark), 0xff82ae8d);
  assert.equal(underlineColor(source, metadata, dark), 0xff191919);
  assert.equal(underlineColor({ ...source, underlineColor: 0x01000004 }, metadata, dark), 0xffd1aa7b);
  assert.equal(backgroundColor({ ...cell, background: 0xff181818 }, metadata, dark), 0xff181818);
  const legacy = { ...source, foreground: 0xff112233, background: 0xff445566, underlineColor: 0xff778899 };
  assert.equal(foregroundColor(legacy, {}, light), legacy.foreground);
  assert.equal(backgroundColor(legacy, {}, light), legacy.background);
  assert.equal(underlineColor(legacy, {}, light), legacy.underlineColor);
});

test("Reference frames validate tags and reject malformed encodings without affecting legacy RGBA", () => {
  const state = { ...metadata, cells: [cell] };
  assert.equal(decodeFrame(frame(state)).cells[0].foreground, cell.foreground);
  for (const foreground of [0, 0x01000100, 0x02000001, 0x03000001, 0x04000000, 0xfe000000])
    assert.throws(() => decodeFrame(frame({ ...state, cells: [{ ...cell, foreground }] })), /color reference/u);
  for (const underline of [0, 0x04000001])
    assert.throws(() => decodeFrame(frame({ ...state, cells: [{ ...cell, underlineColor: underline }] })), /color reference/u);
  for (const colorEncoding of ["future-v2", 1, {}])
    assert.throws(() => decodeFrame(frame({ ...state, colorEncoding })), /color encoding/u);
  for (const colorEncodings of [null, {}, [1], ["x".repeat(65)]])
    assert.throws(() => decodeFrame(frame({ ...state, colorEncodings })), /color encodings/u);
  assert.equal(decodeFrame(frame({ cells: [{ ...cell, foreground: 0x12345678 }] })).cells[0].foreground, 0x12345678);
});

test("Shared GPU preparation resolves backgrounds, glyphs, underlines, cursor and clear color", () => {
  const render = palette => {
    const operations = [];
    const renderer = Object.assign(Object.create(TerminalRenderer.prototype), {
      columns: 1, rows: 1, width: 10, height: 20, atlas: {},
      glyphs: new Map([["0/1/A", { colored: false, u0: 0, v0: 0, u1: 1, v1: 1 }]]),
      backend: { submit(_instances, _count, _batches, background) { operations.push(["clear", background]); } },
      solid(...args) { operations.push(["solid", args[4]]); },
      quad(...args) { operations.push(["glyph", args[5]]); },
    });
    renderer.render([cell], { ...metadata, cursor: { visible: true, x: 0, y: 0, shape: 6 } },
      true, undefined, palette);
    return operations;
  };
  const green = [141 / 255, 174 / 255, 130 / 255, 1];
  assert.deepEqual(render(dark), [
    ["solid", [50 / 255, 50 / 255, 50 / 255, 0]], ["glyph", green], ["solid", green],
    ["solid", green], ["clear", [50 / 255, 50 / 255, 50 / 255, 1]],
  ]);
  assert.notDeepEqual(render(light), render(dark));
  const custom = compilePalette({ ...defaultLightPalette, cursor: "#123456" });
  assert.deepEqual(render(custom).at(-2), ["solid", [18 / 255, 52 / 255, 86 / 255, 1]]);
});

test("Public mode and palette changes repaint retained worker cells without input, resize, or ACK", async t => {
  const workers = browser(t);
  const view = await mounting(t, workers, { readOnly: true });
  await view.worker.request("open");
  await present(view, { colorEncodings: ["indexed-v1"] });
  const terminal = await view.promise;
  assert.equal(terminal.colorMode, "dark");
  assert.deepEqual(view.worker.commands.filter(command => command.type === "colorEncoding"),
    [{ type: "colorEncoding", value: "indexed-v1" }]);
  await present(view, { ...metadata, revision: 2, full: true, cells: [cell], colorEncodings: ["indexed-v1"] });
  assert.equal(await view.worker.request("renderedForeground"), 0xff82ae8d,
    "Mounting without palette options uses Hex1b Dark");
  const before = [...view.worker.commands];
  terminal.setColorMode("light");
  await view.worker.request("flush");
  await view.worker.request("draw");
  assert.equal(terminal.colorMode, "light");
  assert.equal(terminal.resolvedColorMode, "light");
  assert.equal(terminal.element.dataset.theme, "light");
  assert.equal(terminal.screenText, "A");
  assert.deepEqual(view.worker.commands, before);
  assert.equal(await view.worker.request("renderedForeground"), light.indexed[2]);
  terminal.setPalette("light", { ...defaultLightPalette, ansi: defaultDarkPalette.ansi });
  await view.worker.request("flush");
  await view.worker.request("draw");
  assert.equal(await view.worker.request("renderedForeground"), dark.indexed[2]);
  assert.deepEqual(view.worker.commands, before);
  assert.throws(() => terminal.setColorMode("auto"), TypeError);
  assert.throws(() => terminal.setColorMode(undefined), TypeError);
  assert.throws(() => terminal.setPalette("light", {}), TypeError);
  assert.equal(terminal.colorMode, "light");
  terminal.dispose();
  assert.throws(() => terminal.setColorMode("dark"), /disposed/u);
});

test("System mode follows media changes, explicit mode wins, and disposal removes the listener", async t => {
  const workers = browser(t);
  const media = Object.assign(new Target(), { matches: true,
    removeEventListener(type, callback) {
      this.listeners.set(type, this.listeners.get(type).filter(item => item !== callback));
    }
  });
  window.matchMedia = () => media;
  const view = await mounting(t, workers, { colorMode: "system" });
  await view.worker.request("open");
  await present(view, {});
  const terminal = await view.promise;
  assert.equal(terminal.resolvedColorMode, "dark");
  media.matches = false;
  media.dispatchEvent({ type: "change" });
  assert.equal(terminal.resolvedColorMode, "light");
  assert.equal(terminal.element.dataset.theme, "light");
  terminal.setColorMode("dark");
  media.dispatchEvent({ type: "change" });
  assert.equal(terminal.element.dataset.theme, "dark");
  assert.equal(view.worker.commands.some(command => command.type === "colorEncoding"), false,
    "Do not send new commands to legacy servers");
  terminal.dispose();
  assert.equal(media.listeners.get("change").length, 0);
});

test("Encoding transitions require a full frame and malformed deltas trigger resync", async t => {
  const workers = browser(t);
  const view = await mounting(t, workers);
  await view.worker.request("open");
  await present(view, {});
  await view.promise;
  await present(view, { ...metadata, revision: 2, full: false, cells: [cell] });
  assert.ok(view.worker.commands.some(command => command.type === "resync"));
  assert.equal(view.handle.stats.discardedFrames ?? 0, 0, "discard metrics publish with a later presentation");
  await present(view, { ...metadata, revision: 3, full: true, cells: [cell] });
  assert.equal(view.handle.stats.revision, 3);
  assert.equal(view.handle.stats.discardedFrames, 1);
});

test("Two views keep independent palettes and palette replacement does not mutate caller data", async t => {
  const workers = browser(t);
  const first = await mounting(t, workers, { colorMode: "dark" });
  await first.worker.request("open");
  await present(first, { ...metadata, cells: [cell] });
  const firstTerminal = await first.promise;
  const second = await mounting(t, workers, { colorMode: "light" });
  await second.worker.request("open");
  await present(second, { ...metadata, cells: [cell] });
  await second.promise;
  assert.equal(await first.worker.request("renderedForeground"), dark.indexed[2]);
  assert.equal(await second.worker.request("renderedForeground"), light.indexed[2]);
  const custom = { ...defaultDarkPalette, ansi: [...defaultDarkPalette.ansi] };
  custom.ansi[2] = "#123456";
  firstTerminal.setPalette("dark", custom);
  custom.ansi[2] = "#ffffff";
  await first.worker.request("flush");
  await first.worker.request("draw");
  assert.equal(await first.worker.request("renderedForeground"), 0xff563412);
  assert.equal(await second.worker.request("renderedForeground"), light.indexed[2]);
});
