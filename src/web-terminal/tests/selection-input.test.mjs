import assert from "node:assert/strict";
import { test } from "node:test";
import { cellPoint, WheelAccumulator, SelectionGesture } from "../dist/selection-input.js";
import { captureMouse } from "../dist/mouse-input.js";

const point = { x: 4, y: 3 };
const local = { tracking: 0, historical: false, readOnly: false };
const captured = { ...local, tracking: 1003 };

test("Selection granularity follows click count and Option rectangle", () => {
  for (const [event, mode] of [
    [{ detail: 1 }, "character"], [{ detail: 2 }, "word"],
    [{ detail: 3 }, "line"], [{ detail: 3, altKey: true }, "rectangle"]
  ]) {
    const gesture = new SelectionGesture();
    assert.deepEqual(gesture.begin({ button: 0, ...event }, point, local), { owner: "local", mode, extend: false });
  }
});

test("Shift extends an existing selection without changing anchor granularity", () => {
  const gesture = new SelectionGesture();
  assert.deepEqual(gesture.begin({ button: 0, shiftKey: true, altKey: true }, point,
    { ...captured, selection: { status: "valid", mode: "word" } }),
  { owner: "local", mode: "word", extend: true });
  gesture.end();
  assert.equal(gesture.begin({ button: 0, shiftKey: true, altKey: true }, point,
    { ...captured, selection: { status: "expired", mode: "word" } }).mode, "rectangle");
});

test("Shift-click starts anew while a prior selection clear awaits acknowledgement", () => {
  const gesture = new SelectionGesture();
  assert.deepEqual(gesture.begin({ button: 0, shiftKey: true }, point, {
    ...captured, selection: { status: "pending", mode: "word", canExtend: false }
  }), { owner: "local", mode: "character", extend: false });
});

test("Shift local ownership survives modifier release until mouseup", () => {
  const gesture = new SelectionGesture();
  gesture.begin({ button: 0, shiftKey: true }, point, captured);
  assert.equal(gesture.wheelOwner({}, captured), "local");
  assert.deepEqual(gesture.move({ x: 8, y: 6 }), { x: 8, y: 6 });
  assert.deepEqual(gesture.scrollPoint({ x: 8, y: 6 }), { x: 8, y: 6 });
  gesture.end();
  assert.equal(gesture.wheelOwner({}, captured), "app");
  assert.equal(gesture.scrollPoint(point), undefined);
  assert.equal(gesture.move(point), null);
});

test("Application ownership is not stolen by Shift part way through a drag", () => {
  const gesture = new SelectionGesture();
  assert.deepEqual(gesture.begin({ button: 0 }, point, captured), { owner: "app" });
  assert.equal(gesture.wheelOwner({ shiftKey: true }, captured), "app");
  assert.equal(gesture.move(point), null);
  gesture.end();
  assert.equal(gesture.wheelOwner({ shiftKey: true }, captured), "local");
});

test("History and read-only views never begin application gestures", () => {
  for (const state of [{ ...captured, historical: true }, { ...captured, readOnly: true }]) {
    const gesture = new SelectionGesture();
    assert.equal(gesture.begin({ button: 0 }, point, state).owner, "local");
    assert.equal(gesture.wheelOwner({}, state), "local");
    gesture.end();
    assert.equal(gesture.begin({ button: 2 }, point, state), null);
  }
});

test("Rectangle wheel retains column boundaries; post-release wheel has no endpoint", () => {
  const gesture = new SelectionGesture();
  gesture.begin({ button: 0, altKey: true }, point, local);
  gesture.move({ x: 12, y: 6 });
  assert.deepEqual(gesture.scrollPoint({ x: 20, y: 6 }), { x: 12, y: 6 });
  assert.equal(gesture.wheelOwner({ altKey: true }, local), "local");
  gesture.end();
  assert.equal(gesture.scrollPoint({ x: 20, y: 6 }), undefined);
});

