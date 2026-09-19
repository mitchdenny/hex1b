import test from "node:test";
import assert from "node:assert/strict";
import { normalizeLinks, linkAction, LINK_LIMITS } from "../.build/link-options.js";

const actions = new Set(["open"]);
const rule = () => ({ id: "test", pattern: /x/gu, kind: "custom", action: "open" });
test("configuration validates atomically and clones regexes/defaults", () => {
  const original = { detection: { rules: [rule()] } };
  original.detection.rules[0].pattern.lastIndex = 8;
  const copy = normalizeLinks(original, actions);
  assert.equal(copy.detection.activation, "modifierClick");
  assert.equal(copy.detection.decoration, "always");
  assert.equal(copy.detection.underlineStyle, "solid");
  assert.equal(copy.detection.rules[0].text, "logicalLine");
  assert.equal(copy.detection.rules[0].enabled, true);
  assert.notEqual(copy.detection.rules[0].pattern, original.detection.rules[0].pattern);
  assert.equal(copy.detection.rules[0].pattern.lastIndex, 0);
  assert.equal(original.detection.rules[0].pattern.lastIndex, 8);
  assert.equal(normalizeLinks(false, actions), false);
  assert.deepEqual(normalizeLinks(undefined, actions), {});
  for (const changed of [
    { id: "" }, { action: "copySelection" }, { enabled: "yes" }, { text: "line" },
    { pattern: /x/y }, { kind: "file" }, { resolve: true }, { builtin: "url" },
    { pattern: new RegExp("x".repeat(LINK_LIMITS.pattern + 1)) },
  ]) assert.throws(() => normalizeLinks({ detection: { rules: [{ ...rule(), ...changed }] } }, actions));
  for (const options of [
    null, true, [], { osc8: {} }, { osc8: { action: "missing" } },
    { detection: null }, { detection: { rules: [rule(), rule()] } },
    { detection: { rules: [rule()], activation: "doubleClick" } },
    { detection: { rules: [rule()], decoration: "red" } },
    { detection: { rules: [rule()], underlineStyle: "wavy" } },
    { detection: { rules: [rule()], underlineStyle: null } },
    { detection: { rules: Array.from({ length: 33 }, (_, i) => ({ ...rule(), id: `${i}` })) } },
  ]) assert.throws(() => normalizeLinks(options, actions));
});

test("underline appearance and visibility are independently configurable", () => {
  for (const decoration of ["always", "hover", "none"]) {
    for (const underlineStyle of ["solid", "dashed"]) {
      const result = normalizeLinks({ detection: { rules: [rule()], decoration, underlineStyle } }, actions);
      assert.equal(result.detection.decoration, decoration);
      assert.equal(result.detection.underlineStyle, underlineStyle);
    }
  }
});

test("linkAction validates activation envelope and retains async return", async () => {
  let received;
  const handler = linkAction(async (context, link, input) => { received = [context, link, input]; return 42; });
  const activation = Object.freeze({ source: "detected", ruleId: "test", kind: "custom",
    text: "x", target: "x", revision: 1, ranges: [{ row: 0, startColumn: 1, endColumn: 2 }] });
  const context = {}, input = { type: "text", text: "x" };
  assert.equal(await handler(context, activation, input), 42);
  assert.deepEqual(received, [context, activation, input]);
  for (const value of [undefined, {}, { ...activation, ranges: [] }, { ...activation, revision: NaN },
    { ...activation, source: "osc8" }, { ...activation, ranges: [{ row: -1, startColumn: 0, endColumn: 1 }] }]) {
    assert.throws(() => handler(context, value, input));
  }
});
