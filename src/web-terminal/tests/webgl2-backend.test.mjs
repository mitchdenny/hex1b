import assert from "node:assert/strict";
import { test } from "node:test";
import { QUAD_STRIDE, RendererUnavailableError } from "../dist/render-backend.js";
import { WebGl2Backend } from "../dist/webgl2-backend.js";

function harness(options = {}) {
  const calls = [];
  const live = new Set();
  let nextId = 0;
  const gl = {
    ...Object.fromEntries(`MAX_TEXTURE_SIZE MAX_RENDERBUFFER_SIZE MAX_VIEWPORT_DIMS
      VERTEX_SHADER FRAGMENT_SHADER COMPILE_STATUS LINK_STATUS ARRAY_BUFFER DYNAMIC_DRAW
      FLOAT TRIANGLES TEXTURE0 TEXTURE_2D RGBA8 RGBA UNSIGNED_BYTE TEXTURE_MIN_FILTER
      TEXTURE_MAG_FILTER TEXTURE_WRAP_S TEXTURE_WRAP_T LINEAR CLAMP_TO_EDGE
      UNPACK_ALIGNMENT UNPACK_FLIP_Y_WEBGL UNPACK_PREMULTIPLY_ALPHA_WEBGL
      UNPACK_COLORSPACE_CONVERSION_WEBGL UNPACK_ROW_LENGTH UNPACK_SKIP_PIXELS UNPACK_SKIP_ROWS
      BLEND FUNC_ADD SRC_ALPHA ONE_MINUS_SRC_ALPHA ONE DEPTH_TEST STENCIL_TEST CULL_FACE
      SCISSOR_TEST DITHER COLOR_BUFFER_BIT SYNC_GPU_COMMANDS_COMPLETE ALREADY_SIGNALED
      CONDITION_SATISFIED TIMEOUT_EXPIRED WAIT_FAILED`.split(/\s+/).map((name, i) => [name, i + 1])),
    NO_ERROR: 0,
    NONE: 0,
    INVALID_ENUM: 0x0500,
    INVALID_VALUE: 0x0501,
    INVALID_OPERATION: 0x0502,
    OUT_OF_MEMORY: 0x0505,
    INVALID_FRAMEBUFFER_OPERATION: 0x0506,
    CONTEXT_LOST_WEBGL: 0x9242,
    errors: [],
    waitResults: [],
    lost: false,
  };
  class FakeCanvas extends EventTarget {
    width = options.width ?? 64;
    height = options.height ?? 32;
    requests = [];
    listeners = new Set();
    getContext(kind, attributes) {
      this.requests.push({ kind, attributes });
      if (options.acquisitionError) throw options.acquisitionError;
      return options.unavailable ? null : gl;
    }
    addEventListener(type, callback, ...rest) {
      this.listeners.add(callback);
      super.addEventListener(type, callback, ...rest);
    }
    removeEventListener(type, callback, ...rest) {
      this.listeners.delete(callback);
      super.removeEventListener(type, callback, ...rest);
    }
  }
  const canvas = new FakeCanvas();
  const emitLoss = (message = "driver reset") => {
    gl.lost = true;
    const event = new Event("webglcontextlost");
    Object.defineProperty(event, "statusMessage", { value: message });
    canvas.dispatchEvent(event);
  };
  const record = (name, implementation = () => {}) => {
    gl[name] = (...args) => {
      calls.push({ name, args });
      if (gl.throwOn === name) throw new Error(`${name} threw`);
      const result = implementation(...args);
      if (gl.errorOn === name) gl.errors.push(gl.errorCode ?? gl.INVALID_OPERATION);
      return result;
    };
  };
  const allocate = (kind, extra = {}) => {
    if (options.nullResource === kind || gl.nullResource === kind) return null;
    const handle = { kind, id: ++nextId, ...extra };
    live.add(handle);
    return handle;
  };
  for (const name of [
    "shaderSource", "compileShader", "attachShader", "detachShader", "linkProgram", "useProgram",
    "uniform1i", "uniform2f", "activeTexture", "bindVertexArray", "bindBuffer",
    "enableVertexAttribArray", "vertexAttribDivisor", "enable", "disable",
    "blendEquationSeparate", "blendFuncSeparate", "viewport", "bindTexture", "pixelStorei",
    "texParameteri", "texImage2D", "texSubImage2D", "bufferData", "bufferSubData",
    "clearColor", "clear", "vertexAttribPointer", "drawArraysInstanced", "flush",
  ]) record(name);
  record("createShader", type => allocate("shader", { type }));
  record("createProgram", () => allocate("program"));
  record("createBuffer", () => allocate("buffer"));
  record("createVertexArray", () => allocate("vertexArray"));
  record("createTexture", () => allocate("texture"));
  record("fenceSync", () => allocate("sync"));
  for (const name of ["deleteShader", "deleteProgram", "deleteBuffer", "deleteVertexArray", "deleteTexture", "deleteSync"]) {
    record(name, handle => live.delete(handle));
  }
  record("getShaderParameter", shader => options.compileFailure !== shader.type);
  record("getShaderInfoLog", () => "test shader diagnostic");
  record("getProgramParameter", () => !options.linkFailure);
  record("getProgramInfoLog", () => "test link diagnostic");
  record("getUniformLocation", (_program, name) => options.missingUniform === name ? null : { name });
  record("getParameter", parameter => {
    if (parameter === gl.MAX_TEXTURE_SIZE) return options.textureLimit ?? 8192;
    if (parameter === gl.MAX_RENDERBUFFER_SIZE) return options.renderbufferLimit ?? 8192;
    if (parameter === gl.MAX_VIEWPORT_DIMS) return new Int32Array(options.viewportLimit ?? [8192, 8192]);
    assert.fail(`Unexpected parameter: ${parameter}`);
  });
  record("getError", () => gl.errors.shift() ?? gl.NO_ERROR);
  record("isContextLost", () => gl.lost);
  record("clientWaitSync", () => gl.waitResults.shift() ?? gl.waitResult ?? gl.ALREADY_SIGNALED);
  record("loseContext", () => emitLoss("intentional disposal"));
  record("getExtension", name => {
    assert.equal(name, "WEBGL_lose_context");
    return options.noLoseExtension ? null : { loseContext: gl.loseContext };
  });
  Object.defineProperties(gl, {
    drawingBufferWidth: { get: () => gl.bufferWidth ?? canvas.width },
    drawingBufferHeight: { get: () => gl.bufferHeight ?? canvas.height },
  });
  const fatalErrors = [];
  return {
    gl, canvas, live, calls, fatalErrors, emitLoss,
    onFatal: error => fatalErrors.push(error),
    callsFor: name => calls.filter(call => call.name === name).map(call => call.args),
  };
}