test("Scaled thumbnail points use CSS bounds, independent of DPR and backing resolution", () => {
  const bounds = { left: 100, top: 40, width: 200, height: 120 };
  assert.deepEqual(cellPoint({ clientX: 125, clientY: 70 }, bounds, 80, 24),
    { x: 10, y: 6, shift: false, alt: false, ctrl: false });
  assert.equal(cellPoint({ clientX: 300, clientY: 70 }, bounds, 80, 24), null);
  assert.deepEqual(cellPoint({ clientX: 500, clientY: 0, shiftKey: true }, bounds, 80, 24, true),
    { x: 79, y: 0, shift: true, alt: false, ctrl: false });
  assert.equal(cellPoint({ clientX: 0, clientY: 0 }, { width: 0, height: 0 }, 80, 24), null);
});

test("Trackpad fractions accumulate while line and page wheel units remain bounded", () => {
  const wheel = new WheelAccumulator();
  const bounds = { height: 100 };
  const pixels = { deltaX: 0, deltaY: 4, deltaMode: 0 };
  assert.deepEqual(wheel.take(pixels, bounds, 10), { x: 0, y: 0 });
  assert.deepEqual(wheel.take(pixels, bounds, 10), { x: 0, y: 0 });
  assert.deepEqual(wheel.take(pixels, bounds, 10), { x: 0, y: 1 });
  wheel.reset();
  assert.deepEqual(wheel.take({ deltaX: -2, deltaY: 3, deltaMode: 1 }, bounds, 10), { x: -2, y: 3 });
  assert.deepEqual(wheel.take({ deltaX: 0, deltaY: -1, deltaMode: 2 }, bounds, 10), { x: 0, y: -10 });
  assert.deepEqual(wheel.take({ deltaX: 0, deltaY: 10000, deltaMode: 0 }, bounds, 10), { x: 0, y: 32 });
});

function mouseHarness(run, tracking = 1002, resolve, inspection = {}) {
  const originals = Object.fromEntries(["window", "requestAnimationFrame", "cancelAnimationFrame", "setTimeout", "clearTimeout"]
    .map(key => [key, globalThis[key]]));
  const timers = new Map();
  let nextTimer = 1;
  globalThis.window = new EventTarget();
  globalThis.setTimeout = callback => { const id = nextTimer++; timers.set(id, callback); return id; };
  globalThis.clearTimeout = id => timers.delete(id);
  globalThis.requestAnimationFrame = globalThis.setTimeout;
  globalThis.cancelAnimationFrame = globalThis.clearTimeout;
  const canvas = new EventTarget();
  canvas.title = "";
  canvas.style = { cursor: "" };
  canvas.getBoundingClientRect = () => ({ left: 0, top: 0, width: 200, height: 100, bottom: 100 });
  let capturedPointer;
  canvas.setPointerCapture = id => { capturedPointer = id; };
  canvas.hasPointerCapture = id => capturedPointer === id;
  canvas.releasePointerCapture = () => { capturedPointer = undefined; };
  const commands = [];
  const endings = [];
  const mouse = captureMouse(canvas, command => commands.push(command), () => {}, {
    state: () => ({ historical: false, readOnly: false }),
    begin: (point, selection) => commands.push({ type: "begin", point, selection }),
    extend: point => commands.push({ type: "extend", point }),
    scroll: (delta, point) => commands.push({ type: "scroll", delta, point }),
    end: cancelled => endings.push(cancelled),
    resolve,
    execute: (decision, input) => commands.push({ type: "action", decision, input }),
    ...inspection
  });
  mouse.update(20, 10, tracking);
  endings.length = 0;
  const emit = (type, data = {}, target = canvas) => {
    const event = new Event(type, { cancelable: true });
    Object.assign(event, {
      pointerId: 1, pointerType: "mouse", button: 0, buttons: 1, clientX: 25, clientY: 25,
      deltaMode: 1, deltaX: 0, deltaY: 1, ...data
    });
    target.dispatchEvent(event);
    return event;
  };
  try { run({ mouse, commands, emit, timers, canvas, endings }); }
  finally {
    mouse.dispose();
    for (const [key, value] of Object.entries(originals)) {
      if (value === undefined) delete globalThis[key];
      else globalThis[key] = value;
    }
  }
}

