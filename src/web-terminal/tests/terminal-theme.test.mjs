import assert from "node:assert/strict";
import { test } from "node:test";
import { terminalThemeCss } from "../dist/terminal-theme.js";
import { WebTerminal } from "../dist/web-terminal.js";

test("Standalone component preserves the extracted light and dark Clawpilot colors", () => {
  const colors = {
    surface: ["#ffffff", "#292929"],
    text: ["#242424", "#dedede"],
    "text-muted": ["#5c5c5c", "#919191"],
    "border-strong": ["#919191", "#5f5f5f"],
    accent: ["#b11f4b", "#fd8ea1"],
    "accent-soft": ["rgba(177, 31, 75, 0.08)", "rgba(253, 142, 161, 0.14)"],
    danger: ["#dc2626", "#f87171"]
  };
  for (const [name, values] of Object.entries(colors)) {
    assert.equal(values.length, 2, `${name} must have light and dark palettes`);
    for (const value of values) assert.ok(terminalThemeCss.includes(`--cp-terminal-${name}: ${value};`));
    assert.ok(terminalThemeCss.includes(`--cp-view-${name}: var(--cp-${name}, var(--cp-terminal-${name}));`));
    assert.ok(!terminalThemeCss.includes(`--cp-${name}:`), "Do not shadow inherited embedding theme tokens");
  }
  assert.match(terminalThemeCss, /@media \(prefers-color-scheme: dark\)/u);
  assert.match(terminalThemeCss, /:host-context\(\[data-theme="light"\]\)/u);
  assert.match(terminalThemeCss, /:host-context\(\[data-theme="dark"\]\)/u);
});

test("Inspection font has an embedding override and the Clawpilot default stack", () => {
  assert.ok(terminalThemeCss.includes('--cp-terminal-font-family: "Segoe UI", Aptos, Calibri, -apple-system, BlinkMacSystemFont, sans-serif;'));
  assert.ok(terminalThemeCss.includes("--cp-view-font-family: var(--cp-font-family, var(--cp-terminal-font-family));"));
});

test("A newly constructed view follows live before any worker history response", () => {
  const original = globalThis.document;
  globalThis.document = { createElement: () => ({ style: {} }) };
  try {
    const terminal = new WebTerminal({});
    assert.deepEqual(terminal.viewport, {
      available: false, following: true, pending: false, followTail: true, offset: 0
    });
    assert.equal(terminal.selection.active, false);
    assert.equal(terminal.selection.pending, false);
    assert.equal(terminal.selection.status, "unavailable");
  } finally {
    if (original === undefined) delete globalThis.document;
    else globalThis.document = original;
  }
});