test("Create_NullContext_IsUnavailableWithoutAllocatingResources", async () => {
  const h = harness({ unavailable: true });
  await assert.rejects(WebGl2Backend.create(h.canvas, h.onFatal), RendererUnavailableError);
  assert.equal(h.live.size, 0);
  assert.equal(h.canvas.listeners.size, 0);
});

test("Create_UnexpectedAcquisitionError_IsNotACapabilityFailure", async () => {
  const error = new TypeError("unexpected context failure");
  const h = harness({ acquisitionError: error });
  await assert.rejects(WebGl2Backend.create(h.canvas, h.onFatal), actual => actual === error);
});

test("Create_OpaqueCanvas_UsesStraightAlphaBlendingAndTopDownShaders", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  const gl = h.gl;
  assert.equal(backend.kind, "webgl2");
  assert.equal(backend.gl, gl);
  assert.deepEqual(h.canvas.requests, [{
    kind: "webgl2",
    attributes: {
      alpha: false, antialias: false, depth: false, stencil: false,
      premultipliedAlpha: false, preserveDrawingBuffer: false,
    },
  }]);
  assert.deepEqual(h.callsFor("blendFuncSeparate"), [[gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA, gl.ONE, gl.ONE_MINUS_SRC_ALPHA]]);
  assert.deepEqual(h.callsFor("blendEquationSeparate"), [[gl.FUNC_ADD, gl.FUNC_ADD]]);
  assert.deepEqual(h.callsFor("enable"), [[gl.BLEND]]);
  assert.ok(h.callsFor("disable").some(([flag]) => flag === gl.DITHER));
  assert.deepEqual(h.callsFor("vertexAttribDivisor"), [[0, 1], [1, 1], [2, 1], [3, 1]]);
  assert.deepEqual(h.callsFor("enableVertexAttribArray"), [[0], [1], [2], [3]]);
  const [vertex, fragment] = h.callsFor("shaderSource").map(([, source]) => source);
  for (const source of [vertex, fragment]) {
    assert.match(source, /^#version 300 es/);
    assert.match(source, /precision highp float/);
    assert.match(source, /precision highp int/);
  }
  assert.match(vertex, /corners\[gl_VertexID\]/);
  assert.match(vertex, /position \/ viewport \* vec2\(2, -2\) \+ vec2\(-1, 1\)/);
  assert.match(vertex, /mix\(uvRect.xy, uvRect.zw, corner\)/);
  assert.match(fragment, /textureLod\(image, fragmentUv, 0.0\)/);
  assert.match(fragment, /fragmentTint.a \* texel.a/);
  assert.match(fragment, /fragmentColor = texel \* fragmentTint/);
  assert.equal(h.callsFor("deleteShader").length, 2);
});