test("A routed clipboard gesture suppresses context menu and never leaks mouse reports", () => {
  mouseHarness(({ commands, emit }) => {
    const down = emit("pointerdown", { button: 2, buttons: 2, shiftKey: true });
    assert.equal(down.defaultPrevented, true);
    assert.equal(emit("contextmenu", { button: 2 }).defaultPrevented, true);
    emit("pointermove", { button: 2, buttons: 2, clientX: 80 });
    emit("wheel", { shiftKey: false });
    emit("pointerup", { button: 2, buttons: 0 });
    assert.deepEqual(commands.map(command => command.type), ["action"]);
  }, 1003, () => ({ action: "copyOrPaste" }));
});

test("Ctrl/Cmd hyperlink clicks open on release without application or selection commands", () => {
  for (const tracking of [0, 9, 1000, 1002, 1003]) {
    for (const modifiers of [{ ctrlKey: true }, { metaKey: true }]) {
      const opened = [];
      mouseHarness(({ commands, emit }) => {
        assert.equal(emit("pointerdown", modifiers).defaultPrevented, true);
        assert.deepEqual(opened, []);
        assert.equal(emit("contextmenu", modifiers).defaultPrevented, true);
        emit("pointerup", { buttons: 0 });
        emit("click", { buttons: 0, detail: 1 });
        assert.deepEqual(opened, ["https://example.com/"]);
        assert.deepEqual(commands, []);
      }, tracking, undefined, {
        hyperlink: () => "https://example.com/", openHyperlink: uri => opened.push(uri)
      });
    }
  }
});

test("Plain clicks, shift selection, and explicit routing retain ownership over hyperlinks", () => {
  for (const [tracking, modifiers, resolve, expected] of [
    [0, {}, undefined, ["begin"]],
    [1003, {}, undefined, ["mouse", "mouse"]],
    [1003, { ctrlKey: true, shiftKey: true }, undefined, ["begin"]],
    [0, { ctrlKey: true, altKey: true }, undefined, ["begin"]],
    [1003, { ctrlKey: true }, () => ({ route: "application" }), ["mouse", "mouse"]],
    [1003, { ctrlKey: true }, () => ({ route: "consume" }), []],
    [1003, { ctrlKey: true }, () => ({ route: "browser" }), []],
    [1003, { ctrlKey: true }, () => ({ action: "custom" }), ["action"]]
  ]) {
    mouseHarness(({ commands, emit }) => {
      emit("pointerdown", modifiers);
      emit("pointerup", { buttons: 0 });
      assert.deepEqual(commands.map(command => command.type), expected);
    }, tracking, resolve, {
      hyperlink: () => "https://example.com/", openHyperlink: () => assert.fail("Unexpected hyperlink activation")
    });
  }
});

test("Dragging, cancellation, scrolling, stale targets, and disposal never activate hyperlinks", () => {
  for (const cancel of [
    ({ emit }) => { emit("pointermove", { clientX: 35 }); emit("pointermove", { clientX: 25 }); },
    ({ emit }) => emit("pointercancel"),
    ({ emit }) => emit("blur", {}, window),
    ({ emit }) => emit("pointerleave"),
    ({ emit }) => emit("wheel"),
    ({ emit }) => emit("pointerdown", { button: 2, buttons: 3 }),
    ({ mouse }) => mouse.update(40, 10, 1003),
    ({ mouse }) => mouse.cancel(),
    ({ mouse }) => mouse.dispose()
  ]) {
    mouseHarness(harness => {
      harness.emit("pointerdown", { ctrlKey: true });
      cancel(harness);
      harness.emit("pointerup", { buttons: 0 });
    }, 1003, undefined, {
      hyperlink: () => "https://example.com/", openHyperlink: () => assert.fail("Canceled hyperlink activated")
    });
  }
  let uri = "https://example.com/old";
  mouseHarness(({ emit, mouse }) => {
    emit("pointerdown", { ctrlKey: true });
    uri = "https://example.com/new";
    mouse.update(20, 10, 1003);
    uri = "https://example.com/old";
    emit("pointerup", { buttons: 0 });
  }, 1003, undefined, {
    hyperlink: () => uri, openHyperlink: () => assert.fail("Changed hyperlink activated")
  });
});

