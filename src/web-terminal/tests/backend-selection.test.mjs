import assert from "node:assert/strict";
import { test } from "node:test";
import { createRenderBackend } from "../dist/backend-selection.js";
import { RendererUnavailableError } from "../dist/render-backend.js";
import { normalizeRenderer } from "../dist/renderer-options.js";
import { WebGpuBackend } from "../dist/webgpu-backend.js";
import { WebGl2Backend } from "../dist/webgl2-backend.js";
import { TerminalRenderer } from "../dist/renderer.js";
import { WebTerminal } from "../dist/index.js";

test("Renderer preferences default to auto and reject unsupported values before mounting", () => {
  assert.equal(normalizeRenderer(), "auto");
  for (const value of ["auto", "webgpu", "webgl2"]) assert.equal(normalizeRenderer(value), value);
  for (const value of [null, false, 1, {}, [], "", "WebGPU", "canvas2d"]) {
    assert.throws(() => normalizeRenderer(value), TypeError);
    assert.throws(() => new WebTerminal({ url: "/terminal", renderer: value }), /renderer must be/);
  }
});

test("Auto and explicit WebGPU use the GPU backend without probing WebGL2", async t => {
  const backend = { kind: "webgpu" };
  const canvas = {};
  const onFatal = () => {};
  const gpu = t.mock.method(WebGpuBackend, "create", async (...args) => {
    assert.deepEqual(args, [canvas, onFatal]);
    return backend;
  });
  const gl = t.mock.method(WebGl2Backend, "create");
  assert.deepEqual(await createRenderBackend(canvas, onFatal), { backend });
  assert.deepEqual(await createRenderBackend(canvas, onFatal, "webgpu"), { backend });
  assert.equal(gpu.mock.callCount(), 2);
  assert.equal(gl.mock.callCount(), 0);
});

test("Explicit WebGL2 never probes WebGPU even when available", async t => {
  const backend = { kind: "webgl2" };
  const gpu = t.mock.method(WebGpuBackend, "create");
  t.mock.method(WebGl2Backend, "create", async () => backend);
  assert.deepEqual(await createRenderBackend({}, () => {}, "webgl2"), { backend });
  assert.equal(gpu.mock.callCount(), 0);
});

test("Auto falls back only on capability failures and retains their diagnostic", async t => {
  const backend = { kind: "webgl2" };
  t.mock.method(WebGpuBackend, "create", async () => { throw new RendererUnavailableError("No WebGPU adapter"); });
  const gl = t.mock.method(WebGl2Backend, "create", async () => backend);
  assert.deepEqual(await createRenderBackend({}, () => {}), { backend, fallbackReason: "No WebGPU adapter" });
  assert.equal(gl.mock.callCount(), 1);
});

test("Explicit WebGPU fails rather than silently switching backends", async t => {
  const error = new RendererUnavailableError("WebGPU requires HTTPS");
  t.mock.method(WebGpuBackend, "create", async () => { throw error; });
  const gl = t.mock.method(WebGl2Backend, "create");
  await assert.rejects(createRenderBackend({}, () => {}, "webgpu"), e => e === error);
  assert.equal(gl.mock.callCount(), 0);
});

test("Shader, validation and unexpected failures never trigger automatic fallback", async t => {
  const gl = t.mock.method(WebGl2Backend, "create");
  for (const error of [new Error("Shader compilation failed"), new TypeError("Invalid pipeline"), new Error("Device lost")]) {
    const gpu = t.mock.method(WebGpuBackend, "create", async () => { throw error; });
    await assert.rejects(createRenderBackend({}, () => {}), e => e === error);
    gpu.mock.restore();
  }
  assert.equal(gl.mock.callCount(), 0);
});

test("Failure of both backends reports both causes", async t => {
  const gpuError = new RendererUnavailableError("WebGPU requires HTTPS");
  const glError = new RendererUnavailableError("WebGL2 context unavailable");
  t.mock.method(WebGpuBackend, "create", async () => { throw gpuError; });
  t.mock.method(WebGl2Backend, "create", async () => { throw glError; });
  await assert.rejects(createRenderBackend({}, () => {}), error => {
    assert.ok(error instanceof AggregateError);
    assert.deepEqual(error.errors, [gpuError, glError]);
    assert.match(error.message, /WebGPU requires HTTPS.*WebGL2 context unavailable/);
    return true;
  });
});

