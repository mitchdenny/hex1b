async page => {
  // Serve only reviewed HWT1 artifacts at /evidence/ and the matching built client at /web-terminal/.
  // Example query: ?frame=rgba-control.hwt&backend=webgpu
  const { frames, backend, minimum } = await page.evaluate(() => {
    const query = new URL(location.href).searchParams;
    return { frames: query.getAll("frame"), backend: query.get("backend") || "webgpu",
      minimum: query.has("minimum") ? Number(query.get("minimum")) : null };
  });
  if (!frames.length) throw new Error("At least one frame query parameter is required");
  await page.setContent('<canvas id="capture"></canvas>');
  return await page.evaluate(async ({ frames, backend, minimum }) => {
    const { decodeFrame } = await import("/web-terminal/protocol.js");
    const { TerminalRenderer } = await import("/web-terminal/renderer.js");
    const canvas = document.querySelector("#capture");
    const errors = [];
    const renderer = await TerminalRenderer.create(canvas, 1, error => errors.push(error.message), undefined, backend);
    const cells = [];
    let metadata;
    try {
      for (const name of frames) {
        if (name.startsWith("/") || name.split("/").some(part => part === ".."))
          throw new Error("Use a relative reviewed evidence path");
        const response = await fetch(`/evidence/${name}`);
        if (!response.ok) throw new Error(`Frame fetch failed: ${response.status}`);
        const frame = decodeFrame(await response.arrayBuffer());
        metadata = frame.metadata;
        if (metadata.full) cells.length = metadata.columns * metadata.rows;
        for (const cell of frame.cells) cells[cell.index] = cell;
        renderer.resize(metadata.columns, metadata.rows);
        await renderer.updateImages(frame.images, metadata.retainedImages);
        renderer.prepareGlyphs(cells);
      }
      let pixels, stride, redIndex, flipY = false;
      const { device, context, format, gl } = renderer.backend;
      let readback;
      if (backend === "webgpu") {
        context.configure({ device, format, alphaMode: "opaque",
          usage: GPUTextureUsage.RENDER_ATTACHMENT | GPUTextureUsage.COPY_SRC });
        renderer.render(cells, metadata, true);
        stride = Math.ceil(canvas.width * 4 / 256) * 256;
        readback = device.createBuffer({ size: stride * canvas.height,
          usage: GPUBufferUsage.COPY_DST | GPUBufferUsage.MAP_READ });
        const copy = device.createCommandEncoder();
        copy.copyTextureToBuffer({ texture: context.getCurrentTexture() },
          { buffer: readback, bytesPerRow: stride }, [canvas.width, canvas.height]);
        device.queue.submit([copy.finish()]);
        await readback.mapAsync(GPUMapMode.READ);
        pixels = new Uint8Array(readback.getMappedRange()).slice();
        readback.unmap();
        readback.destroy();
        redIndex = format.startsWith("bgra") ? 2 : 0;
      } else {
        renderer.render(cells, metadata, true);
        stride = canvas.width * 4;
        pixels = new Uint8Array(stride * canvas.height);
        gl.readPixels(0, 0, canvas.width, canvas.height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
        redIndex = 0;
        flipY = true;
      }
      let redPixels = 0, coloredViewportPixels = 0;
      for (let y = 0; y < canvas.height; y++) for (let x = 0; x < canvas.width; x++) {
        const offset = (flipY ? canvas.height - 1 - y : y) * stride + x * 4;
        const r = pixels[offset + redIndex], g = pixels[offset + 1], b = pixels[offset + (redIndex === 2 ? 0 : 2)];
        if (r > 240 && g < 10 && b < 10) redPixels++;
        if (x >= 50 && x < canvas.width - 50 && y >= 60 && y < canvas.height - 60 &&
            Math.max(r, g, b) > 70 && Math.max(r, g, b) - Math.min(r, g, b) > 40)
          coloredViewportPixels++;
      }
      if (errors.length) throw new Error(errors.join("; "));
      if (minimum !== null && redPixels < minimum)
        throw new Error(`Expected at least ${minimum} red pixels, found ${redPixels}`);
      // Keep a 2D copy for screenshots after GPU resource cleanup.
      const rgba = new Uint8ClampedArray(canvas.width * canvas.height * 4);
      for (let y = 0; y < canvas.height; y++) for (let x = 0; x < canvas.width; x++) {
        const source = (flipY ? canvas.height - 1 - y : y) * stride + x * 4;
        const destination = (y * canvas.width + x) * 4;
        rgba[destination] = pixels[source + redIndex];
        rgba[destination + 1] = pixels[source + 1];
        rgba[destination + 2] = pixels[source + (redIndex === 2 ? 0 : 2)];
        rgba[destination + 3] = 255;
      }
      const image = document.createElement("canvas");
      image.width = canvas.width;
      image.height = canvas.height;
      image.getContext("2d").putImageData(new ImageData(rgba, image.width, image.height), 0, 0);
      canvas.replaceWith(image);
      return { backend, frames, redPixels, coloredViewportPixels,
        images: metadata.retainedImages.length, placements: metadata.placements.length,
        width: image.width, height: image.height, warnings: metadata.warnings, errors };
    } finally {
      renderer.dispose();
    }
  }, { frames, backend, minimum });
}
