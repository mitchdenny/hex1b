import assert from "node:assert/strict";
import { test } from "node:test";
import { TerminalRenderer } from "../.build/renderer.js";

function render(cellOverrides = {}, mask = new Uint8Array([1]), blink = true, placements = []) {
  const operations = [];
  const renderer = Object.assign(Object.create(TerminalRenderer.prototype), {
    columns: 1, rows: 1, width: 10, height: 20, glyphs: new Map(),
    backend: { submit() {} },
    solid(...args) { operations.push({ type: "solid", args }); },
    placement(value) { operations.push({ type: "image", z: value.z }); }
  });
  const cell = Object.freeze({ index: 0, width: 1, text: "x", attributes: 0, underlineStyle: 0,
    foreground: 0xff804020, background: 0xff000000, underlineColor: 0xff123456, ...cellOverrides });
  renderer.render([cell], { placements, cursor: { visible: false } }, blink, mask);
  return { operations, lines: operations.filter(op => op.type === "solid" && op.args[3] === 1), cell };
}

test("Detected underlines use projected foreground without changing authored cells", () => {
  for (const attributes of [0, 2, 32, 34]) {
    const { lines, cell } = render({ attributes });
    assert.deepEqual(lines, [{ type: "solid", args: [0, 18, 10, 1, [32 / 255, 64 / 255, 128 / 255, 1], undefined] }]);
    assert.equal(cell.attributes, attributes);
    assert.equal(cell.underlineStyle, 0);
    assert.equal(cell.underlineColor, 0xff123456);
  }
  assert.equal(render({}, null).lines.length, 0);
  assert.equal(render({}, new Uint8Array([0])).lines.length, 0);
});

test("Every authored underline style and SGR underline/color takes precedence", () => {
  for (const underlineStyle of [1, 2, 3, 4, 5]) {
    const authored = render({ underlineStyle }, new Uint8Array([0])).operations;
    assert.deepEqual(render({ underlineStyle }).operations, authored);
    assert.deepEqual(render({ underlineStyle }, new Uint8Array([5])).operations, authored);
    for (const line of render({ underlineStyle }).lines)
      assert.deepEqual(line.args[4], [86 / 255, 52 / 255, 18 / 255, 1]);
  }
  assert.deepEqual(render({ attributes: 8 }).operations,
    render({ attributes: 8 }, new Uint8Array([0])).operations);
});

test("Dashed detected underlines use the shared dashed primitive and projected foreground", () => {
  const { lines, cell } = render({}, new Uint8Array([5]));
  const color = [32 / 255, 64 / 255, 128 / 255, 1];
  assert.deepEqual(lines, [
    { type: "solid", args: [0, 18, 3, 1, color, undefined] },
    { type: "solid", args: [5, 18, 3, 1, color, undefined] }
  ]);
  assert.equal(cell.underlineStyle, 0);
  for (const hidden of [{ attributes: 64 }, { width: 0 }, { text: "\u{10eeee}" }])
    assert.equal(render(hidden, new Uint8Array([5])).lines.length, 0);
  assert.equal(render({ attributes: 16 }, new Uint8Array([5]), false).lines.length, 0);
});

test("Hidden, blinking-off, continuation and graphics placeholder cells never gain link underlines", () => {
  for (const cell of [{ attributes: 64 }, { width: 0 }, { text: "\u{10eeee}\u0301" }])
    assert.equal(render(cell).lines.length, 0);
  assert.equal(render({ attributes: 16 }, new Uint8Array([1]), false).lines.length, 0);
  assert.equal(render({ attributes: 16 }).lines.length, 1);
});

test("Local link underline shares text graphics order and retains strike/overline", () => {
  const { operations, lines } = render({ attributes: 128 | 256 }, new Uint8Array([1]), true,
    [{ kind: "kgp", z: 0 }, { kind: "kgp", z: -2 }, { kind: "kgp", z: -1073741825 }]);
  assert.deepEqual(lines.map(line => line.args[1]), [10, 1, 18]);
  assert.deepEqual(operations.map(op => op.type === "image" ? op.z : op.args[1]),
    [-1073741825, 0, -2, 10, 1, 18, 0]);
});
