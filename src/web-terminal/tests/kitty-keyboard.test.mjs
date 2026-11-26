import assert from "node:assert/strict";
import { test } from "node:test";
import { decodeFrame } from "../.build/protocol.js";
import { browser, frame, mounting, present } from "./fixtures/browser.mjs";

function emit(target, type, data = {}) {
  const Type = type === "input" ? globalThis.InputEvent : Event;
  const event = new Type(type, { cancelable: true, bubbles: true });
  const locks = { CapsLock: data.capsLock ?? false, NumLock: data.numLock ?? false, AltGraph: data.altGraph ?? false };
  Object.assign(event, {
    key: "a", code: "KeyA", repeat: false, isComposing: false, ctrlKey: false, altKey: false, shiftKey: false,
    metaKey: false, getModifierState: name => locks[name] ?? false, composedPath: () => [target], ...data
  });
  target.dispatchEvent(event);
  return event;
}

async function kitty(t, flags, options = {}) {
  const workers = browser(t, { InputEvent: class extends Event {} });
  const view = await mounting(t, workers, options);
  await view.worker.request("open");
  await present(view, flags === undefined ? {} : { kittyKeyboardFlags: flags });
  const terminal = await view.promise;
  const input = terminal.element.shadowRoot.querySelector("textarea");
  const sent = async () => {
    await view.worker.request("flush");
    return view.worker.commands.filter(command => ["input", "key", "paste"].includes(command.type));
  };
  return { view, terminal, input, sent };
}

function key(key, code, eventType = "press", modifiers = {}) {
  return { type: "key", key, code, eventType, ctrl: false, alt: false, shift: false, meta: false,
    capsLock: false, numLock: false, ...modifiers };
}

for (const flags of [undefined, 0, 96]) {
  test(`Legacy key commands are unchanged with Kitty flags ${flags}`, async t => {
    const { terminal, input, sent } = await kitty(t, flags);
    emit(terminal.element, "keydown", { key: "Enter", code: "Enter" });
    emit(terminal.element, "keydown", { key: "F13", code: "F13" });
    emit(terminal.element, "keyup", { key: "Enter", code: "Enter" });
    const meta = emit(terminal.element, "keydown", { key: "k", code: "KeyK", metaKey: true });
    input.value = "x";
    emit(input, "input", { data: "x", inputType: "insertText" });
    assert.equal(meta.defaultPrevented, false, "Meta shortcuts stay with the browser");
    assert.deepEqual(await sent(), [
      { type: "key", key: "Enter", ctrl: false, alt: false, shift: false },
      { type: "input", text: "x" }
    ]);
  });
}

test("Kitty metadata adds physical code, event type and lock state to forwarded keys", async t => {
  const { terminal, sent } = await kitty(t, 1);
  emit(terminal.element, "keydown", { key: "Enter", code: "NumpadEnter", numLock: true });
  emit(terminal.element, "keydown", { key: "a", code: "KeyA", ctrlKey: true, capsLock: true, repeat: true });
  assert.deepEqual(await sent(), [
    key("Enter", "NumpadEnter", "press", { numLock: true }),
    key("a", "KeyA", "repeat", { ctrl: true, capsLock: true })
  ]);
});

test("Kitty modes route reported keys to the terminal while browser shortcuts stay with the browser", async t => {
  const { terminal, sent } = await kitty(t, 1);
  const f13 = emit(terminal.element, "keydown", { key: "F13", code: "F13" });
  const pad = emit(terminal.element, "keydown", { key: "1", code: "Numpad1" });
  const text = emit(terminal.element, "keydown", { key: "a", code: "KeyA" });
  const reload = emit(terminal.element, "keydown", { key: "F5", code: "F5" });
  const tab = emit(terminal.element, "keydown", { key: "t", code: "KeyT", ctrlKey: true });
  const alt = emit(terminal.element, "keydown", { key: "a", code: "KeyA", altKey: true });
  assert.equal(f13.defaultPrevented, true);
  assert.equal(pad.defaultPrevented, true);
  assert.equal(text.defaultPrevented, false, "flag 1 text keys still arrive through text input");
  assert.equal(reload.defaultPrevented, false);
  assert.equal(tab.defaultPrevented, false);
  assert.equal(alt.defaultPrevented, false);
  assert.deepEqual(await sent(), [key("F13", "F13"), key("1", "Numpad1")]);
});

