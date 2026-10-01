import assert from "node:assert/strict";
import { test } from "node:test";
import { TerminalRenderer } from "../.build/renderer.js";
import { compilePalette, normalizePalette, defaultDarkPalette, defaultLightPalette } from "../.build/terminal-palette.js";

const cell = { index: 0, text: "A", width: 1, attributes: 0, underlineStyle: 1,
  foreground: 0xff0000ff, background: 0xffff0000, underlineColor: 0xff00ff00 };
const range = { row: 0, startColumn: 0, endColumn: 1 };
const rgba = hex => [...hex.slice(1).match(/../gu).map(c => Math.fround(parseInt(c, 16) / 255)), 1];

function render({ cells = [cell], ranges = [range], status = "valid", palette = defaultDarkPalette,
  placements = [], colored = false, blink = true, lineRenditions, cursor = { visible: false },
  linkDecorations, columns = 4 } = {}) {
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
  Object.assign(renderer, { columns, rows: 2, width: columns * 10, height: 40, atlas: {} });
  for (const c of cells.filter(Boolean))
    renderer.glyphs.set(`${c.attributes & 5}/${c.width}/${c.text}`, { colored, u0: 0, v0: 0, u1: 1, v1: 1 });
  renderer.placement = () => renderer.solid(0, 0, 40, 40, [1, 0, 1, 1]);
  renderer.render(cells, { placements, lineRenditions, cursor, history: { selection: { status, ranges } } },
    blink, linkDecorations, compilePalette(normalizePalette(palette)));
  return result;
}

test("DEC double-height halves scale glyphs and crop matching texture halves to independent rows", () => {
  const cells = Array.from({ length: 8 }, (_, index) => ({
    ...cell, index, text: index === 0 || index === 4 ? "A" : " ", underlineStyle: 0
  }));
  const quads = render({ cells, ranges: [], lineRenditions: [2, 3] });
  const glyphs = quads.filter(q => q.mode === 1);
  assert.deepEqual(glyphs[0].rect, [0, 0, 20, 20]);
  assert.deepEqual(glyphs[0].uv, [0, 0, 1, .5]);
  assert.deepEqual(glyphs[2].rect, [0, 20, 20, 20]);
  assert.deepEqual(glyphs[2].uv, [0, .5, 1, 1]);
  assert.equal(glyphs.length, 4, "The inaccessible right half must not render");
});

test("DEC selection and wide Unicode glyphs use scaled logical columns", () => {
  const quads = render({ cells: [{ ...cell, width: 2, underlineStyle: 0 }, { ...cell, index: 1, text: "", width: 0 }],
    ranges: [{ row: 0, startColumn: 1, endColumn: 2 }], lineRenditions: [1, 0] });
  assert.deepEqual(quads.at(-2).rect, [20, 0, 20, 20]);
  assert.deepEqual(quads.at(-1).rect, [20, 0, 20, 20]);
  assert.deepEqual(quads.at(-1).uv, [.5, 0, 1, 1]);
});

test("DEC double-height underlines appear only in the bottom half", () => {
  const top = render({ ranges: [], lineRenditions: [2, 0] });
  const bottom = render({ ranges: [], lineRenditions: [3, 0] });
  assert.equal(top.length, 2);
  assert.deepEqual(bottom.at(-1).rect, [0, 16, 20, 2]);
});

test("DEC decorations, colored glyphs and links remain clipped to their physical half", () => {
  for (const mode of [1, 2, 3]) for (const style of [1, 2, 3, 4, 5]) {
    const quads = render({ cells: [{ ...cell, text: "👩‍💻", width: 2, underlineStyle: style,
      attributes: 128 | 256 }], colored: true, ranges: [], lineRenditions: [mode, 0] });
    assert.ok(quads.some(q => q.mode === 2));
    assert.ok(quads.every(q => q.rect[1] >= 0 && q.rect[1] + q.rect[3] <= 20));
    const links = render({ cells: [{ ...cell, underlineStyle: 0 }], ranges: [],
      lineRenditions: [mode, 0], linkDecorations: new Uint8Array([style]) });
    if (mode === 2) assert.equal(links.length, 2, "Top half cannot show an underline");
    else assert.ok(links.length > 2);
  }
});

test("DEC cursor shapes occupy one scaled logical column, independently of glyph height", () => {
  for (const mode of [1, 2, 3]) for (const shape of [2, 4, 6]) {
    const quads = render({ cells: [], ranges: [], lineRenditions: [mode, 0],
      cursor: { visible: true, x: 1, y: 0, shape } });
    assert.deepEqual(quads.at(-1).rect, shape === 4 ? [20, 18, 20, 2] :
      shape === 6 ? [20, 0, 2, 20] : [20, 0, 20, 20]);
  }
});

test("DEC odd viewport and one-column viewport keep quads within the physical canvas", () => {
  for (const columns of [1, 3, 5]) {
    const cells = Array.from({ length: columns }, (_, index) => ({ ...cell, index }));
    const quads = render({ cells, ranges: [], columns, lineRenditions: [2, 0] });
    assert.ok(quads.every(q => q.rect[0] + q.rect[2] <= columns * 10));
    assert.equal(quads.filter(q => q.mode === 1).length, Math.max(1, Math.floor(columns / 2)));
  }
});

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
