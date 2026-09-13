import assert from "node:assert/strict";
import { Worker as NodeWorker } from "node:worker_threads";
import { setImmediate as nextTurn } from "node:timers/promises";
import { WebTerminal } from "../../dist/web-terminal.js";

export class Target {
  listeners = new Map();
  addEventListener(type, callback) {
    const handlers = this.listeners.get(type) ?? [];
    handlers.push(callback);
    this.listeners.set(type, handlers);
  }
  dispatchEvent(event) {
    for (const callback of this.listeners.get(event.type) ?? []) callback(event);
    return !event.defaultPrevented;
  }
}

export class Element extends Target {
  style = {};
  dataset = {};
  attributes = new Map();
  children = [];
  selectors = new Map();
  captures = new Set();
  textContent = "";
  title = "";
  setAttribute(name, value) { this.attributes.set(name, value); }
  getAttribute(name) { return this.attributes.get(name); }
  attachShadow() {
    this.shadowRoot = new Element();
    this.shadowRoot.host = this;
    return this.shadowRoot;
  }
  querySelector(selector) {
    if (!this.selectors.has(selector)) {
      const element = new Element();
      element.parent = this;
      this.selectors.set(selector, element);
    }
    return this.selectors.get(selector);
  }
  append(...elements) {
    this.children.push(...elements);
    for (const element of elements) element.parent = this;
  }
  replaceChildren(...elements) { this.children = []; this.append(...elements); }
  remove() {
    if (this.parent) this.parent.children = this.parent.children.filter(child => child !== this);
  }
  transferControlToOffscreen() { return {}; }
  getBoundingClientRect() { return { width: 100, height: 100, left: 0, top: 0, right: 100, bottom: 100 }; }
  hasPointerCapture(id) { return this.captures.has(id); }
  setPointerCapture(id) { this.captures.add(id); }
  releasePointerCapture(id) { this.captures.delete(id); }
  focus() {
    let root = this;
    while (root.parent && !root.host) root = root.parent;
    if (root.host) {
      document.activeElement = root.host;
      root.activeElement = this;
    } else {
      document.activeElement = this;
      if (this.shadowRoot) this.shadowRoot.activeElement = null;
    }
  }
}

class WorkerBridge extends Target {
  worker = new NodeWorker(new URL("./title-worker.mjs", import.meta.url));
  pending = new Map();
  outputs = [];
  commands = [];
  errors = [];
  serial = 0;
  constructor() {
    super();
    this.worker.on("message", envelope => {
      if (envelope.type === "output") {
        this.outputs.push(envelope.message);
        // Browser listener errors are reported to the host, not back into the worker.
        try { this.deliver(envelope.message); }
        catch (error) { this.errors.push(error); }
      } else if (envelope.type === "sent") {
        this.commands.push(envelope.command);
      } else {
        const pending = this.pending.get(envelope.id);
        if (!pending) return;
        this.pending.delete(envelope.id);
        if (envelope.type === "failure") pending.reject(new Error(envelope.error));
        else pending.resolve();
      }
    });
    this.worker.on("error", error => {
      for (const pending of this.pending.values()) pending.reject(error);
      this.pending.clear();
    });
    this.worker.on("exit", () => {
      for (const pending of this.pending.values()) pending.reject(new Error("Worker terminated"));
      this.pending.clear();
    });
  }
  deliver(message) { this.dispatchEvent({ type: "message", data: message }); }
  request(action, details = {}) {
    const id = ++this.serial;
    const pending = Promise.withResolvers();
    this.pending.set(id, pending);
    this.worker.postMessage({ id, action, ...details });
    return pending.promise;
  }
  postMessage(message) { this.request("input", { message }).catch(() => {}); }
  terminate() { return this.worker.terminate(); }
}

