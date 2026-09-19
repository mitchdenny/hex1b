import { parentPort } from "node:worker_threads";
import { setImmediate as nextTurn } from "node:timers/promises";

const listeners = new Map();
globalThis.self = {
  addEventListener(type, handler) { listeners.set(type, handler); },
  postMessage(message) { parentPort.postMessage({ type: "output", message }); },
};
globalThis.postMessage = globalThis.self.postMessage;
await import("../../.build/link-detection-worker.js");

parentPort.on("message", async ({ id, action, message }) => {
  try {
    if (action === "input") {
      listeners.get("message")?.({ data: message });
      globalThis.onmessage?.({ data: message });
    }
    await nextTurn();
    parentPort.postMessage({ type: "done", id });
  } catch (error) {
    parentPort.postMessage({ type: "failure", id, error: error.stack });
  }
});