test("Report-all forwards text and modifier keys as key events, but not AltGr, dead or IME keys", async t => {
  const { terminal, sent } = await kitty(t, 8);
  const text = emit(terminal.element, "keydown", { key: "a", code: "KeyA" });
  emit(terminal.element, "keydown", { key: "Shift", code: "ShiftLeft", shiftKey: true });
  emit(terminal.element, "keydown", { key: "Shift", code: "" , shiftKey: true });
  const altGr = emit(terminal.element, "keydown", { key: "@", code: "KeyQ", ctrlKey: true, altKey: true, altGraph: true });
  const dead = emit(terminal.element, "keydown", { key: "Dead", code: "BracketLeft" });
  const ime = emit(terminal.element, "keydown", { key: "Process", code: "KeyA" });
  assert.equal(text.defaultPrevented, true);
  assert.equal(altGr.defaultPrevented, false);
  assert.equal(dead.defaultPrevented, false);
  assert.equal(ime.defaultPrevented, false);
  assert.deepEqual(await sent(), [key("a", "KeyA"), key("Shift", "ShiftLeft", "press", { shift: true })]);
});

test("Releases are sent only for tracked presses, with the release's modifiers", async t => {
  const { terminal, sent } = await kitty(t, 2);
  emit(terminal.element, "keydown", { key: "a", code: "KeyA" });
  const up = emit(terminal.element, "keyup", { key: "A", code: "KeyA", shiftKey: true });
  emit(terminal.element, "keyup", { key: "b", code: "KeyB" });
  emit(terminal.element, "keyup", { key: "F5", code: "F5" });
  emit(terminal.element, "keyup", { key: "a", code: "KeyA" });
  assert.equal(up.defaultPrevented, true);
  assert.deepEqual(await sent(), [key("a", "KeyA"), key("a", "KeyA", "release", { shift: true })]);
});

test("Held-key limit rejects new presses before sending, allows existing keys, and frees capacity on release", async t => {
  const errors = [];
  const { terminal, sent } = await kitty(t, 10, { onInputError: error => errors.push(error) });
  const keys = Array.from({ length: 256 }, (_, i) => String.fromCodePoint(0x3400 + i));
  for (const value of keys) emit(terminal.element, "keydown", { key: value, code: "" });
  assert.deepEqual(await sent(), keys.map(value => key(value, "")));

  // A 257th distinct identity is rejected before sending, so its release is never forwarded.
  emit(terminal.element, "keydown", { key: "\u3500", code: "" });
  emit(terminal.element, "keydown", { key: "\u3500", code: "", repeat: true });
  emit(terminal.element, "keyup", { key: "\u3500", code: "" });
  assert.equal(errors.length, 2);
  for (const error of errors) assert.match(error.message, /limit of 256 held keys/);
  assert.deepEqual(await sent(), keys.map(value => key(value, "")));

  emit(terminal.element, "keydown", { key: keys[0], code: "", repeat: true });
  emit(terminal.element, "keydown", { key: keys[0], code: "" });
  emit(terminal.element, "keyup", { key: keys[0], code: "" });
  emit(terminal.element, "keydown", { key: "\u3500", code: "" });
  emit(terminal.element, "keyup", { key: "\u3500", code: "" });
  assert.deepEqual((await sent()).slice(256), [
    key(keys[0], "", "repeat"), key(keys[0], ""), key(keys[0], "", "release"),
    key("\u3500", ""), key("\u3500", "", "release")
  ]);
  terminal.setReadOnly(true);
  assert.deepEqual((await sent()).slice(261), keys.slice(1).map(value => key(value, "", "release")));
  assert.equal(errors.length, 2);
});