test("Hyperlink hover hints track modifiers, frame changes, and pointer departure", () => {
  let uri = "https://example.com/";
  mouseHarness(({ emit, canvas, mouse }) => {
    emit("pointermove", { buttons: 0 });
    assert.match(canvas.title, /https:\/\/example.com\/\nCtrl\/Cmd\+click/);
    assert.equal(canvas.style.cursor, "");
    emit("keydown", { ctrlKey: true }, window);
    assert.equal(canvas.style.cursor, "pointer");
    mouse.update(20, 10, 0);
    assert.equal(canvas.style.cursor, "pointer");
    emit("keyup", { ctrlKey: false }, window);
    mouse.refresh();
    assert.equal(canvas.style.cursor, "");
    uri = "https://example.com/new";
    mouse.update(20, 10, 0);
    assert.match(canvas.title, /example.com\/new/);
    emit("pointerleave");
    assert.equal(canvas.title, "");
    assert.equal(canvas.style.cursor, "");
  }, 0, undefined, { hyperlink: () => uri });
});

test("Browser-routed pointer gestures preserve the native context menu even under capture", () => {
  mouseHarness(({ commands, emit }) => {
    assert.equal(emit("pointerdown", { button: 2, buttons: 2 }).defaultPrevented, false);
    assert.equal(emit("contextmenu", { button: 2 }).defaultPrevented, false);
    emit("pointerup", { button: 2, buttons: 0 });
    assert.deepEqual(commands, []);
  }, 1003, () => ({ route: "browser" }));
});

test("Forced application routing latches down and up despite a modifier change", () => {
  mouseHarness(({ commands, emit }) => {
    emit("pointerdown", { button: 2, buttons: 2, shiftKey: true });
    emit("pointerup", { button: 2, buttons: 0, shiftKey: false });
    assert.deepEqual(commands.map(command => command.action), ["down", "up"]);
    assert.ok(commands.every(command => command.button === "right"));
  }, 1002, input => ({ route: input.shift ? "application" : "consume" }));
});

test("Wheel overrides run only outside held gestures, preserving existing selection ownership", () => {
  mouseHarness(({ commands, emit }) => {
    emit("wheel");
    assert.equal(commands.at(-1).type, "action");
    emit("pointerdown");
    emit("wheel");
    assert.equal(commands.at(-1).type, "scroll");
    emit("pointerup", { buttons: 0 });
  }, 0, input => input.type === "wheel" ? { action: "scrollToLive" } : { route: "continue" });
});

test("Captured local drag sends no application reports after Shift is released", () => {
  mouseHarness(({ commands, emit }) => {
    emit("pointerdown", { shiftKey: true });
    emit("pointermove", { clientX: 85 });
    emit("wheel");
    emit("pointerup", { buttons: 0, clientX: 85 });
    assert.deepEqual(commands.map(command => command.type), ["begin", "extend", "scroll"]);
    assert.deepEqual(commands[2].point, { x: 8, y: 2 });
    emit("wheel");
    assert.equal(commands.at(-1).type, "mouse");
    assert.equal(commands.at(-1).action, "wheel");
  });
});

test("Wheel after release scrolls history without altering the selected range", () => {
  mouseHarness(({ commands, emit }) => {
    emit("pointerdown", { altKey: true });
    emit("pointermove", { clientX: 85 });
    emit("wheel", { altKey: true });
    assert.deepEqual(commands.at(-1).point, { x: 8, y: 2 });
    emit("pointerup", { buttons: 0, clientX: 85 });
    emit("wheel", { altKey: true });
    assert.equal(commands.at(-1).type, "scroll");
    assert.equal(commands.at(-1).point, undefined);
  }, 0);
});

test("Real pointer events with zero detail count double and triple clicks deterministically", () => {
  mouseHarness(({ commands, emit }) => {
    for (let count = 1; count <= 3; count++) {
      emit("pointerdown", { detail: 0 });
      emit("pointerup", { detail: 0, buttons: 0 });
    }
    assert.deepEqual(commands.map(command => command.selection.mode), ["character", "word", "line"]);
  }, 0);
});