export function browser(t, overrides = {}) {
  const workers = [];
  const globals = {
    Element, HTMLElement: Element, HTMLDivElement: Element, HTMLCanvasElement: Element,
    HTMLTextAreaElement: Element, HTMLButtonElement: Element, HTMLSpanElement: Element,
    ResizeObserver: class { observe() {} disconnect() {} },
    OffscreenCanvas: class {},
    Worker: class extends WorkerBridge {
      constructor() { super(); workers.push(this); }
    },
    document: { createElement: () => new Element(), title: "Host document", activeElement: null },
    location: { href: "https://example.test/terminal" },
    getComputedStyle: element => ({ width: element.style.width ?? "0", height: element.style.height ?? "0" }),
    ...overrides
  };
  globals.window = Object.assign(new Target(), {
    Worker: globals.Worker, ResizeObserver: globals.ResizeObserver,
    OffscreenCanvas: globals.OffscreenCanvas, devicePixelRatio: 1
  });
  const saved = new Map(Object.keys(globals).map(key => [key, Object.getOwnPropertyDescriptor(globalThis, key)]));
  for (const [key, value] of Object.entries(globals))
    Object.defineProperty(globalThis, key, { configurable: true, writable: true, value });
  t.after(async () => {
    for (const worker of workers) worker.disposeView?.();
    await Promise.all(workers.map(worker => worker.terminate()));
    for (const [key, descriptor] of saved) {
      if (descriptor) Object.defineProperty(globalThis, key, descriptor);
      else delete globalThis[key];
    }
  });
  return workers;
}

export function frame({ title = "", revision = 1, full = revision === 1, baseRevision = full ? 0 : revision - 1,
  peer = { id: null, primaryId: null, isPrimary: true }, ...overrides } = {}) {
  const metadata = {
    version: 1, title, revision, full, baseRevision, peer,
    progress: { state: "none", percentage: null },
    shellIntegration: { phase: "unknown", lastExitCode: null },
    workingDirectory: { uri: null, host: null, path: null },
    commandMark: null,
    columns: 1, rows: 1, cellWidth: 10, cellHeight: 20, mouseTracking: 0,
    cursor: { visible: true, x: 0, y: 0, shape: 1 },
    history: null, images: [], retainedImages: [], placements: [], warnings: [], hyperlinks: [],
    stats: { workloadBytes: 0, outputBatches: 0, captureMs: 0, elapsedMs: 0 }, ...overrides
  };
  const json = new TextEncoder().encode(JSON.stringify(metadata));
  const count = full ? metadata.columns * metadata.rows : 0;
  const bytes = new Uint8Array(12 + json.length + count * 23);
  const view = new DataView(bytes.buffer);
  view.setUint32(0, 0x31545748, true);
  view.setUint32(4, json.length, true);
  bytes.set(json, 8);
  view.setUint32(8 + json.length, count, true);
  for (let index = 0; index < count; index++) {
    const offset = 12 + json.length + index * 23;
    view.setUint32(offset, index, true);
    view.setUint8(offset + 18, 1);
    view.setUint16(offset + 20, 1, true);
    view.setUint8(offset + 22, 65);
  }
  return bytes.buffer;
}

export async function mounting(t, workers, options = {}) {
  const container = new Element();
  const notices = [];
  let handle;
  let settled = false;
  const promise = WebTerminal.mount(container, {
    url: "/ws", renderer: "webgl2", label: "Host input label", ...options,
    onSelectionUI(event) {
      void event.detail.runAction(context => { handle = context.terminal; });
      options.onSelectionUI?.(event);
    },
    onTitleChange(title) {
      assert.ok(handle, "public input action provides the in-flight handle");
      assert.equal(handle.title, title, "getter must update before the callback");
      notices.push({ title, settled });
      options.onTitleChange?.(title);
    }
  });
  promise.then(() => { settled = true; }, () => { settled = true; });
  await nextTurn();
  const worker = workers.at(-1);
  await worker.request("flush");
  worker.disposeView = () => handle?.dispose();
  return { worker, promise, notices, container, get handle() { return handle; }, get settled() { return settled; } };
}

export async function present(view, state) {
  await view.worker.request("frame", { buffer: frame(state) });
  await view.worker.request("draw");
}
