import { parentPort } from "node:worker_threads";
import { setImmediate as nextTurn } from "node:timers/promises";
import { TerminalRenderer } from "../../dist/renderer.js";

// Execute the real worker with only its transport, GPU, and animation clock doubled.
const listeners = new Map();
const animations = [];
const intervals = new Map();
let nextInterval = 0;
let socket;
let holdPresentation = false;
let presentation;
globalThis.self = {
  addEventListener(type, handler) { listeners.set(type, handler); },
  postMessage(message) { parentPort.postMessage({ type: "output", message }); },
  requestAnimationFrame(callback) { animations.push(callback); return animations.length; },
  close() {}
};
globalThis.setInterval = callback => {
  intervals.set(++nextInterval, callback);
  return nextInterval;
};
globalThis.clearInterval = id => intervals.delete(id);
globalThis.WebSocket = class {
  static OPEN = 1;
  readyState = 0;
  listeners = new Map();
  constructor() { socket = this; }
  addEventListener(type, handler) { this.listeners.set(type, handler); }
  send(data) { parentPort.postMessage({ type: "sent", command: JSON.parse(data) }); }
  close() { this.readyState = 3; }
};
const renderer = {
  backend: { kind: "webgl2" },
  disposed: false,
  resize() {},
  updateImages() {},
  prepareGlyphs() {},
  render() {
    if (holdPresentation) presentation = Promise.withResolvers();
    return { cpuMs: 0, quads: 1, drawCalls: 1 };
  },
  async idle() { await presentation?.promise; },
  metrics() { return {}; },
  dispose() { this.disposed = true; }
};
TerminalRenderer.create = async () => renderer;
await import("../../dist/terminal-worker.js");

let sequence = Promise.resolve();
parentPort.on("message", ({ id, action, message, buffer, details }) => {
  sequence = sequence.then(async () => {
    switch (action) {
      case "input": listeners.get("message")({ data: message }); break;
      case "open":
        socket.readyState = WebSocket.OPEN;
        socket.listeners.get("open")();
        break;
      case "frame": socket.listeners.get("message")({ data: buffer }); break;
      case "draw":
        for (const callback of animations.splice(0)) callback(performance.now());
        break;
      case "hold": holdPresentation = true; break;
      case "release":
        holdPresentation = false;
        presentation?.resolve();
        presentation = undefined;
        break;
      case "pulse":
        for (const callback of intervals.values()) callback();
        break;
      case "disconnect":
        socket.readyState = 3;
        socket.listeners.get("close")({ code: 1000, reason: "test disconnect", wasClean: true, ...details });
        break;
      case "socketError":
        socket.listeners.get("error")?.({});
        break;
    }
    await nextTurn();
    parentPort.postMessage({ type: "done", id });
  }).catch(error => parentPort.postMessage({ type: "failure", id, error: error.stack }));
});