test("Held keys are released on focus loss and read-only, and forgotten on disconnect or mode drop", async t => {
  const { view, terminal, sent } = await kitty(t, 2);
  emit(terminal.element, "keydown", { key: "a", code: "KeyA", capsLock: true });
  emit(terminal.element, "keydown", { key: "Enter", code: "Enter", ctrlKey: true, capsLock: true });
  emit(terminal.element, "focusout", { relatedTarget: null });
  emit(terminal.element, "keydown", { key: "b", code: "KeyB" });
  terminal.setReadOnly(true);
  terminal.setReadOnly(false);
  emit(terminal.element, "keyup", { key: "b", code: "KeyB" });
  emit(terminal.element, "keydown", { key: "c", code: "KeyC" });
  await present(view, { revision: 2, kittyKeyboardFlags: 1 });
  await present(view, { revision: 3, kittyKeyboardFlags: 2 });
  emit(terminal.element, "keyup", { key: "c", code: "KeyC" });
  assert.deepEqual(await sent(), [
    key("a", "KeyA", "press", { capsLock: true }),
    key("Enter", "Enter", "press", { ctrl: true, capsLock: true }),
    key("a", "KeyA", "release", { capsLock: true }),
    key("Enter", "Enter", "release", { capsLock: true }),
    key("b", "KeyB"),
    key("b", "KeyB", "release"),
    key("c", "KeyC")
  ]);
});

test("Kitty keyboard state resets when the connection closes", async t => {
  const { view, terminal, sent } = await kitty(t, 2);
  emit(terminal.element, "keydown", { key: "a", code: "KeyA" });
  await view.worker.request("close");
  await view.worker.request("open");
  await present(view, { revision: 1 });
  emit(terminal.element, "keyup", { key: "a", code: "KeyA" });
  emit(terminal.element, "keydown", { key: "Enter", code: "Enter" });
  assert.deepEqual((await sent()).slice(-1), [{ type: "key", key: "Enter", ctrl: false, alt: false, shift: false }]);
  assert.equal((await sent()).some(command => command.eventType === "release"), false);
});

test("A composition commit is sent once, while later same-value typing is still sent", async t => {
  const workers = browser(t, { InputEvent: class extends Event {} });
  t.mock.timers.enable({ apis: ["setTimeout"] });
  const view = await mounting(t, workers);
  await view.worker.request("open");
  await present(view, { kittyKeyboardFlags: 1 });
  const terminal = await view.promise;
  const input = terminal.element.shadowRoot.querySelector("textarea");
  emit(input, "compositionstart");
  emit(input, "compositionend", { data: "日本" });
  t.mock.timers.tick(1);
  emit(input, "input", { data: "日本", inputType: "insertFromComposition" });
  emit(input, "input", { data: "日本", inputType: "insertText" });
  emit(input, "compositionstart");
  emit(input, "compositionend", { data: "語" });
  emit(input, "input", { data: "語", inputType: "insertCompositionText" });
  t.mock.timers.tick(1);
  await view.worker.request("flush");
  assert.deepEqual(view.worker.commands.filter(command => command.type === "input"), [
    { type: "input", text: "日本" }, { type: "input", text: "日本" }, { type: "input", text: "語" }
  ]);
});

test("Paste remains a paste command under report-all", async t => {
  const { input, sent } = await kitty(t, 31);
  emit(input, "paste", { clipboardData: { getData: () => "pasted" } });
  assert.deepEqual(await sent(), [{ type: "paste", text: "pasted" }]);
});

test("Frame metadata accepts only integer Kitty flags within the low seven bits", () => {
  assert.equal(decodeFrame(frame({ kittyKeyboardFlags: 127 })).metadata.kittyKeyboardFlags, 127);
  assert.equal(decodeFrame(frame({})).metadata.kittyKeyboardFlags, undefined);
  for (const flags of [-1, 128, 1.5, "1", null])
    assert.throws(() => decodeFrame(frame({ kittyKeyboardFlags: flags })), undefined, String(flags));
});
