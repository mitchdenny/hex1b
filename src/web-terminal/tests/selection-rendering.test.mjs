import assert from "node:assert/strict";
import { test } from "node:test";
import { TerminalRenderer } from "../.build/renderer.js";
import { compilePalette, normalizePalette, defaultDarkPalette, defaultLightPalette } from "../.build/terminal-palette.js";

const cell = { index: 0, text: "A", width: 1, attributes: 0, underlineStyle: 1,
  foreground: 0xff0000ff, background: 0xffff0000, underlineColor: 0xff00ff00 };
const range = { row: 0, startColumn: 0, endColumn: 1 };
const rgba = hex => [...hex.slice(1).match(/../gu).map(c => Math.fround(parseInt(c, 16) / 255)), 1];

function render({ cells = [cell], ranges = [range], status = "valid", palette = defaultDarkPalette,
  placements = [], colored = false, blink = true } = {}) {
  let result;
  const renderer = new TerminalRenderer({}, 1, {
    submit(instances, count) {
      result = Array.from({ length: count }, (_, i) => ({
        rect: [...instances.slice(i * 16, i * 16 + 4)],
        uv: [...instances.slice(i * 16 + 4, i * 16 + 8)],
        color: [...instances.slice(i * 16 + 8, i * 16 + 12)],
        mode: instances[i * 16 + 12],
      }));
    }
  }, {});
  Object.assign(renderer, { columns: 4, rows: 2, width: 40, height: 40, atlas: {} });
  for (const c of cells.filter(Boolean))
    renderer.glyphs.set(`${c.attributes & 5}/${c.width}/${c.text}`, { colored, u0: 0, v0: 0, u1: 1, v1: 1 });
  renderer.placement = () => renderer.solid(0, 0, 40, 40, [1, 0, 1, 1]);
  renderer.render(cells, { placements, cursor: { visible: false }, history: { selection: { status, ranges } } },
    blink, undefined, compilePalette(normalizePalette(palette)));
  return result;
}

test("Selection defaults swap palette neutrals and optional overrides validate independently", () => {
  for (const palette of [defaultDarkPalette, defaultLightPalette]) {
    assert.equal(palette.selectionForeground, palette.background);
    assert.equal(palette.selectionBackground, palette.foreground);
    const { selectionForeground, selectionBackground, ...withoutSelection } = palette;
    assert.deepEqual(compilePalette(withoutSelection), compilePalette(palette));
    const custom = compilePalette(normalizePalette({ ...withoutSelection, selectionForeground: "#123456" }));
    assert.equal(custom.selectionForeground, 0xff563412);
    assert.equal(custom.selectionBackground, compilePalette(palette).foreground);
  }
});

test("Opaque selected background, glyph, and underline override RGB, reverse, and dim without mutating cells", () => {
  for (const palette of [defaultDarkPalette, defaultLightPalette,
    { ...defaultDarkPalette, selectionForeground: "#123456", selectionBackground: "#abcdef" }]) {
    for (const attributes of [0, 2, 32, 34]) {
      const source = Object.freeze({ ...cell, attributes });
      const quads = render({ cells: [source], palette });
      assert.deepEqual(quads.slice(-3).map(q => q.color), [
        rgba(palette.selectionBackground), rgba(palette.selectionForeground), rgba(palette.selectionForeground),
      ]);
      assert.deepEqual(quads.at(-3).rect, [0, 0, 10, 20]);
      assert.equal(source.foreground, cell.foreground);
      assert.equal(source.background, cell.background);
    }
  }
});

test("Inactive selections restore ordinary cell colors and selection covers blanks above image layers", () => {
  for (const status of ["none", "pending", "invalidated", "unavailable"]) {
    const quads = render({ status });
    assert.equal(quads.length, 3);
    assert.deepEqual(quads[1].color, rgba("#ff0000"));
  }
  const quads = render({ cells: [], placements: [{ kind: "kgp", z: 1 }],
    ranges: [{ row: 1, startColumn: 1, endColumn: 3 }] });
  assert.deepEqual(quads.at(-1).rect, [10, 20, 20, 20]);
  assert.deepEqual(quads.at(-1).color, rgba(defaultDarkPalette.selectionBackground));
});

test("Wide glyphs and decorations clip to a selection starting on their continuation", () => {
  const quads = render({ cells: [{ ...cell, width: 2, attributes: 128 | 256 },
    { ...cell, index: 1, text: "", width: 0 }],
  ranges: [{ ...range, startColumn: 1, endColumn: 2 }] });
  const selected = quads.slice(-5);
  assert.deepEqual(selected.map(q => q.rect), [
    [10, 0, 10, 20], [10, 0, 10, 20], [10, 10, 10, 1], [10, 1, 10, 1], [10, 18, 10, 1],
  ]);
  assert.deepEqual(selected[1].uv, [.5, 0, 1, 1]);
});

test("Selection does not reveal concealed/blinking text and preserves colored glyph pixels", () => {
  for (const attributes of [64, 16]) {
    const quads = render({ cells: [{ ...cell, attributes }], blink: false });
    assert.equal(quads.length, 2);
    assert.ok(quads.every(q => q.mode === 0));
  }
  const quads = render({ colored: true });
  assert.equal(quads.at(-2).mode, 2);
  assert.deepEqual(quads.at(-2).color, [1, 1, 1, 1]);
});
