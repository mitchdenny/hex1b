async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const browser = page.context().browser();
  const context = await browser.newContext({ viewport: { width: 1000, height: 900 }, deviceScaleFactor: 2 });
  const test = await context.newPage();
  const errors = [];
  test.on("pageerror", error => errors.push(error.message));
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  try {
    await test.goto(`${origin}/health`);
    const parity = await test.evaluate(async () => {
      const { TerminalRenderer } = await import("/web-terminal/renderer.js");
      const check = (condition, message) => { if (!condition) throw new Error(message); };
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
      const rgba = new Uint8Array([
        255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255,
        255, 255, 0, 128, 255, 0, 255, 128, 0, 255, 255, 128,
        255, 255, 255, 255, 128, 64, 32, 255, 255, 0, 0, 0
      ]);
      const pngCanvas = new OffscreenCanvas(3, 3);
      pngCanvas.getContext("2d").putImageData(new ImageData(new Uint8ClampedArray(rgba), 3, 3), 0, 0);
      const png = new Uint8Array(await (await pngCanvas.convertToBlob({ type: "image/png" })).arrayBuffer());
      const images = [
        { key: "rgba", width: 3, height: 3, format: "rgba", bytes: rgba, byteLength: rgba.length },
        { key: "png", width: 3, height: 3, format: "png", bytes: png, byteLength: png.length }
      ];
      const cells = Array.from({ length: 96 }, (_, index) => ({
        index, text: ["A", "\u250c", "\u2500", "\u2510", "\ue0b0", "\uf120", "\u2588", "\ud83d\ude00"][index % 8],
        width: index % 12 === 10 ? 2 : index % 12 === 11 ? 0 : 1,
        foreground: 0xffb4dcf0, background: index < 12 ? 0xff203040 : 0x80304050,
        underlineColor: 0xff40e080, attributes: [0, 1, 4, 2, 16, 64, 128, 256][Math.floor(index / 12)],
        underlineStyle: index % 6
      }));
      const placement = (key, x, y, width, height, z) => ({
        key, kind: "kgp", x, y, width, height, sourceX: 0, sourceY: 0, sourceWidth: 3, sourceHeight: 3,
        clipX: 0, clipY: 0, clipWidth: 120, clipHeight: 160, z
      });
      const metadata = {
        defaultBackground: 0xff081018, cursor: { visible: true, shape: 2, x: 5, y: 5 },
        placements: [
          placement("rgba", 0, 0, 120, 160, -2147483648),
          { ...placement("png", 40, 40, 35, 35, -1), kind: "sixel" },
          placement("rgba", 100, 4, 3, 3, 1),
          placement("png", 75, 100, 33, 33, 2),
          { ...placement("rgba", 90, 110, 35, 35, 3), sourceX: -1, sourceY: 1,
            sourceWidth: 5, sourceHeight: 3, clipX: 100, clipY: 105, clipWidth: 17, clipHeight: 30 },
          placement("png", 12, 60, 15, 20, 4)
        ]
      };
      const reports = [];
      for (const scale of [1, 1.25, 1.5, 2, 3]) {
        const rendered = [];
        const frameEdges = [];
        for (const kind of ["webgpu", "webgl2"]) {
          const canvas = document.createElement("canvas");
          document.body.append(canvas);
          const failures = [];
          const renderer = await TerminalRenderer.create(canvas, scale, error => failures.push(error.message), undefined, kind);
          try {
            check(renderer.metrics().renderer === kind && !renderer.metrics().rendererFallbackReason, "Forced renderer was ignored");
            renderer.resize(12, 8);
            if (kind === "webgpu") {
              const { device, context, format } = renderer.backend;
              context.configure({ device, format, alphaMode: "opaque",
                usage: GPUTextureUsage.RENDER_ATTACHMENT | GPUTextureUsage.COPY_SRC });
            }
            await renderer.updateImages(images, ["rgba", "png"]);
            renderer.prepareGlyphs(cells);
            const frames = [];
            const capture = async () => {
              if (kind === "webgpu") {
                const sx = canvas.width / renderer.width, sy = canvas.height / renderer.height;
                const edges = [];
                for (let i = 0; i < renderer.quadCount; i++) {
                  const [x, y, width, height] = renderer.instances.subarray(i * 16, i * 16 + 4);
                  edges.push([x * sx, (x + width) * sx, y * sy], [x * sx, (x + width) * sx, (y + height) * sy]);
                }
                frameEdges.push({ width: canvas.width, edges });
              }
              frames.push(await pixels(renderer));
            };
            for (const blink of [true, false]) {
              renderer.render(cells, metadata, blink);
              await capture();
            }
            if (scale === 1 || scale === 2) {
              const offset = (4 * scale * canvas.width + 100 * scale) * 4;
              check(frames[0][offset] > 240 && frames[0][offset + 1] < 10 && frames[0][offset + 2] < 10,
                `${kind} raw image orientation/position changed`);
            }
            const initial = renderer.metrics();
            await renderer.updateImages([], ["rgba", "png"]);
            renderer.prepareGlyphs(cells);
            renderer.resize(12, 8, { width: 60, height: 80 });
            renderer.render(cells, metadata, true);
            await capture();
            const thumbnail = renderer.metrics();
            check(thumbnail.imageUploadBytes === initial.imageUploadBytes &&
              thumbnail.glyphUploadBytes === initial.glyphUploadBytes, "Resize reuploaded retained resources");
            renderer.resize(1000, 200);
            check(canvas.width <= renderer.backend.maxCanvasDimension2D &&
              canvas.height <= renderer.backend.maxCanvasDimension2D, "Canvas exceeded backend limits");
            renderer.resize(12, 8, { width: 0, height: 0 });
            renderer.render(cells, metadata, true);
            await renderer.idle();
            check(canvas.width === 1 && canvas.height === 1, "Hidden canvas was not bounded");
            renderer.resize(12, 8);
            renderer.render(cells, metadata, true);
            await capture();
            await renderer.updateImages([], []);
            check(renderer.metrics().textureBytes === 0, "Released image textures were retained");
            check(failures.length === 0, failures.join("; "));
            rendered.push(frames);
          } finally { renderer.dispose(); canvas.remove(); }
        }
        let maxDifference = 0;
        let edgeTiePixels = 0;
        for (let f = 0; f < rendered[0].length; f++) {
          const a = rendered[0][f], b = rendered[1][f];
          check(a.length === b.length, "Framebuffer dimensions differ");
          for (let i = 0; i < a.length; i += 4) {
            const difference = Math.max(...[0, 1, 2, 3].map(channel => Math.abs(a[i + channel] - b[i + channel])));
            if (difference <= 2) { maxDifference = Math.max(maxDifference, difference); continue; }
            const { width, edges } = frameEdges[f];
            const x = (i / 4) % width + .5, y = Math.floor(i / 4 / width) + .5;
            // WebGPU and OpenGL include opposite horizontal edges when a quad
            // ends exactly on a pixel center. Only those rasterization ties may differ.
            const tie = edges.some(([left, right, edge]) => x >= left && x <= right && Math.abs(y - edge) < .00001);
            check(tie, `Non-edge pixel differs by ${difference} at scale ${scale}, frame ${f}, (${x}, ${y}): ` +
              `${[...a.slice(i, i + 4)]} vs ${[...b.slice(i, i + 4)]}`);
            edgeTiePixels++;
          }
        }
        reports.push({ scale, frames: rendered[0].length, maxNonEdgeChannelDifference: maxDifference, edgeTiePixels });
      }
      return reports;
    });

    // Serve the same first-party assets under an untrustworthy HTTP origin, without
    // exposing the shell demo or changing the browser's secure-context settings.
    const httpOrigin = "http://terminal-renderer.test";
    await context.route(`${httpOrigin}/**`, async route => {
      const path = route.request().url().slice(httpOrigin.length);
      const response = await route.fetch({ url: `${origin}${path}` });
      await route.fulfill({ response });
    });
    let workerSource;
    {
      const metadata = {
        version: 1, revision: 1, baseRevision: 0, full: true,
        columns: 20, rows: 10, cellWidth: 10, cellHeight: 20, mouseTracking: 0,
        peer: { id: null, primaryId: null, isPrimary: true },
        cursor: { x: 0, y: 0, visible: false, shape: 0 }, history: null,
        images: [], retainedImages: [], placements: [], warnings: [], hyperlinks: [], title: "",
        progress: { state: "none", percentage: null }, shellIntegration: { phase: "unknown", lastExitCode: null },
        stats: { workloadBytes: 0, outputBatches: 0, captureMs: 0, elapsedMs: 0 }
      };
      const json = Uint8Array.from(JSON.stringify(metadata), character => character.charCodeAt(0));
      const cellCount = metadata.columns * metadata.rows;
      const bytes = new Uint8Array(8 + json.length + 4 + cellCount * 23);
      const view = new DataView(bytes.buffer);
      view.setUint32(0, 0x31545748, true);
      view.setUint32(4, json.length, true);
      bytes.set(json, 8);
      const offset = 8 + json.length;
      view.setUint32(offset, cellCount, true);
      for (let i = 0; i < cellCount; i++) {
        const cell = offset + 4 + i * 23;
        view.setUint32(cell, i, true);
        view.setUint32(cell + 4, 0xffffffff, true);
        view.setUint32(cell + 8, 0xff000000, true);
        view.setUint8(cell + 18, 1);
        view.setUint16(cell + 20, 1, true);
        bytes[cell + 22] = i === 0 ? 65 : 32;
      }
      // Playwright WebSocket routing does not reliably intercept dedicated-worker
      // sockets. Mock only that transport; run the actual HWT1 worker and GPU code.
      workerSource = `
          const pending = [];
          const capture = event => pending.push(event.data);
          self.addEventListener("message", capture);
          self.WebSocket = class extends EventTarget {
            static OPEN = 1;
            readyState = 0;
            constructor() {
              super();
              queueMicrotask(() => {
                this.readyState = 1;
                this.dispatchEvent(new Event("open"));
                this.dispatchEvent(new MessageEvent("message", {
                  data: new Uint8Array(${JSON.stringify([...bytes])}).buffer
                }));
              });
            }
            send(message) {
              if (JSON.parse(message).type === "ack") self.postMessage({
                type: "status", message: "Fixture frame acknowledged", level: "info"
              });
            }
            close() { this.readyState = 3; }
          };
          await import("${httpOrigin}/web-terminal/terminal-worker.js");
          self.removeEventListener("message", capture);
          for (const data of pending) self.dispatchEvent(new MessageEvent("message", { data }));
        `;
    }
    await test.goto(`${httpOrigin}/health`);
    const http = await test.evaluate(async workerSource => {
      if (isSecureContext || navigator.gpu) throw new Error("HTTP fixture unexpectedly has WebGPU privileges");
      const { WebTerminal } = await import("/web-terminal/index.js");
      const results = [];
      const workerUrl = URL.createObjectURL(new Blob([workerSource], { type: "text/javascript" }));
      try {
        for (const preference of ["auto", "webgl2", "webgpu"]) {
          const host = document.createElement("div");
          host.style.cssText = "width:200px;height:200px";
          document.body.append(host);
          try {
            if (preference === "webgpu") {
              let message;
              try { await WebTerminal.mount(host, { url: "/ws", renderer: preference }); }
              catch (error) { message = error.message; }
              if (!message?.includes("HTTPS") || host.children.length) throw new Error("Forced WebGPU did not fail cleanly on HTTP");
              results.push({ preference, error: message });
            } else {
              const status = [];
              let terminal;
              try {
                terminal = await WebTerminal.mount(host, {
                  url: "/ws", workerUrl, renderer: preference,
                  onStatus: message => status.push(message)
                });
              } catch (error) {
                throw new Error(`${preference}: ${error.message}; status: ${status.join("; ")}`);
              }
              try {
                const stats = terminal.stats;
                if (stats.renderer !== "webgl2" || terminal.screenText.trim() !== "A") {
                  throw new Error(`HTTP worker did not present WebGL2 content: ${JSON.stringify(stats)}`);
                }
                if (!!stats.rendererFallbackReason !== (preference === "auto")) throw new Error("Incorrect fallback diagnostics");
                if (!status.includes("Fixture frame acknowledged")) throw new Error("Worker did not acknowledge its rendered frame");
                results.push({ preference, renderer: stats.renderer, fallback: stats.rendererFallbackReason, frames: stats.frames });
              } finally { terminal.dispose(); }
              if (host.children.length) throw new Error("Disposed mount leaked its wrapper");
            }
          } finally { host.remove(); }
        }
        return results;
      } finally { URL.revokeObjectURL(workerUrl); }
    }, workerSource);
    check(errors.length === 0, errors.join("; "));
    return { passed: true, parity, http };
  } finally { await context.close(); }
}