test("Create_CompileAndLinkFailures_ReportDiagnosticsAndFreePartialResources", async () => {
  const shaderTypes = harness().gl;
  for (const options of [
    { compileFailure: shaderTypes.VERTEX_SHADER },
    { compileFailure: shaderTypes.FRAGMENT_SHADER },
    { linkFailure: true },
  ]) {
    const h = harness(options);
    await assert.rejects(WebGl2Backend.create(h.canvas, h.onFatal), error => {
      assert.ok(error instanceof Error);
      assert.ok(!(error instanceof RendererUnavailableError));
      assert.match(error.message, /test (shader|link) diagnostic/);
      return true;
    });
    assert.equal(h.live.size, 0);
    assert.equal(h.canvas.listeners.size, 0);
    assert.equal(h.callsFor("loseContext").length, 1);
    assert.deepEqual(h.fatalErrors, []);
  }
});

test("Create_AllocationOrUniformFailures_FreeEarlierResources", async () => {
  for (const options of [
    { nullResource: "shader" }, { nullResource: "program" }, { nullResource: "buffer" },
    { nullResource: "vertexArray" }, { missingUniform: "viewport" }, { missingUniform: "image" },
  ]) {
    const h = harness(options);
    await assert.rejects(WebGl2Backend.create(h.canvas, h.onFatal), error =>
      error instanceof Error && !(error instanceof RendererUnavailableError));
    assert.equal(h.live.size, 0);
    assert.equal(h.canvas.listeners.size, 0);
    assert.deepEqual(h.fatalErrors, []);
  }
});

test("Create_GLInitializationError_DisposesContextWithoutFatalCallback", async () => {
  const h = harness();
  h.gl.errorOn = "blendFuncSeparate";
  h.gl.errorCode = h.gl.INVALID_ENUM;
  await assert.rejects(WebGl2Backend.create(h.canvas, h.onFatal), /INVALID_ENUM/);
  assert.equal(h.live.size, 0);
  assert.equal(h.canvas.listeners.size, 0);
  assert.deepEqual(h.fatalErrors, []);
});

test("CreateTexture_EmptyAtlas_IsZeroInitializedLinearRgba8WithClampedEdges", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  const gl = h.gl;
  const resource = backend.createTexture(17, 13, "Glyph atlas");
  assert.equal(resource.width, 17);
  assert.equal(resource.height, 13);
  assert.deepEqual(h.callsFor("texImage2D"), [[gl.TEXTURE_2D, 0, gl.RGBA8, 17, 13, 0, gl.RGBA, gl.UNSIGNED_BYTE, null]]);
  assert.deepEqual(h.callsFor("texParameteri"), [
    [gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR],
    [gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR],
    [gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE],
    [gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE],
  ]);
  assert.equal(h.callsFor("texSubImage2D").length, 0);
});