test("Insecure contexts fall back before WebGPU or the presentation canvas is touched", async t => {
  const secure = Object.getOwnPropertyDescriptor(globalThis, "isSecureContext");
  Object.defineProperty(globalThis, "isSecureContext", { configurable: true, value: false });
  t.after(() => {
    if (secure) Object.defineProperty(globalThis, "isSecureContext", secure);
    else delete globalThis.isSecureContext;
  });
  const backend = { kind: "webgl2" };
  t.mock.method(WebGl2Backend, "create", async () => backend);
  const result = await createRenderBackend({ getContext() { assert.fail("WebGPU must not acquire this surface"); } }, () => {});
  assert.equal(result.backend, backend);
  assert.match(result.fallbackReason, /HTTPS or localhost/);
});

test("Missing API, absent adapter and device acquisition rejection are capability failures", async t => {
  const secure = Object.getOwnPropertyDescriptor(globalThis, "isSecureContext");
  const navigator = Object.getOwnPropertyDescriptor(globalThis, "navigator");
  Object.defineProperty(globalThis, "isSecureContext", { configurable: true, value: true });
  const setGpu = gpu => Object.defineProperty(globalThis, "navigator", { configurable: true, value: { gpu } });
  t.after(() => {
    if (secure) Object.defineProperty(globalThis, "isSecureContext", secure);
    else delete globalThis.isSecureContext;
    if (navigator) Object.defineProperty(globalThis, "navigator", navigator);
    else delete globalThis.navigator;
  });
  const backend = { kind: "webgl2" };
  t.mock.method(WebGl2Backend, "create", async () => backend);
  for (const gpu of [undefined, { requestAdapter: async () => null }, {
    requestAdapter: async () => ({ requestDevice: async () => { throw new DOMException("Unavailable", "OperationError"); } })
  }]) {
    setGpu(gpu);
    const result = await createRenderBackend({}, () => {});
    assert.equal(result.backend, backend);
    assert.ok(result.fallbackReason);
  }
  const bug = new TypeError("Unexpected device request failure");
  setGpu({ requestAdapter: async () => ({ requestDevice: async () => { throw bug; } }) });
  await assert.rejects(createRenderBackend({}, () => {}), error => error === bug);
});

test("Font initialization failures dispose the selected backend without fallback", async t => {
  let disposed = 0;
  t.mock.method(WebGpuBackend, "create", async () => ({ dispose() { disposed++; } }));
  const gl = t.mock.method(WebGl2Backend, "create");
  const error = new Error("Could not load terminal font");
  t.mock.method(TerminalRenderer.prototype, "initialize", async () => { throw error; });
  await assert.rejects(TerminalRenderer.create({}, 1, () => {}), e => e === error);
  assert.equal(disposed, 1);
  assert.equal(gl.mock.callCount(), 0);
});

test("Invalid scale and font configuration do not allocate a backend", async t => {
  const gpu = t.mock.method(WebGpuBackend, "create");
  const gl = t.mock.method(WebGl2Backend, "create");
  await assert.rejects(TerminalRenderer.create({}, 0, () => {}), /Invalid backing scale/);
  await assert.rejects(TerminalRenderer.create({}, 1, () => {}, { family: "" }), TypeError);
  assert.equal(gpu.mock.callCount(), 0);
  assert.equal(gl.mock.callCount(), 0);
});

test("Renderer diagnostics distinguish auto fallback from explicit selection", async t => {
  t.mock.method(WebGpuBackend, "create", async () => { throw new RendererUnavailableError("Adapter disabled"); });
  t.mock.method(WebGl2Backend, "create", async () => ({ kind: "webgl2", instanceBufferBytes: 0, dispose() {} }));
  t.mock.method(TerminalRenderer.prototype, "initialize", async function () {
    this.font = { family: "monospace", dispose() {} };
    this.atlas = { width: 1, height: 1, destroy() {} };
  });
  for (const preference of ["auto", "webgl2"]) {
    const renderer = await TerminalRenderer.create({ width: 1, height: 1 }, 1, () => {}, undefined, preference);
    assert.equal(renderer.metrics().renderer, "webgl2");
    assert.equal(renderer.metrics().rendererFallbackReason, preference === "auto" ? "Adapter disabled" : undefined);
    renderer.dispose();
    renderer.dispose();
  }
});
