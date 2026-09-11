async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({ viewport: { width: 1000, height: 900 }, deviceScaleFactor: 2 });
  const test = await context.newPage();
  const errors = [];
  const sockets = [];
  let instanceId;
  let releaseFont;
  test.on("pageerror", error => errors.push(error.message));
  test.on("websocket", socket => sockets.push(socket.url()));
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  try {
    await test.goto(`${origin}/health`);
    await test.setContent('<canvas id="pixels"></canvas><div id="a" style="width:300px;height:200px"></div><div id="b" style="width:300px;height:200px"></div><div id="c" style="width:300px;height:200px"></div>');
    const pixels = await test.evaluate(async () => {
      const { TerminalRenderer } = await import("/web-terminal/renderer.js");
      const canvas = document.getElementById("pixels");
      const results = [];
      for (const scale of [1, 1.25, 1.5, 2, 3]) {
        const failures = [];
        const renderer = await TerminalRenderer.create(canvas, scale, error => failures.push(error.message), undefined, "webgpu");
        const { context, device, format } = renderer.backend;
        try {
          renderer.resize(8, 8);
          const cells = Array.from({ length: 64 }, (_, index) => {
            const x = index % 8, y = Math.floor(index / 8);
            const text = y === 0 ? x === 0 ? "\u250c" : x === 7 ? "\u2510" : "\u2500"
              : y === 7 ? x === 0 ? "\u2514" : x === 7 ? "\u2518" : "\u2500"
              : x === 0 || x === 7 ? "\u2502" : " ";
            return { text, width: 1, foreground: 0xffffffff, background: 0xff000000,
              underlineColor: 0xffffffff, attributes: x >= 4 ? 1 : 0, underlineStyle: 0 };
          });
          renderer.prepareGlyphs(cells);
          context.configure({ device, format, alphaMode: "opaque",
            usage: GPUTextureUsage.RENDER_ATTACHMENT | GPUTextureUsage.COPY_SRC });
          renderer.render(cells, { defaultBackground: 0xff000000,
            cursor: { visible: false }, placements: [] }, true);
          const bytesPerRow = Math.ceil(canvas.width * 4 / 256) * 256;
          const buffer = device.createBuffer({ size: bytesPerRow * canvas.height,
            usage: GPUBufferUsage.COPY_DST | GPUBufferUsage.MAP_READ });
          const encoder = device.createCommandEncoder();
          encoder.copyTextureToBuffer({ texture: context.getCurrentTexture() },
            { buffer, bytesPerRow }, [canvas.width, canvas.height]);
          device.queue.submit([encoder.finish()]);
          await buffer.mapAsync(GPUMapMode.READ);
          const bytes = new Uint8Array(buffer.getMappedRange());
          const ink = (x, y) => bytes[y * bytesPerRow + x * 4] > 64;
          const cellWidth = 10 * scale, cellHeight = 20 * scale;
          for (let y = Math.ceil(cellHeight); y < Math.floor(7 * cellHeight); y++) {
            for (const column of [0, 7]) {
              let found = false;
              for (let x = Math.ceil(column * cellWidth); x < Math.floor((column + 1) * cellWidth); x++) found ||= ink(x, y);
              if (!found) throw new Error(`Vertical font border gap at scale ${scale}, column ${column}, pixel row ${y}`);
            }
          }
          for (let x = Math.ceil(cellWidth); x < Math.floor(7 * cellWidth); x++) {
            for (const row of [0, 7]) {
              let found = false;
              for (let y = row * cellHeight; y < (row + 1) * cellHeight; y++) found ||= ink(x, y);
              if (!found) throw new Error(`Horizontal font border gap at scale ${scale}, row ${row}, pixel column ${x}`);
            }
          }
          buffer.unmap();
          buffer.destroy();

          const glyphs = [];
          for (const text of ["\ue0b0", "\uf120", "\udb80\udc01", "\udbff\udfff"]) {
            renderer.prepareGlyphs([{ ...cells[0], text }]);
            const data = renderer.raster.getImageData(2, 2, Math.ceil(cellWidth), Math.ceil(cellHeight)).data;
            let hash = 2166136261, inkPixels = 0;
            for (let i = 3; i < data.length; i += 4) {
              hash = Math.imul(hash ^ data[i], 16777619);
              if (data[i]) inkPixels++;
            }
            glyphs.push({ text, hash, inkPixels });
          }
          if (glyphs.slice(0, 3).some(glyph => !glyph.inkPixels || glyph.hash === glyphs[3].hash) ||
              new Set(glyphs.map(glyph => glyph.hash)).size !== 4) {
            throw new Error(`Missing Nerd Font glyphs at scale ${scale}: ${JSON.stringify(glyphs)}`);
          }
          if (failures.length) throw new Error(failures.join("; "));
          results.push({ scale, family: renderer.font.family, connectedBorders: true, glyphs: glyphs.slice(0, 3) });
        } finally {
          renderer.dispose();
        }
      }
      return results;
    });

    const asset = await test.request.get(`${origin}/web-terminal/fonts/cascadia-mono-nf/CascadiaMonoNF.woff2`);
    check(asset.ok(), "Bundled font was not served");
    const fontBytes = await asset.body();
    let requestedFont;
    const requested = new Promise(resolve => { requestedFont = resolve; });
    const released = new Promise(resolve => { releaseFont = resolve; });
    await test.route("**/fonts/delayed.woff2", async route => {
      requestedFont();
      await released;
      await route.fulfill({ body: fontBytes, contentType: "font/woff2" });
    });
    const created = await test.request.post(`${origin}/api/terminals`, {
      headers: { Origin: origin }, data: { scene: "text" }
    });
    check(created.status() === 201, "Could not create isolated font fixture terminal");
    instanceId = (await created.json()).id;
    await test.evaluate(async instanceId => {
      const { WebTerminal } = await import("/web-terminal/index.js");
      window.WebTerminal = WebTerminal;
      window.wsUrl = `/ws?instance=${instanceId}`;
      window.pending = WebTerminal.mount(document.getElementById("a"), {
        url: wsUrl, workerUrl: "/web-terminal/terminal-worker.js",
        font: { family: "Developer Font", faces: [{ url: "/fonts/delayed.woff2", weight: "200 700" }] }
      }).then(view => { window.fontViewA = view; });
    }, instanceId);
    await requested;
    check(await test.evaluate(() => !window.fontViewA) && sockets.length === 0,
      "The worker connected or presented before its font finished loading");
    releaseFont();
    await test.waitForFunction(() => window.fontViewA?.stats.fontFamily === "Developer Font");
    const beforeMissing = sockets.length;
    const missing = await test.evaluate(async () => {
      try {
        await WebTerminal.mount(document.getElementById("c"), {
          url: wsUrl, font: { family: "Missing Font", faces: [{ url: "/fonts/does-not-exist.woff2" }] }
        });
        return "unexpected success";
      } catch (error) {
        return error.message;
      }
    });
    check(missing.includes('Could not load terminal font "Missing Font"') && sockets.length === beforeMissing,
      `Font failure was not surfaced before connection: ${missing}`);
    check(await test.evaluate(() => !document.querySelector("#c .hex1b-terminal")), "Failed font mount leaked its wrapper");
    await test.evaluate(async () => {
      window.fontViewB = await WebTerminal.mount(document.getElementById("b"), { url: wsUrl, font: { family: "monospace" } });
    });
    check(await test.evaluate(() => fontViewA.stats.fontFamily === "Developer Font" && fontViewB.stats.fontFamily === "monospace" &&
      ![...document.fonts].some(face => face.family === "Developer Font")),
    "Font selection was not isolated to each rendering worker");
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, pixels, workerFontReadiness: true, perViewFonts: true, missingFontError: missing, browserErrors: errors };
  } finally {
    releaseFont?.();
    try {
      if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