test("TextureUploads_RgbaRowsAndBitmaps_PreserveStraightAlphaAndTopDownOrientation", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  const gl = h.gl;
  const resource = backend.createTexture(4, 5, "Image");
  const errorChecks = h.callsFor("getError").length;
  const pixels = new Uint8Array([200, 100, 50, 128, 10, 20, 30, 64, 1, 2, 3, 0, 9, 8, 7, 255]);
  resource.writePixels(pixels, 2, 2, 1, 3);
  const bitmap = { width: 4, height: 5 };
  resource.writeBitmap(bitmap);
  const clamped = new Uint8ClampedArray([20, 30, 40, 127]);
  resource.writePixels(clamped, 1, 1);
  assert.deepEqual(h.callsFor("texSubImage2D"), [
    [gl.TEXTURE_2D, 0, 1, 3, 2, 2, gl.RGBA, gl.UNSIGNED_BYTE, pixels],
    [gl.TEXTURE_2D, 0, 0, 0, gl.RGBA, gl.UNSIGNED_BYTE, bitmap],
    [gl.TEXTURE_2D, 0, 0, 0, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, clamped],
  ]);
  assert.equal(h.callsFor("texSubImage2D")[0].at(-1), pixels);
  assert.equal(h.callsFor("texSubImage2D")[1].at(-1), bitmap);
  const unpackState = [
    [gl.UNPACK_ALIGNMENT, 1],
    [gl.UNPACK_FLIP_Y_WEBGL, false],
    [gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false],
    [gl.UNPACK_COLORSPACE_CONVERSION_WEBGL, gl.NONE],
    [gl.UNPACK_ROW_LENGTH, 0],
    [gl.UNPACK_SKIP_PIXELS, 0],
    [gl.UNPACK_SKIP_ROWS, 0],
  ];
  assert.deepEqual(h.callsFor("pixelStorei"), Array.from({ length: 4 }, () => unpackState).flat());
  assert.equal(h.callsFor("getError").length, errorChecks, "Do not synchronize GL errors per glyph upload");
});

test("CreateTexture_InvalidDimensions_AreRejectedBeforeAllocation", async t => {
  const h = harness({ textureLimit: 256 });
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  for (const [width, height] of [[0, 1], [-1, 2], [1.5, 2], [1, NaN], [Infinity, 1], [257, 1], [1, 257]]) {
    assert.throws(() => backend.createTexture(width, height, "Test atlas"), /Test atlas.*dimensions|Test atlas.*limit/);
  }
  assert.equal(h.callsFor("createTexture").length, 0);
  const resource = backend.createTexture(256, 256, "Maximum atlas");
  assert.equal(resource.width, 256);
});

test("TextureUploads_InvalidRegionsAndDestroyedTextures_AreRejected", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  const resource = backend.createTexture(2, 2, "Image");
  const pixels = new Uint8Array(16);
  for (const dimensions of [[3, 2], [2, 2, 1, 0], [2, 2, 0, -1], [0, 1], [1, 1, 0.5, 0]]) {
    assert.throws(() => resource.writePixels(pixels, ...dimensions), /Invalid WebGL2 texture upload/);
  }
  assert.throws(() => resource.writePixels(new Uint8Array(3), 1, 1), /pixel data/);
  assert.throws(() => resource.writeBitmap({ width: 3, height: 2 }), /bitmap dimensions/);
  resource.destroy();
  resource.destroy();
  assert.throws(() => resource.writePixels(pixels, 2, 2), /destroyed/);
  assert.throws(() => resource.writeBitmap({ width: 2, height: 2 }), /destroyed/);
  assert.equal(h.callsFor("texSubImage2D").length, 0);
  assert.equal(h.callsFor("deleteTexture").length, 1);
});

test("CreateTexture_NullAllocationAndStorageErrors_DoNotLeakTextureHandles", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  const originalResources = h.live.size;
  h.gl.nullResource = "texture";
  assert.throws(() => backend.createTexture(1, 1, "Image"), /Could not allocate WebGL2 texture/);
  delete h.gl.nullResource;
  h.gl.errorOn = "texImage2D";
  h.gl.errorCode = h.gl.OUT_OF_MEMORY;
  assert.throws(() => backend.createTexture(16, 16, "Image"), /OUT_OF_MEMORY/);
  assert.equal(h.live.size, originalResources);
  delete h.gl.errorOn;
  h.gl.throwOn = "texParameteri";
  assert.throws(() => backend.createTexture(16, 16, "Image"), /texParameteri threw/);
  assert.equal(h.live.size, originalResources);
  assert.equal(h.callsFor("deleteTexture").length, 2);
});

test("Resize_LimitsIncludeTextureRenderbufferAndBothViewportAxes", async () => {
  for (const limits of [
    { textureLimit: 512 },
    { renderbufferLimit: 512 },
    { viewportLimit: [512, 2048] },
    { viewportLimit: [2048, 512] },
  ]) {
    const h = harness(limits);
    const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
    try {
      assert.equal(backend.maxTextureDimension2D, limits.textureLimit ?? 8192);
      assert.equal(backend.maxCanvasDimension2D, 512);
      h.canvas.width = 512;
      h.canvas.height = 256;
      backend.resize(20000, 10000);
      assert.deepEqual(h.callsFor("viewport").at(-1), [0, 0, 512, 256]);
      assert.deepEqual(h.callsFor("uniform2f").at(-1), [{ name: "viewport" }, 20000, 10000]);
      h.canvas.width = 513;
      assert.throws(() => backend.resize(20000, 10000), /drawing buffer dimension limit/);
      h.canvas.width = 256;
      h.canvas.height = 513;
      assert.throws(() => backend.resize(20000, 10000), /drawing buffer dimension limit/);
    } finally {
      backend.dispose();
    }
  }
});

