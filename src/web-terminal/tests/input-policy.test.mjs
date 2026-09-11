import assert from "node:assert/strict";
import { test } from "node:test";
import { InputPolicy, InputRoute, TerminalAction, defaultInputBindings, inputModifiers } from "../dist/input-policy.js";
import { assertCommandSize, LIMITS } from "../dist/protocol.js";

const key = (key, overrides = {}) => ({ type: "key", key, ctrl: false, alt: false, shift: false, meta: false, ...overrides });
const pointer = (overrides = {}) => ({ type: "pointer", button: "right", ...inputModifiers({}), ...overrides });
const context = (overrides = {}) => ({
  mouseCaptured: false, historical: false, readOnly: false, buffer: "main", ...overrides
});

test("Input size preflight counts UTF-8 and JSON escaping, accepting exactly the wire limit", () => {
  const encoder = new TextEncoder();
  const overhead = encoder.encode(JSON.stringify({ type: "paste", text: "" })).byteLength;
  const exact = { type: "paste", text: "a".repeat(LIMITS.commandBytes - overhead) };
  assert.doesNotThrow(() => assertCommandSize(exact));
  assert.throws(() => assertCommandSize({ ...exact, text: exact.text + "a" }), RangeError);
  for (const text of ["\u754c".repeat(22000), "\n".repeat(33000), "\u0000".repeat(11000)]) {
    assert.ok(text.length < LIMITS.commandBytes);
    assert.throws(() => assertCommandSize({ type: "paste", text }), RangeError);
  }
});

test("Default shortcuts preserve terminal Ctrl+C and browser-native paste", () => {
  const policy = new InputPolicy();
  assert.equal(policy.resolve(key("c", { ctrl: true }), context()).route, InputRoute.Application);
  for (const input of [key("c", { ctrl: true, shift: true }), key("c", { meta: true })])
    assert.equal(policy.resolve(input, context()).action, TerminalAction.CopySelection);
  for (const input of [key("v", { ctrl: true }), key("v", { ctrl: true, shift: true }),
    key("v", { meta: true }), key("F5"), key("l", { ctrl: true })])
    assert.equal(policy.resolve(input, context()).route, InputRoute.Browser);
  assert.equal(policy.resolve(key("x"), context()).route, InputRoute.Continue);
  assert.equal(policy.resolve(key("Enter"), context()).route, InputRoute.Application);
});

test("Windows-style context click applies in both screens, with Shift capture override", () => {
  const policy = new InputPolicy();
  for (const buffer of ["main", "alternate"]) {
    assert.equal(policy.resolve(pointer(), context({ buffer })).action, TerminalAction.CopyOrPaste);
    assert.equal(policy.resolve(pointer(), context({ buffer, mouseCaptured: true })).route, InputRoute.Continue);
    assert.equal(policy.resolve(pointer({ shift: true }), context({ buffer, mouseCaptured: true })).action,
      TerminalAction.CopyOrPaste);
    for (const override of [{ historical: true }, { readOnly: true }])
      assert.equal(policy.resolve(pointer(), context({ buffer, mouseCaptured: true, ...override })).action,
        TerminalAction.CopyOrPaste);
  }
});

test("Matching IDs replace defaults, explicit removal disables them, and views stay independent", () => {
  const overrides = [
    { id: "clipboard.copy-key", remove: true },
    { id: "clipboard.context-click", match: input => input.type === "pointer", action: "host.menu" }
  ];
  const policy = new InputPolicy({ inputBindings: overrides, actions: { "host.menu": () => {} } });
  overrides[1].action = "unknown";
  assert.equal(policy.resolve(pointer(), context()).action, "host.menu");
  assert.equal(policy.resolve(key("c", { ctrl: true, shift: true }), context()).route, InputRoute.Browser);
  assert.equal(new InputPolicy().resolve(pointer(), context()).action, TerminalAction.CopyOrPaste);
  assert.equal(defaultInputBindings().find(binding => binding.id === "clipboard.context-click").action,
    TerminalAction.CopyOrPaste);
  policy.bindings[0].action = "changed";
  assert.equal(policy.resolve(pointer(), context()).action, "host.menu");
});