test("Explicit clickCount detail wins, and dragging resets inferred multiclick count", () => {
  mouseHarness(({ commands, emit }) => {
    emit("pointerdown", { detail: 2 });
    assert.equal(commands.at(-1).selection.mode, "word");
    emit("pointermove", { clientX: 85 });
    emit("pointerup", { buttons: 0, clientX: 85 });
    emit("pointerdown", { detail: 0 });
    assert.equal(commands.at(-1).selection.mode, "character");
    emit("pointerup", { buttons: 0 });
  }, 0);
});

test("Native click detail corrects isolated triple-click when pointer detail is zero", () => {
  mouseHarness(({ commands, emit }) => {
    emit("pointerdown", { detail: 0 });
    emit("pointerup", { detail: 0, buttons: 0 });
    emit("click", { detail: 3, buttons: 0 });
    assert.equal(commands.at(-1).selection.mode, "line");
  }, 0);
});

test("Native double-click count seeds a zero-detail third pointerdown and line drag", () => {
  mouseHarness(({ commands, emit }) => {
    emit("pointerdown", { detail: 0 });
    emit("pointerup", { detail: 0, buttons: 0 });
    emit("click", { detail: 2, buttons: 0 });
    assert.equal(commands.at(-1).selection.mode, "word");
    emit("pointerdown", { detail: 0 });
    assert.equal(commands.at(-1).selection.mode, "line");
    emit("pointermove", { clientX: 85 });
    emit("pointerup", { buttons: 0, clientX: 85 });
    const count = commands.length;
    emit("click", { detail: 3, buttons: 0, clientX: 85 });
    assert.equal(commands.length, count);
  }, 0);
});

test("Application drag preserves down/up pairing despite modifier changes", () => {
  mouseHarness(({ commands, emit }) => {
    emit("pointerdown");
    emit("pointermove", { shiftKey: true, clientX: 85 });
    emit("pointerup", { shiftKey: true, buttons: 0, clientX: 85 });
    assert.deepEqual(commands.map(command => command.action), ["down", "move", "up"]);
    assert.ok(commands.every(command => command.type === "mouse"));
  });

});

test("Local selection discards a scheduled application hover move", () => {
  mouseHarness(({ commands, emit, timers }) => {
    emit("pointermove", { buttons: 0 });
    assert.equal(timers.size, 1);
    emit("pointerdown", { shiftKey: true });
    assert.equal(timers.size, 0);
    emit("wheel");
    emit("pointerup", { buttons: 0 });
    assert.ok(commands.every(command => command.type !== "mouse"));
  }, 1003);
});

test("Pointer release finishes selection but subsequent blur still cancels a deferred endpoint", () => {
  mouseHarness(({ emit, endings }) => {
    emit("pointerdown", { shiftKey: true });
    emit("pointermove", { clientX: 85 });
    emit("pointerup", { buttons: 0, clientX: 85 });
    assert.deepEqual(endings, [false]);
    emit("blur", {}, window);
    assert.deepEqual(endings, [false, true]);
  });
});

test("Edge autoscroll has one bounded timer and stops on release, cancel, blur, or dispose", () => {
  for (const end of ["pointerup", "pointercancel", "blur", "dispose"]) {
    mouseHarness(({ mouse, commands, emit, timers }) => {
      emit("pointerdown", { shiftKey: true });
      emit("pointermove", { clientY: -100 });
      assert.equal(timers.size, 1);
      const [id, tick] = timers.entries().next().value;
      timers.delete(id);
      tick();
      assert.equal(commands.at(-1).type, "scroll");
      assert.equal(commands.at(-1).delta, -8);
      assert.equal(timers.size, 1);
      if (end === "dispose") mouse.dispose();
      else emit(end, { buttons: 0 }, end === "blur" ? window : undefined);
      assert.equal(timers.size, 0);
      assert.ok(commands.every(command => command.type !== "mouse"));
    });
  }
});
