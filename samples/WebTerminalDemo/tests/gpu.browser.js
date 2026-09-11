async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({ viewport: { width: 1000, height: 900 }, deviceScaleFactor: 2 });
  const test = await context.newPage();
  try {
    await test.goto(`${origin}/health`);
    await test.setContent('<canvas id="terminal" style="width:400px;height:400px"></canvas>');
    return await test.evaluate(async () => {
      const { TerminalRenderer } = await import("/web-terminal/renderer.js");
      const errors = [];
      const canvas = document.querySelector("canvas");
      const renderer = await TerminalRenderer.create(canvas, 2, error => errors.push(error.message), undefined, "webgpu");
      const { context, device, format } = renderer.backend;
      const check = (condition, message) => { if (!condition) throw new Error(message); };
      try {
        renderer.resize(40, 20, { width: 800, height: 800 });
        const bytes = new Uint8Array(3 * 3 * 4);
        for (let i = 0; i < bytes.length; i += 4) { bytes[i] = 255; bytes[i + 3] = 255; }
        await renderer.updateImages([{ key: "native", width: 3, height: 3, format: "rgba", byteLength: bytes.length, bytes }], ["native"]);
        const cells = [{ text: "A", width: 1, foreground: 0xffffffff, background: 0xff000000, underlineColor: 0xffffffff, attributes: 0, underlineStyle: 0 }];
        renderer.prepareGlyphs(cells);
        context.configure({ device, format, alphaMode: "opaque",
          usage: GPUTextureUsage.RENDER_ATTACHMENT | GPUTextureUsage.COPY_SRC });
        const metadata = {
          defaultBackground: 0xff000000,
          cursor: { visible: false, shape: 0, x: 0, y: 0 },
          placements: [{ key: "native", kind: "kgp", x: 20, y: 40, width: 3, height: 3,
            sourceX: 0, sourceY: 0, sourceWidth: 3, sourceHeight: 3,
            clipX: 0, clipY: 0, clipWidth: 400, clipHeight: 400, z: 1 }]
        };
        renderer.render(cells, metadata, true);
        const bytesPerRow = Math.ceil(canvas.width * 4 / 256) * 256;
        const readback = device.createBuffer({ size: bytesPerRow * canvas.height,
          usage: GPUBufferUsage.COPY_DST | GPUBufferUsage.MAP_READ });
        const copy = device.createCommandEncoder();
        copy.copyTextureToBuffer({ texture: context.getCurrentTexture() },
          { buffer: readback, bytesPerRow }, [canvas.width, canvas.height]);
        device.queue.submit([copy.finish()]);
        await readback.mapAsync(GPUMapMode.READ);
        const pixels = new Uint8Array(readback.getMappedRange());
        const redIndex = format.startsWith("bgra") ? 2 : 0;
        const blueIndex = redIndex === 2 ? 0 : 2;
        const red = [];
        for (let y = 0; y < canvas.height; y++) for (let x = 0; x < canvas.width; x++) {
          const offset = y * bytesPerRow + x * 4;
          if (pixels[offset + redIndex] > 240 && pixels[offset + 1] < 10 && pixels[offset + blueIndex] < 10) red.push([x, y]);
        }
        readback.unmap();
        readback.destroy();
        check(red.length === 36, `Expected 3x3 logical sprite to cover 6x6 physical pixels, got ${red.length}`);
        check(Math.min(...red.map(point => point[0])) === 40 && Math.max(...red.map(point => point[0])) === 45, "Native sprite width or offset changed");
        check(Math.min(...red.map(point => point[1])) === 80 && Math.max(...red.map(point => point[1])) === 85, "Native sprite height or offset changed");
        const primary = renderer.metrics();

        renderer.resize(40, 20, { width: 200, height: 200 });
        renderer.render(cells, metadata, true);
        await renderer.idle();
        check(canvas.width === 200 && canvas.height === 200 && renderer.columns === 40 && renderer.rows === 20, "Thumbnail changed grid instead of backing resolution");
        const thumbnail = renderer.metrics();
        check(thumbnail.imageUploadBytes === primary.imageUploadBytes && thumbnail.glyphUploadBytes === primary.glyphUploadBytes, "Viewport change reuploaded graphics or glyphs");

        renderer.resize(1000, 200);
        const large = { columns: renderer.columns, rows: renderer.rows, width: canvas.width, height: canvas.height, limit: renderer.backend.maxCanvasDimension2D };
        check(large.columns === 1000 && large.rows === 200 && large.width <= large.limit && large.height <= large.limit, "Remote grid did not respect framebuffer bounds");
        renderer.resize(40, 20, { width: 0, height: 0 });
        renderer.render(cells, metadata, true);
        await renderer.idle();
        check(canvas.width === 1 && canvas.height === 1, "Hidden surface did not reduce its framebuffer");
        renderer.resize(40, 20, { width: 800, height: 800 });
        renderer.render(cells, metadata, true);
        await renderer.idle();
        check(errors.length === 0, `GPU errors: ${errors.join("; ")}`);
        return { passed: true, nativeSpritePhysicalPixels: red.length, primary, thumbnail, large, gpuErrors: errors };
      } finally {
        renderer.dispose();
      }
    });
  } finally {
    await context.close();
  }
}
