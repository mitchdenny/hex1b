// Run against WebTerminalDemo with:
// playwright-cli -s=<session> run-code "$(cat samples/WebTerminalDemo/tests/graphics-placeholders.browser.js)"
async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({ deviceScaleFactor: 1 });
  const test = await context.newPage();
  const errors = [];
  test.on("pageerror", error => errors.push(error.message));
  try {
    await test.goto(`${origin}/health`);
    const results = await test.evaluate(async () => {
      const { TerminalRenderer } = await import("/web-terminal/renderer.js");
      const check = (condition, message) => { if (!condition) throw new Error(message); };
      // Same native framebuffer readback as renderers.browser.js; never infer
      // image coverage from quad counts or screenshots of a scaled DOM canvas.
      async function pixels(renderer) {
        const { backend, canvas } = renderer;
        const result = new Uint8Array(canvas.width * canvas.height * 4);
        if (backend.kind === "webgl2") {
          const raw = new Uint8Array(result.length);
          const gl = backend.gl;
          gl.readPixels(0, 0, canvas.width, canvas.height, gl.RGBA, gl.UNSIGNED_BYTE, raw);
          check(gl.getError() === gl.NO_ERROR, "WebGL2 readback failed");
          for (let y = 0; y < canvas.height; y++) {
            result.set(raw.subarray((canvas.height - 1 - y) * canvas.width * 4,
              (canvas.height - y) * canvas.width * 4), y * canvas.width * 4);
          }
        } else {
          const { device, context, format } = backend;
          const bytesPerRow = Math.ceil(canvas.width * 4 / 256) * 256;
          const buffer = device.createBuffer({ size: bytesPerRow * canvas.height,
            usage: GPUBufferUsage.COPY_DST | GPUBufferUsage.MAP_READ });
          try {
            const encoder = device.createCommandEncoder();
            encoder.copyTextureToBuffer({ texture: context.getCurrentTexture() },
              { buffer, bytesPerRow }, [canvas.width, canvas.height]);
            device.queue.submit([encoder.finish()]);
            await buffer.mapAsync(GPUMapMode.READ);
            const raw = new Uint8Array(buffer.getMappedRange());
            const red = format.startsWith("bgra") ? 2 : 0;
            for (let y = 0; y < canvas.height; y++) for (let x = 0; x < canvas.width; x++) {
              const source = y * bytesPerRow + x * 4, target = (y * canvas.width + x) * 4;
              result.set([raw[source + red], raw[source + 1], raw[source + 2 - red], 255], target);
            }
          } finally { buffer.destroy(); }
        }
        await renderer.idle();
        return result;
      }
      const red = [255, 0, 0, 255], black = [0, 0, 0, 255];
      const blue = [0, 0, 255, 255], green = [0, 255, 0, 255], white = [255, 255, 255, 255];
      const cells = Object.freeze(["\u0305", "\u030d", "\u030e", "\u0310"].map((column, index) =>
        Object.freeze({
          index, text: `\u{10eeee}\u0305${column}`, width: 1,
          // Packed RGBA: RGB 0,0,1 encodes Kitty image ID 1.
          foreground: 0xff010000, background: 0xff000000,
          underlineColor: 0xff00ff00, attributes: 0, underlineStyle: 0
        })));
      const originalCells = JSON.stringify(cells);
      const rgba = new Uint8Array(40 * 20 * 4);
      for (let i = 0; i < rgba.length; i += 4) rgba.set(red, i);
      const image = { key: "red", width: 40, height: 20, format: "rgba", bytes: rgba, byteLength: rgba.length };
      // HWT's projected destination for a 4-column, 1-row virtual placement.
      const placement = Object.freeze({
        key: "red", kind: "kgp", x: 0, y: 0, width: 40, height: 20,
        sourceX: 0, sourceY: 0, sourceWidth: 40, sourceHeight: 20,
        clipX: 0, clipY: 0, clipWidth: 40, clipHeight: 20, z: -1
      });
      const metadata = Object.freeze({
        defaultBackground: 0xff000000, cursor: Object.freeze({ visible: false, shape: 0, x: 0, y: 0 }),
        placements: Object.freeze([placement])
      });
      const originalMetadata = JSON.stringify(metadata);
      const reports = [];
      for (const kind of ["webgpu", "webgl2"]) {
        const canvas = document.createElement("canvas");
        document.body.append(canvas);
        const failures = [];
        let renderer;
        try {
          renderer = await TerminalRenderer.create(canvas, 1, error => failures.push(error.message), undefined, kind);
          check(renderer.backend.kind === kind && !renderer.fallbackReason, `${kind}: forced renderer ignored`);
          renderer.resize(4, 1);
          if (kind === "webgpu") {
            const { device, context, format } = renderer.backend;
            context.configure({ device, format, alphaMode: "opaque",
              usage: GPUTextureUsage.RENDER_ATTACHMENT | GPUTextureUsage.COPY_SRC });
          }
          await renderer.updateImages([image], ["red"]);
          const capture = async (frameCells = cells, frameMetadata = metadata, blink = true) => {
            renderer.prepareGlyphs(frameCells);
            renderer.render(frameCells, frameMetadata, blink);
            return pixels(renderer);
          };
          const assertPixels = (actual, expected, label) => {
            check(actual.length === 800 * 4, `${kind}/${label}: framebuffer must be 40x20`);
            for (let i = 0; i < 800; i++) {
              const color = expected(i % 40, Math.floor(i / 40));
              check(color.every((value, channel) => actual[i * 4 + channel] === value),
                `${kind}/${label}: wrong pixel (${i % 40},${Math.floor(i / 40)}): ` +
                `${[...actual.subarray(i * 4, i * 4 + 4)]}, expected ${color}`);
            }
          };
          const initial = await capture();
          assertPixels(initial, () => red, "all 800 red pixels");
          check(renderer.metrics().atlasGlyphs === 0 && renderer.metrics().glyphUploadBytes === 0,
            `${kind}: placeholders entered the glyph atlas`);

          const ordinary = cells.map(cell => ({ ...cell, text: "A", foreground: 0xffffffff }));
          const ordinaryPixels = await capture(ordinary);
          const ordinaryRedPixels = Array.from({ length: 800 }, (_, i) =>
            red.every((value, channel) => ordinaryPixels[i * 4 + channel] === value)).filter(Boolean).length;
          check(ordinaryRedPixels > 0 && ordinaryRedPixels < 800, `${kind}: ordinary glyphs no longer paint above images`);
          check(renderer.metrics().atlasGlyphs === 1, `${kind}: ordinary glyph was not prepared`);

          // Seed a cached glyph deliberately: a preparation-only fix must not
          // accidentally pass without an independent paint-time guard.
          const ordinaryGlyph = renderer.glyphs.get("0/1/A");
          for (const cell of cells) renderer.glyphs.set(`0/1/${cell.text}`, ordinaryGlyph);
          try {
            assertPixels(await capture(), () => red, "cached placeholder glyph suppression");
          } finally {
            for (const cell of cells) renderer.glyphs.delete(`0/1/${cell.text}`);
          }

          const bare = cells.map(cell => ({ ...cell, text: "\u{10eeee}" }));
          assertPixels(await capture(bare), () => red, "bare reserved scalar");
          check(renderer.metrics().atlasGlyphs === 1, `${kind}: bare placeholder entered the atlas`);
          const neighbors = ["\u{10eeed}", "\u{10eeef}", "A\u0305", "\u0305"].map((text, index) =>
            ({ ...ordinary[index], text }));
          renderer.prepareGlyphs(neighbors);
          for (const cell of neighbors) check(renderer.glyphs.has(`0/1/${cell.text}`),
            `${kind}: non-placeholder text was suppressed: ${cell.text}`);

          assertPixels(await capture(cells, { ...metadata, placements: [{ ...placement, clipX: 10, clipWidth: 20 }] }),
            x => x >= 10 && x < 30 ? red : black, "placement clipping");
          assertPixels(await capture(cells, { ...metadata, placements: [{ ...placement, sourceX: -10 }] }),
            x => x >= 10 ? red : black, "source cropping");
          assertPixels(await capture(cells, { ...metadata, placements: [{ ...placement, z: -2147483648 }] }),
            () => black, "image below opaque backgrounds");
          assertPixels(await capture(cells.map(cell => ({ ...cell, background: 0 })), {
            ...metadata, placements: [{ ...placement, z: -2147483648 }]
          }), () => red, "image below transparent backgrounds");
          assertPixels(await capture(ordinary, { ...metadata, placements: [{ ...placement, z: 0 }] }),
            () => red, "image above ordinary glyphs");
          assertPixels(await capture(cells.map(cell => ({ ...cell, underlineStyle: 1 }))),
            (_x, y) => y === 18 ? green : red, "placeholder decorations preserved");
          assertPixels(await capture(ordinary.map(cell => ({ ...cell, attributes: 64 }))),
            () => red, "concealed ordinary glyphs");
          assertPixels(await capture(ordinary.map(cell => ({ ...cell, attributes: 16 })), metadata, false),
            () => red, "blinking ordinary glyphs");
          assertPixels(await capture(cells.map(cell => ({ ...cell, foreground: 0xffffffff })), {
            ...metadata, cursor: { visible: true, shape: 4, x: 0, y: 0 }
          }), (x, y) => x < 10 && y >= 18 ? white : red, "cursor over placeholders");

          await renderer.updateImages([], []);
          assertPixels(await capture(cells.map(cell => ({ ...cell, background: 0xffff0000 })), {
            ...metadata, placements: []
          }), () => blue, "missing image leaves placeholder background without tofu");
          check(renderer.metrics().textureBytes === 0, `${kind}: deleted image texture retained`);
          let missingPlacementError;
          try { renderer.render(cells, metadata, true); }
          catch (error) { missingPlacementError = error.message; }
          check(missingPlacementError === "Placement texture is missing: red",
            `${kind}: invalid missing-texture placement no longer fails`);
          check(JSON.stringify(cells) === originalCells && JSON.stringify(metadata) === originalMetadata,
            `${kind}: authoritative cell text/colors or placement metadata changed`);
          check(failures.length === 0, `${kind}: ${failures.join("; ")}`);

          // Observe calls to the native GPU objects, not just RenderTexture.destroy.
          // These bounded test-held references are deliberately not a GC/working-set probe.
          const backend = renderer.backend;
          const textures = new Map();
          const bufferDeletes = new Map();
          const restores = [];
          const prematureDeletes = [];
          let inFlight = false;
          const intercept = (owner, name, observe) => {
            const original = owner[name];
            const own = Object.getOwnPropertyDescriptor(owner, name);
            owner[name] = function (...args) {
              const result = original.apply(this, args);
              observe(...args);
              return result;
            };
            restores.push(() => {
              if (own) Object.defineProperty(owner, name, own);
              else delete owner[name];
            });
          };
          const deleted = texture => {
            const entry = textures.get(texture);
            if (!entry) return;
            entry.deletes++;
            if (inFlight) prematureDeletes.push(entry.label);
          };
          const watchTexture = (resource, label) => {
            textures.set(resource.texture, { label, deletes: 0 });
            if (kind === "webgpu") intercept(resource.texture, "destroy", () => deleted(resource.texture));
            return resource;
          };
          const deletedBuffer = buffer => bufferDeletes.set(buffer, (bufferDeletes.get(buffer) || 0) + 1);
          let deviceDeletes = 0;
          try {
            if (kind === "webgl2") {
              intercept(backend.gl, "deleteTexture", deleted);
              intercept(backend.gl, "deleteBuffer", deletedBuffer);
            } else {
              intercept(backend.instanceBuffer, "destroy", () => deletedBuffer(backend.instanceBuffer));
              intercept(backend.uniform, "destroy", () => deletedBuffer(backend.uniform));
              intercept(backend.device, "destroy", () => { deviceDeletes++; });
            }
            watchTexture(renderer.atlas, "atlas");
            const originalCreate = backend.createTexture;
            backend.createTexture = function (width, height, label) {
              return watchTexture(originalCreate.call(this, width, height, label), label);
            };
            restores.push(() => { backend.createTexture = originalCreate; });
            const imageEntries = () => [...textures.values()].filter(entry => entry.label !== "atlas");
            const assertCurrent = key => {
              check(renderer.images.size === 1 && renderer.images.has(key) &&
                renderer.metrics().imageCount === 1 && renderer.metrics().textureBytes === 3200,
              `${kind}/${key}: CPU image ownership did not stay at one 3200-byte texture`);
              const current = renderer.images.get(key).texture;
              for (const [texture, entry] of textures) {
                check(entry.deletes === (texture === current || entry.label === "atlas" ? 0 : 1),
                  `${kind}/${key}: incorrect native texture lifetime for ${entry.label}: ${entry.deletes}`);
                if (kind === "webgl2") check(backend.gl.isTexture(texture) === (entry.deletes === 0),
                  `${kind}/${key}: native WebGL texture liveness disagrees with deletion`);
              }
              check(backend.instanceBufferBytes > 0, `${kind}/${key}: live instance buffer disappeared`);
            };
            const generationPixels = new Uint8Array(rgba.length);
            const presentCurrent = async (key, color) => {
              const frameMetadata = { ...metadata, placements: [{ ...placement, key }] };
              renderer.prepareGlyphs(cells);
              inFlight = true;
              try {
                renderer.render(cells, frameMetadata, true);
                // Retaining an in-flight image without replacing it must not free it.
                await renderer.updateImages([], [key]);
                assertCurrent(key);
                assertPixels(await pixels(renderer), () => color, `generation ${key}`);
              } finally { inFlight = false; }
            };
            const uploadsBefore = renderer.metrics().imageUploadBytes;
            for (let generation = 0; generation < 65; generation++) {
              const key = `generation-${generation}`;
              const color = generation % 2 ? blue : red;
              for (let offset = 0; offset < generationPixels.length; offset += 4) generationPixels.set(color, offset);
              await renderer.updateImages([{ ...image, key, bytes: generationPixels }], [key]);
              assertCurrent(key);
              await presentCurrent(key, color);
            }
            check(imageEntries().length === 65 && imageEntries().filter(entry => entry.deletes === 1).length === 64,
              `${kind}: 65 image generations did not delete exactly 64 superseded native textures`);
            check(renderer.metrics().imageUploadBytes - uploadsBefore === 65 * 3200,
              `${kind}: retain-only updates retransmitted image pixels`);

            // Replacing a resource under the same key follows the same release path.
            await renderer.updateImages([{ ...image, key: "generation-64" }], ["generation-64"]);
            assertCurrent("generation-64");
            await presentCurrent("generation-64", red);

            // A still-placed shared image must survive pruning of other generations.
            const sharedKey = "generation-64";
            const sharedTexture = renderer.images.get(sharedKey).texture;
            for (let offset = 0; offset < generationPixels.length; offset += 4) generationPixels.set(blue, offset);
            for (let generation = 0; generation < 4; generation++) {
              const key = `shared-current-${generation}`;
              await renderer.updateImages([{ ...image, key, bytes: generationPixels }], [sharedKey, key]);
              check(renderer.images.size === 2 && renderer.textureBytes === 6400 &&
                renderer.images.get(sharedKey).texture === sharedTexture && textures.get(sharedTexture).deletes === 0,
              `${kind}/${key}: pruning released or replaced a shared still-referenced image`);
              const currentTexture = renderer.images.get(key).texture;
              for (const [texture, entry] of textures) {
                check(entry.deletes === (texture === currentTexture || texture === sharedTexture || entry.label === "atlas" ? 0 : 1),
                  `${kind}/${key}: shared retention leaked an obsolete generation`);
              }
              inFlight = true;
              try {
                renderer.render(cells, { ...metadata, placements: [
                  { ...placement, key, clipWidth: 20 },
                  { ...placement, key: sharedKey, clipX: 20, clipWidth: 20 }
                ] }, true);
                await renderer.updateImages([], [sharedKey, key]);
                assertPixels(await pixels(renderer), x => x < 20 ? blue : red, `shared retention ${generation}`);
              } finally { inFlight = false; }
            }
            const lastSharedCurrent = renderer.images.get("shared-current-3");
            await renderer.updateImages([], ["shared-current-3"]);
            check(renderer.images.get("shared-current-3") === lastSharedCurrent &&
              textures.get(sharedTexture).deletes === 1, `${kind}: releasing shared key disturbed retained current image`);
            assertCurrent("shared-current-3");
            await presentCurrent("shared-current-3", blue);
            await renderer.updateImages([], []);
            check(renderer.images.size === 0 && renderer.metrics().textureBytes === 0 &&
              imageEntries().every(entry => entry.deletes === 1), `${kind}: empty retention leaked a native image`);
            assertPixels(await capture(cells, { ...metadata, placements: [] }), () => black, "empty retention clears images");

            // Dispose with a live image and a submitted frame's batches still retained.
            await renderer.updateImages([{ ...image, key: "dispose-current" }], ["dispose-current"]);
            await presentCurrent("dispose-current", red);
            renderer.dispose();
            renderer.dispose();
            check(renderer.images.size === 0 && renderer.textureBytes === 0 &&
              renderer.glyphs.size === 0 && renderer.glyphKeyUnits === 0 && renderer.batches.length === 0 &&
              renderer.quadCount === 0 && renderer.instances.byteLength === 0 && renderer.fontMetrics.size === 0,
            `${kind}: disposed renderer retained CPU resources or active byte counts`);
            check([...textures.values()].every(entry => entry.deletes === 1),
              `${kind}: dispose did not delete every native image and atlas exactly once`);
            check(bufferDeletes.size === (kind === "webgpu" ? 2 : 1) &&
              [...bufferDeletes.values()].every(count => count === 1),
            `${kind}: dispose did not delete its native buffers exactly once`);
            check(kind !== "webgpu" || deviceDeletes === 1, "WebGPU device was not destroyed exactly once");
            check(kind !== "webgl2" || backend.textures.size === 0, "WebGL2 retained destroyed texture wrappers");
            check(prematureDeletes.length === 0, `${kind}: resources deleted in flight: ${prematureDeletes}`);
            check(failures.length === 0, `${kind}: ${failures.join("; ")}`);
            reports.push({
              renderer: kind, redPixels: 800, expectedRedPixels: 800, ordinaryRedPixels,
              lifetime: { successiveKeys: 65, singleKeyImageCount: 1, singleKeyImageBytes: 3200,
                sharedGenerations: 4, sharedImageCount: 2, sharedImageBytes: 6400,
                nativeImageDeletes: imageEntries().reduce((sum, entry) => sum + entry.deletes, 0),
                nativeAtlasDeletes: textures.get(renderer.atlas.texture).deletes,
                nativeBufferDeletes: [...bufferDeletes.values()].reduce((sum, count) => sum + count, 0),
                prematureDeletes: prematureDeletes.length, disposedImageBytes: renderer.textureBytes }
            });
          } finally {
            renderer.dispose();
            for (const restore of restores.reverse()) restore();
          }
        } finally {
          renderer?.dispose();
          canvas.remove();
        }
      }
      return reports;
    });
    if (errors.length) throw new Error(errors.join("; "));
    return { passed: true, results };
  } finally { await context.close(); }
}
