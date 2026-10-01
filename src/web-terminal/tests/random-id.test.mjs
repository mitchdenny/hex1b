import assert from "node:assert/strict";
import { test } from "node:test";
import { randomId } from "../.build/random-id.js";

test("IDs use native randomUUID when available", t => {
  const original = Object.getOwnPropertyDescriptor(globalThis, "crypto");
  t.after(() => Object.defineProperty(globalThis, "crypto", original));
  const expected = "01234567-89ab-4cde-8f01-23456789abcd";
  Object.defineProperty(globalThis, "crypto", { configurable: true, value: {
    randomUUID: () => expected,
    getRandomValues() { assert.fail("Native randomUUID should be used"); }
  } });
  assert.equal(randomId(), expected);
});

for (const byte of [0, 255]) {
  test(`HTTP IDs use random bytes with UUID v4 version and variant bits (${byte})`, t => {
    const original = Object.getOwnPropertyDescriptor(globalThis, "crypto");
    t.after(() => Object.defineProperty(globalThis, "crypto", original));
    let calls = 0;
    Object.defineProperty(globalThis, "crypto", { configurable: true, value: {
      getRandomValues(bytes) {
        assert.ok(bytes instanceof Uint8Array);
        assert.equal(bytes.length, 16);
        calls++;
        return bytes.fill(byte);
      }
    } });
    assert.equal(randomId(), byte === 0
      ? "00000000-0000-4000-8000-000000000000"
      : "ffffffff-ffff-4fff-bfff-ffffffffffff");
    assert.equal(calls, 1);
  });
}