test("Resize_InvalidLogicalDimensionsAndUndersizedDrawingBuffer_SurfaceErrors", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  for (const dimensions of [[0, 1], [1, -1], [NaN, 1], [1, Infinity]]) {
    assert.throws(() => backend.resize(...dimensions), /logical viewport dimensions/);
  }
  h.gl.bufferWidth = h.canvas.width - 1;
  assert.throws(() => backend.resize(640, 320), /could not allocate.*drawing buffer/);
  delete h.gl.bufferWidth;
  h.canvas.width = 0;
  assert.throws(() => backend.resize(640, 320), /drawing buffer dimension limit/);
});

test("Submit_OrderedBatches_RebaseEveryAttributeWithoutBaseInstance", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  const gl = h.gl;
  const atlas = backend.createTexture(1, 1, "Atlas");
  const image = backend.createTexture(1, 1, "Image");
  const instances = new Float32Array(8 * QUAD_STRIDE);
  const batches = [
    { resource: atlas, start: 0, count: 2 },
    { resource: image, start: 2, count: 3 },
    { resource: atlas, start: 5, count: 1 },
  ];
  const start = h.calls.length;
  backend.submit(instances, 6, batches, [0.1, 0.2, 0.3, 0.4]);
  assert.deepEqual(h.callsFor("vertexAttribPointer"), [0, 2, 5].flatMap(base => [
    [0, 4, gl.FLOAT, false, 64, base * 64],
    [1, 4, gl.FLOAT, false, 64, base * 64 + 16],
    [2, 4, gl.FLOAT, false, 64, base * 64 + 32],
    [3, 1, gl.FLOAT, false, 64, base * 64 + 48],
  ]));
  assert.deepEqual(h.callsFor("drawArraysInstanced"), [
    [gl.TRIANGLES, 0, 6, 2], [gl.TRIANGLES, 0, 6, 3], [gl.TRIANGLES, 0, 6, 1],
  ]);
  const [atlasHandle, imageHandle] = h.callsFor("texImage2D").map((_, i) => h.callsFor("bindTexture")[i][1]);
  assert.deepEqual(h.calls.slice(start).filter(call => call.name === "bindTexture").map(call => call.args[1]),
    [atlasHandle, imageHandle, atlasHandle]);
  assert.deepEqual(h.callsFor("clearColor"), [[0.1, 0.2, 0.3, 1]]);
  assert.deepEqual(h.callsFor("bufferSubData"), [[gl.ARRAY_BUFFER, 0, instances, 0, 6 * QUAD_STRIDE]]);
  assert.equal(h.callsFor("bufferSubData")[0][2], instances);
});

test("Submit_BufferGrowth_UsesSharedArrayCapacityAndOnlyUploadsUsedInstances", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  const small = new Float32Array(8 * QUAD_STRIDE);
  const large = new Float32Array(32 * QUAD_STRIDE);
  backend.submit(small, 1, [], [0, 0, 0, 1]);
  assert.equal(backend.instanceBufferBytes, small.byteLength);
  backend.submit(large, 8, [], [0, 0, 0, 1]);
  assert.equal(backend.instanceBufferBytes, small.byteLength);
  backend.submit(large, 9, [], [0, 0, 0, 1]);
  assert.equal(backend.instanceBufferBytes, large.byteLength);
  backend.submit(small, 1, [], [0, 0, 0, 1]);
  assert.equal(backend.instanceBufferBytes, large.byteLength);
  assert.deepEqual(h.callsFor("bufferData").map(([, bytes]) => bytes), [small.byteLength, large.byteLength]);
  assert.deepEqual(h.callsFor("bufferSubData").map(args => args.at(-1)), [16, 128, 144, 16]);
});

