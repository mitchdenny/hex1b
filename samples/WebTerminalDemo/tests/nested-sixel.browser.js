async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1100 } });
  const test = await context.newPage();
  const errors = [];
  const results = [];
  let instance;
  let stage = "outer shell";
  test.on("pageerror", error => errors.push(error.message));
  const canvas = test.locator('.terminal-window[data-view="1"] canvas');
  const clickText = async text => {
    const position = await test.evaluate(text => {
      const lines = webTerminalViews.get("1").terminal.screenText.split("\n");
      const row = lines.findIndex(line => line.includes(text));
      if (row < 0) throw new Error(`Missing terminal control: ${text}`);
      return { row, column: lines[row].indexOf(text) };
    }, text);
    const box = await canvas.boundingBox();
    await test.mouse.click(box.x + (position.column + 1.5) * box.width / 120,
      box.y + (position.row + .5) * box.height / 40);
  };
  try {
    for (const mode of ["placements", "raster"]) {
      stage = `${mode} outer shell`;
      const created = test.waitForResponse(response =>
        response.url() === `${origin}/api/terminals` && response.request().method() === "POST" && response.status() === 201);
      await test.goto(`${origin}/?scene=shell&scale=auto`);
      instance = (await (await created).json()).id;
      await test.waitForFunction(() => webTerminalViews.get("1")?.terminal?.peer.isPrimary &&
        /[\u276f$#%>]$/.test(webTerminalViews.get("1").terminal.screenText.trimEnd()));
      await test.evaluate(() => webTerminalViews.get("1").terminal.setSizing({ mode: "fixed", columns: 120, rows: 40 }));
      await test.locator('.terminal-window[data-view="1"]').evaluate(element => element.style.height = "760px");
      await test.waitForFunction(() => webTerminalViews.get("1").stats.columns === 120 &&
        webTerminalViews.get("1").stats.rows === 40 && webTerminalViews.get("1").stats.backingHeight > 500);
      await canvas.click({ position: { x: 100, y: 100 } });
      stage = "WindowingDemo";
      await test.keyboard.type("dotnet ../WindowingDemo/bin/Release/net10.0/WindowingDemo.dll");
      await test.keyboard.press("Enter");
      await test.waitForFunction(() => webTerminalViews.get("1").terminal.screenText.includes("File  Window  Samples") &&
        webTerminalViews.get("1").stats.mouseTracking === 1003);
      await clickText("File");
      await test.waitForFunction(() => webTerminalViews.get("1").terminal.screenText.includes("New Bash Terminal"));
      await clickText("New Bash Terminal");
      stage = "Bash prompt";
      await test.waitForFunction(() => webTerminalViews.get("1").terminal.screenText.includes("Bash Terminal 1") &&
        /[$#%>]\s*\u2502/.test(webTerminalViews.get("1").terminal.screenText));
      const content = await test.evaluate(() => {
        const lines = webTerminalViews.get("1").terminal.screenText.split("\n");
        const titleRow = lines.findIndex(line => line.includes("Bash Terminal 1"));
        return { row: titleRow + 1, column: lines[titleRow].indexOf("Bash Terminal 1") };
      });
      stage = `${mode} startup`;
      await test.keyboard.type("dotnet ../SixelCloudDemo/bin/Release/net10.0/SixelCloudDemo.dll --motes 700 --frame-ms 33" +
        (mode === "raster" ? " --raster" : ""));
      await test.keyboard.press("Enter");
      await test.waitForFunction(() => {
        const view = webTerminalViews.get("1");
        return view.stats.imageCount > 0 && !view.terminal.screenText.includes("SixelCloudDemo") &&
          !/[$#%>]\s*\u2502/.test(view.terminal.screenText);
      }, null, { timeout: 30000 });
      stage = `${mode} pixels`;
      const result = await test.evaluate(async content => {
        const terminal = webTerminalViews.get("1").terminal;
        const canvas = terminal.element.shadowRoot.querySelector("canvas");
        const probe = document.createElement("canvas");
        probe.width = canvas.width;
        probe.height = canvas.height;
        const pixels = probe.getContext("2d", { willReadFrequently: true });
        const counts = [];
        const framesBefore = terminal.stats.frames;
        const hashes = new Set();
        const started = performance.now();
        for (let frame = 0; performance.now() - started < 20000 &&
            (frame < 180 || hashes.size < 5 || terminal.stats.frames - framesBefore < 10); frame++) {
          await new Promise(requestAnimationFrame);
          pixels.drawImage(canvas, 0, 0);
          const data = pixels.getImageData(
            Math.ceil(canvas.width * (content.column + .5) / 120),
            Math.ceil(canvas.height * (content.row + .5) / 40),
            Math.floor(canvas.width * 79 / 120), Math.floor(canvas.height * 22 / 40)).data;
          let painted = 0;
          let hash = 2166136261;
          for (let offset = 0; offset < data.length; offset += 4) {
            if (Math.max(data[offset], data[offset + 1], data[offset + 2]) > 110) painted++;
            hash = Math.imul(hash ^ data[offset], 16777619);
          }
          counts.push(painted);
          hashes.add(hash);
        }
        return {
          samples: counts.length,
          blankFrames: counts.filter(count => count === 0).length,
          // The cloud naturally contracts. Detect abrupt partial redraws, not
          // the gradual change between its largest and smallest visible area.
          abruptDrops: counts.slice(1).filter((count, index) => count < counts[index] * .4).length,
          minimumPaintedPixels: Math.min(...counts),
          maximumPaintedPixels: Math.max(...counts),
          uniqueImages: hashes.size,
          receivedFrames: terminal.stats.frames - framesBefore,
          gpu: terminal.stats.gpu,
          warnings: terminal.stats.warnings
        };
      }, content);
      if (result.samples < 180 || result.blankFrames || result.uniqueImages < 5 ||
          result.abruptDrops ||
          result.receivedFrames < 10 || result.gpu !== "ready" || result.warnings.length || errors.length) {
        throw new Error(JSON.stringify({ mode, ...result, errors }));
      }
      results.push({ mode, ...result });
      await test.request.delete(`${origin}/api/terminals/${instance}`, { headers: { Origin: origin } });
      instance = undefined;
    }
    return { passed: true, sample: "WindowingDemo -> Bash -> SixelCloudDemo", results };
  } catch (error) {
    const state = await test.evaluate(() => ({
      text: webTerminalViews.get("1")?.terminal?.screenText,
      stats: webTerminalViews.get("1")?.stats
    }));
    throw new Error(`${stage}: ${error.message}; ${JSON.stringify(state)}`);
  } finally {
    try {
      if (instance) await test.request.delete(`${origin}/api/terminals/${instance}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
