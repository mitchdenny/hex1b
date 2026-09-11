import assert from "node:assert/strict";
import { test } from "node:test";
import { Hyperlinks, hyperlinkUri } from "../dist/hyperlinks.js";

test("OSC 8 links allow only absolute web and mail destinations", () => {
  for (const uri of ["https://example.com/docs?a=b#section", "http://localhost:5290/", "mailto:hello@example.com"])
    assert.equal(hyperlinkUri(uri), uri);
  assert.equal(hyperlinkUri("HTTPS://EXAMPLE.COM"), "https://example.com/");
  for (const uri of ["javascript:alert(1)", "JaVaScRiPt:alert(1)", "data:text/html,test",
    "file:///etc/passwd", "vbscript:msgbox(1)", "blob:https://example.com/id", "about:blank",
    "/relative", "//example.com", "", "https://", "https://example.com/\npath",
    "java\tscript:alert(1)", " https://example.com", "https://example.com/a b", "custom:action"])
    assert.equal(hyperlinkUri(uri), null, uri);
});

test("Hyperlink hit testing uses authoritative exclusive ranges, including wide and wrapped cells", () => {
  const links = new Hyperlinks();
  links.update([
    { row: 0, startColumn: 38, endColumn: 40, uri: "https://example.com/docs" },
    { row: 1, startColumn: 0, endColumn: 2, uri: "https://example.com/docs" },
    { row: 1, startColumn: 2, endColumn: 4, uri: "mailto:hello@example.com" },
    { row: 2, startColumn: 0, endColumn: 5, uri: "javascript:alert(1)" }
  ]);
  for (const point of [{ x: 38, y: 0 }, { x: 39, y: 0 }, { x: 0, y: 1 }, { x: 1, y: 1 }])
    assert.equal(links.at(point), "https://example.com/docs");
  assert.equal(links.at({ x: 2, y: 1 }), "mailto:hello@example.com");
  for (const point of [{ x: 37, y: 0 }, { x: 40, y: 0 }, { x: 4, y: 1 }, { x: 0, y: 2 }, { x: 0, y: 3 }])
    assert.equal(links.at(point), null);
});

test("Every presented frame replaces link state, even with no text delta", () => {
  const links = new Hyperlinks();
  const range = { row: 0, startColumn: 0, endColumn: 4, uri: "https://example.com/old" };
  links.update([range]);
  range.uri = "https://example.com/mutated";
  assert.equal(links.at({ x: 0, y: 0 }), "https://example.com/old");
  links.update([range]);
  assert.equal(links.at({ x: 0, y: 0 }), range.uri);
  links.update([]);
  assert.equal(links.at({ x: 0, y: 0 }), null);
});