test("Submit_EmptyFrame_ClearsOpaqueWithoutUploadingOrDrawing", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  backend.submit(new Float32Array(), 0, [], [0.2, 0.4, 0.6, 0]);
  assert.equal(backend.instanceBufferBytes, 256);
  assert.deepEqual(h.callsFor("clearColor"), [[0.2, 0.4, 0.6, 1]]);
  assert.deepEqual(h.callsFor("clear"), [[h.gl.COLOR_BUFFER_BIT]]);
  assert.equal(h.callsFor("bufferSubData").length, 0);
  assert.equal(h.callsFor("drawArraysInstanced").length, 0);
});

test("Submit_ForeignOrDestroyedTextures_AreRejectedBeforeDrawing", async t => {
  const h = harness();
  const other = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  const otherBackend = await WebGl2Backend.create(other.canvas, other.onFatal);
  t.after(() => { backend.dispose(); otherBackend.dispose(); });
  const instances = new Float32Array(QUAD_STRIDE);
  const foreign = otherBackend.createTexture(1, 1, "Other context");
  for (const resource of [{}, foreign]) {
    assert.throws(() => backend.submit(instances, 1, [{ resource, start: 0, count: 1 }], [0, 0, 0, 1]),
      /different rendering backend/);
  }
  const resource = backend.createTexture(1, 1, "Image");
  resource.destroy();
  assert.throws(() => backend.submit(instances, 1, [{ resource, start: 0, count: 1 }], [0, 0, 0, 1]), /destroyed/);
  assert.equal(h.callsFor("drawArraysInstanced").length, 0);
});

test("Submit_InvalidInstanceAndBatchRanges_AreRejected", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  const instances = new Float32Array(QUAD_STRIDE);
  const resource = backend.createTexture(1, 1, "Atlas");
  for (const count of [-1, 0.5, NaN, Infinity, 2]) {
    assert.throws(() => backend.submit(instances, count, [], [0, 0, 0, 1]), /instance data/);
  }
  for (const [start, count] of [[-1, 1], [0, -1], [0, 2], [1, 1], [0.5, 0], [NaN, 1], [0, Infinity]]) {
    assert.throws(() => backend.submit(instances, 1, [{ resource, start, count }], [0, 0, 0, 1]), /batch instance range/);
  }
  backend.submit(instances, 1, [{ resource, start: 1, count: 0 }], [0, 0, 0, 1]);
  assert.equal(h.callsFor("drawArraysInstanced").length, 0);
});

test("Submit_DeferredUploadAndDrawErrors_SurfaceAtFrameBoundary", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  const resource = backend.createTexture(1, 1, "Atlas");
  const instances = new Float32Array(QUAD_STRIDE);
  const batch = { resource, start: 0, count: 1 };
  backend.submit(instances, 1, [batch], [0, 0, 0, 1]);
  h.gl.errorOn = "texSubImage2D";
  resource.writePixels(new Uint8Array(4), 1, 1);
  assert.throws(() => backend.submit(instances, 1, [batch], [0, 0, 0, 1]), /frame submission failed: INVALID_OPERATION/);
  h.gl.errorOn = "drawArraysInstanced";
  h.gl.errorCode = h.gl.INVALID_FRAMEBUFFER_OPERATION;
  assert.throws(() => backend.submit(instances, 1, [batch], [0, 0, 0, 1]), /INVALID_FRAMEBUFFER_OPERATION/);
});

test("Submit_BufferAllocationError_DoesNotReportUnallocatedCapacity", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  h.gl.errorOn = "bufferData";
  h.gl.errorCode = h.gl.OUT_OF_MEMORY;
  assert.throws(() => backend.submit(new Float32Array(QUAD_STRIDE), 1, [], [0, 0, 0, 1]), /OUT_OF_MEMORY/);
  assert.equal(backend.instanceBufferBytes, 0);
  assert.equal(h.callsFor("drawArraysInstanced").length, 0);
});

test("Idle_CompletedFence_FlushesAndDeletesSyncWithoutBlocking", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  for (const result of [h.gl.ALREADY_SIGNALED, h.gl.CONDITION_SATISFIED]) {
    h.gl.waitResults.push(result);
    await backend.idle();
  }
  assert.deepEqual(h.callsFor("fenceSync"), [
    [h.gl.SYNC_GPU_COMMANDS_COMPLETE, 0], [h.gl.SYNC_GPU_COMMANDS_COMPLETE, 0],
  ]);
  assert.equal(h.callsFor("flush").length, 2);
  assert.equal(h.callsFor("deleteSync").length, 2);
  for (const [, flags, timeout] of h.callsFor("clientWaitSync")) {
    assert.equal(flags, 0);
    assert.equal(timeout, 0);
  }
  assert.equal(h.callsFor("finish").length, 0);
  assert.ok(![...h.live].some(resource => resource.kind === "sync"));
});