test("Custom bindings take priority and when predicates can distinguish alternate screen", () => {
  const callback = () => {};
  const policy = new InputPolicy({ inputBindings: [
    { id: "host.forward-clear", match: input => input.type === "key" && input.key === "l", route: InputRoute.Application },
    { id: "host.alt-menu", match: input => input.type === "pointer", when: ctx => ctx.buffer === "alternate", action: callback },
    { id: "host.wheel", match: input => input.type === "wheel", action: TerminalAction.ScrollLines, args: -5 }
  ] });
  assert.equal(policy.resolve(key("l", { ctrl: true }), context()).route, InputRoute.Application);
  assert.equal(policy.resolve(pointer(), context({ buffer: "alternate" })).action, callback);
  assert.equal(policy.resolve(pointer(), context()).action, TerminalAction.CopyOrPaste);
  assert.deepEqual(policy.resolve({ type: "wheel" }, context()), { action: TerminalAction.ScrollLines, args: -5 });
});

test("Interception explicitly routes input before any bindings, without executing actions", () => {
  for (const result of ["consume", "application", "browser", { action: "copySelection", args: { clear: true } }]) {
    let matched = false;
    const policy = new InputPolicy({
      onInput: () => result,
      inputBindings: [{ id: "host.test", match: () => { matched = true; return true; }, route: "consume" }]
    });
    assert.deepEqual(policy.resolve(key("Enter"), context()), typeof result === "string" ? { route: result } : result);
    assert.equal(matched, false);
  }
  for (const result of [undefined, "continue", { route: "continue" }]) {
    const policy = new InputPolicy({ onInput: () => result });
    assert.equal(policy.resolve(key("Enter"), context()).route, InputRoute.Application);
  }
});

test("Continue falls through other bindings; committed text and paste remain separate intents", () => {
  const observed = [];
  const policy = new InputPolicy({
    onInput: input => { observed.push(input.type); },
    inputBindings: [{ id: "host.observe", match: () => true, route: InputRoute.Continue }]
  });
  for (const type of ["text", "paste"]) assert.equal(policy.resolve({ type, text: "value" }, context()).route, "continue");
  assert.deepEqual(observed, ["text", "paste"]);
});

test("Invalid configuration fails rather than leaving silently ignored bindings", () => {
  for (const options of [
    { inputBindings: {} }, { actions: [] }, { actions: { copySelection: () => {} } },
    { actions: { "host.bad": 1 } }, { onInput: async () => "consume" },
    { inputBindings: [{ id: "unknown", remove: true }] },
    { inputBindings: [{ id: "clipboard.copy-key", remove: true, action: "copySelection" }] },
    { inputBindings: [{ id: "a", match: () => true, action: "unknown" }] },
    { inputBindings: [{ id: "a", match: () => true, route: "unknown" }] },
    { inputBindings: [{ id: "a", match: () => true, route: "consume", action: "copySelection" }] },
    { inputBindings: [{ id: "a", match: async () => true, route: "consume" }] },
    { inputBindings: [{ id: "a", match: () => true, when: async () => true, route: "consume" }] },
    { inputBindings: [{ id: "a", match: () => true, route: "consume" }, { id: "a", match: () => false, route: "browser" }] }
  ]) assert.throws(() => new InputPolicy(options), TypeError);
});

test("Invalid synchronous hook results throw before input can fall through to the application", () => {
  for (const result of [false, null, "unknown", Promise.resolve("consume"), { action: "unknown" }]) {
    const policy = new InputPolicy({ onInput: () => result });
    assert.throws(() => policy.resolve(key("Enter"), context()), TypeError);
  }
  for (const property of ["match", "when"]) {
    const policy = new InputPolicy({ inputBindings: [{
      id: "host.invalid", match: () => true, [property]: () => 1, route: "consume"
    }] });
    assert.throws(() => policy.resolve(key("Enter"), context()), /boolean/);
  }
});
