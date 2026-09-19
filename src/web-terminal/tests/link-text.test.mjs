import test from "node:test";
import assert from "node:assert/strict";
import { extractLinkText, mapLinkRange } from "../.build/link-text.js";

const cell = (text, index, width = 1, attributes = 0) => ({
  text, index, width, attributes, foreground: 0, background: 0, underlineColor: 0, underlineStyle: 0,
});
function snapshot(lines, soft = []) {
  const columns = lines[0].length;
  return { revision: 1, columns, rows: lines.length, hyperlinks: [],
    cells: lines.flatMap((line, row) => Array.from(line, (char, col) =>
      cell(char, row * columns + col, 1, soft.includes(row) && col === columns - 1 ? 1024 : 0))) };
}
test("text modes preserve spaces, soft-wrap topology and hard newlines", () => {
  const source = snapshot([" abc", "def ", " ghi"], [0]);
  assert.deepEqual(extractLinkText(source, "physicalRow").map(x => x.chunk.text), [" abc", "def ", " ghi"]);
  assert.deepEqual(extractLinkText(source, "logicalLine").map(x => x.chunk.text), [" abcdef ", " ghi"]);
  assert.deepEqual(extractLinkText(source, "viewport").map(x => x.chunk.text), [" abcdef \n ghi"]);
  const logical = extractLinkText(source, "logicalLine")[0];
  assert.deepEqual(mapLinkRange(logical, 1, 6), [
    { row: 0, startColumn: 1, endColumn: 4 }, { row: 1, startColumn: 0, endColumn: 3 },
  ]);
  const spaces = extractLinkText(snapshot([" ab ", " cd "], [0]), "logicalLine")[0];
  assert.equal(spaces.chunk.text, " ab  cd ");
});
test("unknown first and cropped right edges reject incomplete candidates", () => {
  const physical = extractLinkText(snapshot(["abcd", "efgh"], [0]), "physicalRow");
  assert.equal(physical[0].chunk.start, "unknown");
  assert.equal(physical[0].chunk.end, "clipped");
  assert.equal(physical[1].chunk.start, "clipped");
  assert.equal(mapLinkRange(physical[0], 0, 2), null);
  assert.equal(mapLinkRange(physical[0], 2, 2), null);
  assert.equal(mapLinkRange(physical[1], 0, 2), null);
  assert.equal(mapLinkRange(physical[1], 2, 2), null);
  assert.ok(mapLinkRange(physical[1], 1, 1));
});
test("only the final cell SoftWrap flag joins rows", () => {
  const source = snapshot([" abc", "def "]);
  source.cells[1].attributes = 1024;
  assert.deepEqual(extractLinkText(source, "logicalLine").map(mapped => mapped.chunk.text), [" abc", "def "]);
  assert.equal(extractLinkText(source, "physicalRow")[1].chunk.start, "complete");
  assert.equal(extractLinkText(source, "viewport")[0].chunk.text, " abc\ndef ");
  source.cells[3].attributes = 1024;
  assert.deepEqual(extractLinkText(source, "logicalLine").map(mapped => mapped.chunk.text), [" abcdef "]);
  assert.equal(extractLinkText(source, "physicalRow")[1].chunk.start, "clipped");
});
test("wide cells, combining graphemes and emoji map only whole visible cells", () => {
  const source = { revision: 1, columns: 7, rows: 1, hyperlinks: [], cells: [
    cell(" ", 0), cell("漢", 1, 2), cell("", 2, 0), cell("é", 3), cell("👩‍💻", 4, 2),
    cell("", 5, 0), cell(" ", 6),
  ] };
  const mapped = extractLinkText(source, "logicalLine")[0];
  assert.equal(mapped.chunk.text, " 漢é👩‍💻 ");
  assert.deepEqual(mapLinkRange(mapped, 1, 8), [{ row: 0, startColumn: 1, endColumn: 6 }]);
  assert.equal(mapLinkRange(mapped, 2, 1), null);
  assert.equal(mapLinkRange(mapped, 4, 2), null);
  const split = snapshot([" á "]);
  split.columns = split.cells.length;
  assert.equal(mapLinkRange(extractLinkText(split, "logicalLine")[0], 1, 1), null);
});
test("hidden cells, placeholders, orphan continuations and cropped wide cells are barriers", () => {
  for (const replacement of [cell("X", 2, 1, 64), cell("\u{10eeee}", 2), cell("", 2, 0)]) {
    const source = snapshot([" abcd "]);
    source.cells[2] = replacement;
    const mapped = extractLinkText(source, "logicalLine")[0];
    assert.ok(mapped.chunk.text.includes("\0"));
    assert.equal(mapLinkRange(mapped, 1, 4), null);
    assert.equal(mapLinkRange(mapped, 1, 1), null);
  }
  const source = snapshot([" ab漢"]);
  source.cells[3].width = 2;
  assert.equal(extractLinkText(source, "logicalLine")[0].chunk.text, " ab\0");
});
test("oversized chunks are rejected rather than split into seemingly complete lines", () => {
  const source = snapshot([" "]);
  source.cells[0].text = "x".repeat(65537);
  assert.throws(() => extractLinkText(source, "logicalLine"), /limit/u);
});