test("Idle_UnsignaledFence_YieldsToEventLoopAndMaintainsBackpressure", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  h.gl.waitResult = h.gl.TIMEOUT_EXPIRED;
  let completed = false;
  const idle = backend.idle().then(() => { completed = true; });
  await Promise.resolve();
  assert.equal(completed, false, "A resolved promise is not GPU completion");
  assert.equal(h.callsFor("deleteSync").length, 0);
  assert.equal(h.callsFor("clientWaitSync").length, 1);
  h.gl.waitResult = h.gl.CONDITION_SATISFIED;
  await idle;
  assert.equal(completed, true);
  assert.equal(h.callsFor("clientWaitSync").length, 2);
  assert.equal(h.callsFor("deleteSync").length, 1);
  assert.ok(h.callsFor("clientWaitSync").every(([, flags, timeout]) => flags === 0 && timeout === 0));
});

test("Idle_ConcurrentWaits_CompleteAndDeleteTheirOwnFences", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  h.gl.waitResult = h.gl.TIMEOUT_EXPIRED;
  const first = backend.idle();
  const second = backend.idle();
  assert.equal(h.callsFor("fenceSync").length, 2);
  h.gl.waitResult = h.gl.ALREADY_SIGNALED;
  await Promise.all([first, second]);
  const deleted = h.callsFor("deleteSync").map(([sync]) => sync);
  assert.equal(new Set(deleted).size, 2);
  assert.ok(deleted.every(sync => !h.live.has(sync)));
});

test("Idle_NullFenceAndFailedWait_RejectWithoutSyncLeaks", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  h.gl.nullResource = "sync";
  await assert.rejects(backend.idle(), /Could not allocate.*GPU fence/);
  delete h.gl.nullResource;
  h.gl.waitResult = h.gl.WAIT_FAILED;
  await assert.rejects(backend.idle(), /GPU fence wait failed/);
  assert.equal(h.callsFor("deleteSync").length, 1);
  assert.ok(![...h.live].some(resource => resource.kind === "sync"));
});

test("Idle_UploadOrFenceErrors_RejectAndFreeAllocatedSyncs", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  h.gl.errors.push(h.gl.INVALID_VALUE);
  await assert.rejects(backend.idle(), /INVALID_VALUE/);
  assert.equal(h.callsFor("fenceSync").length, 0);
  h.gl.errorOn = "flush";
  await assert.rejects(backend.idle(), /GPU fence submission failed: INVALID_OPERATION/);
  delete h.gl.errorOn;
  h.gl.throwOn = "flush";
  await assert.rejects(backend.idle(), /flush threw/);
  delete h.gl.throwOn;
  h.gl.waitResult = h.gl.WAIT_FAILED;
  h.gl.errorOn = "clientWaitSync";
  await assert.rejects(backend.idle(), /GPU fence wait failed: INVALID_OPERATION/);
  assert.equal(h.callsFor("deleteSync").length, 3);
  assert.ok(![...h.live].some(resource => resource.kind === "sync"));
});

test("Idle_ErrorDuringPendingCompletion_IsNotSilentlyIgnored", async t => {
  t.mock.timers.enable({ apis: ["setTimeout"] });
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  h.gl.waitResults.push(h.gl.TIMEOUT_EXPIRED, h.gl.ALREADY_SIGNALED);
  const idle = backend.idle();
  const rejected = assert.rejects(idle, /OUT_OF_MEMORY/);
  h.gl.errors.push(h.gl.OUT_OF_MEMORY);
  t.mock.timers.tick(1);
  await rejected;
  assert.equal(h.callsFor("deleteSync").length, 1);
});

test("Dispose_PendingIdle_RejectsAllWaitsAndCancelsTheirTimers", async t => {
  t.mock.timers.enable({ apis: ["setTimeout"] });
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  h.gl.waitResult = h.gl.TIMEOUT_EXPIRED;
  const first = assert.rejects(backend.idle(), /disposed/);
  const second = assert.rejects(backend.idle(), /disposed/);
  backend.dispose();
  await Promise.all([first, second]);
  const polls = h.callsFor("clientWaitSync").length;
  t.mock.timers.tick(100);
  assert.equal(h.callsFor("clientWaitSync").length, polls);
  assert.equal(h.callsFor("deleteSync").length, 2);
  assert.equal(h.live.size, 0);
  assert.deepEqual(h.fatalErrors, []);
});

test("ContextLoss_PendingIdle_ReportsFatalOnceAndCancelsTimersWithoutFallback", async t => {
  t.mock.timers.enable({ apis: ["setTimeout"] });
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  h.gl.waitResult = h.gl.TIMEOUT_EXPIRED;
  const idle = assert.rejects(backend.idle(), /context lost: driver reset/);
  h.emitLoss();
  h.emitLoss("second notification");
  await idle;
  t.mock.timers.tick(100);
  assert.equal(h.fatalErrors.length, 1);
  assert.match(h.fatalErrors[0].message, /context lost: driver reset/);
  assert.equal(h.callsFor("clientWaitSync").length, 1);
  assert.equal(h.callsFor("deleteSync").length, 1);
  assert.equal(h.canvas.requests.length, 1);
  assert.throws(() => backend.createTexture(1, 1, "Image"), error => error === h.fatalErrors[0]);
  assert.throws(() => backend.submit(new Float32Array(), 0, [], [0, 0, 0, 1]), /context lost/);
  assert.throws(() => backend.resize(640, 320), /context lost/);
  await assert.rejects(backend.idle(), /context lost/);
});

test("ContextLoss_BeforeLossEvent_IsDetectedByFencePolling", async t => {
  t.mock.timers.enable({ apis: ["setTimeout"] });
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  h.gl.waitResult = h.gl.TIMEOUT_EXPIRED;
  const idle = assert.rejects(backend.idle(), /context lost/);
  h.gl.lost = true;
  t.mock.timers.tick(1);
  await idle;
  h.emitLoss();
  t.mock.timers.tick(100);
  assert.equal(h.fatalErrors.length, 1);
  assert.equal(h.callsFor("clientWaitSync").length, 1);
  assert.equal(h.callsFor("deleteSync").length, 1);
});

test("ContextLoss_GLErrorCode_ReportsFatalBeforeTheEvent", async t => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  t.after(() => backend.dispose());
  h.gl.errors.push(h.gl.CONTEXT_LOST_WEBGL);
  await assert.rejects(backend.idle(), /context lost/);
  h.emitLoss();
  assert.equal(h.fatalErrors.length, 1);
  assert.equal(h.callsFor("fenceSync").length, 0);
});

test("Dispose_RepeatedDisposal_FreesOwnedResourcesAndSuppressesIntentionalLoss", async () => {
  const h = harness();
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  const first = backend.createTexture(1, 1, "First");
  const second = backend.createTexture(1, 1, "Second");
  first.destroy();
  backend.submit(new Float32Array(QUAD_STRIDE), 1, [{ resource: second, start: 0, count: 1 }], [0, 0, 0, 1]);
  backend.dispose();
  backend.dispose();
  second.destroy();
  h.emitLoss();
  assert.equal(h.live.size, 0);
  assert.equal(backend.instanceBufferBytes, 0);
  assert.equal(h.callsFor("deleteShader").length, 2);
  assert.equal(h.callsFor("deleteProgram").length, 1);
  assert.equal(h.callsFor("deleteBuffer").length, 1);
  assert.equal(h.callsFor("deleteVertexArray").length, 1);
  assert.equal(h.callsFor("deleteTexture").length, 2);
  assert.equal(h.callsFor("loseContext").length, 1);
  assert.equal(h.canvas.listeners.size, 0);
  assert.deepEqual(h.fatalErrors, []);
  assert.throws(() => backend.createTexture(1, 1, "Image"), /disposed/);
  assert.throws(() => second.writePixels(new Uint8Array(4), 1, 1), /disposed/);
  assert.throws(() => backend.resize(640, 320), /disposed/);
  assert.throws(() => backend.submit(new Float32Array(), 0, [], [0, 0, 0, 1]), /disposed/);
  await assert.rejects(backend.idle(), /disposed/);
});

test("Dispose_MissingLoseContextExtension_StillFreesAllResources", async () => {
  const h = harness({ noLoseExtension: true });
  const backend = await WebGl2Backend.create(h.canvas, h.onFatal);
  backend.dispose();
  assert.equal(h.live.size, 0);
  assert.equal(h.canvas.listeners.size, 0);
  assert.deepEqual(h.fatalErrors, []);
});
