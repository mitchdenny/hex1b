async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1100 } });
  const test = await context.newPage();
  const errors = [];
  let instance;
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
    await test.keyboard.type("dotnet ../WindowingDemo/bin/Release/net10.0/WindowingDemo.dll");
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => webTerminalViews.get("1").terminal.screenText.includes("File  Window  Samples") &&
      webTerminalViews.get("1").stats.mouseTracking === 1003);
    await clickText("File");
    await test.waitForFunction(() => webTerminalViews.get("1").terminal.screenText.includes("New Bash Terminal"));
    await clickText("New Bash Terminal");
    await test.waitForFunction(() => webTerminalViews.get("1").terminal.screenText.includes("Bash Terminal 1") &&
      /[$#%>]\s*\u2502/.test(webTerminalViews.get("1").terminal.screenText));
    await test.keyboard.type("dotnet ../KittySearch/bin/Release/net10.0/KittySearch.dll");
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => webTerminalViews.get("1").terminal.screenText.includes("bundled TGIF animations."));
    await test.keyboard.type("cat");
    await test.waitForFunction(() => webTerminalViews.get("1").terminal.screenText.includes("Showing 4 result(s)") &&
      webTerminalViews.get("1").stats.imageCount >= 3);
    const card = await test.evaluate(() => {
      const lines = webTerminalViews.get("1").terminal.screenText.split("\n");
      const border = "\u250c" + "\u2500".repeat(22) + "\u2510";
      const row = lines.findIndex(line => line.includes(border));
      if (row < 0) throw new Error("First image card was not rendered");
      return { row, column: lines[row].indexOf(border) };
    });
    const box = await canvas.boundingBox();
    await test.mouse.move(box.x + (card.column + 10.5) * box.width / 120,
      box.y + (card.row + 4.5) * box.height / 40);

    const result = await test.evaluate(async card => {
      const terminal = webTerminalViews.get("1").terminal;
      const canvas = terminal.element.shadowRoot.querySelector("canvas");
      const probe = document.createElement("canvas");
      probe.width = canvas.width;
      probe.height = canvas.height;
      const pixels = probe.getContext("2d", { willReadFrequently: true });
      const counts = [];
      const hashes = new Set();
      const framesBefore = terminal.stats.frames;
      const started = performance.now();
      for (let frame = 0; performance.now() - started < 20000 &&
          (frame < 180 || hashes.size < 5 || terminal.stats.frames - framesBefore < 10); frame++) {
        await new Promise(requestAnimationFrame);
        pixels.drawImage(canvas, 0, 0);
        // Exclude the card border and the animated parent background.
        const data = pixels.getImageData(
          Math.ceil(canvas.width * (card.column + 1.5) / 120),
          Math.ceil(canvas.height * (card.row + 1.5) / 40),
          Math.floor(canvas.width * 20 / 120), Math.floor(canvas.height * 5 / 40)).data;
        let painted = 0;
        let hash = 2166136261;
        for (let offset = 0; offset < data.length; offset += 4) {
          if (Math.max(data[offset], data[offset + 1], data[offset + 2]) > 80) painted++;
          hash = Math.imul(hash ^ data[offset], 16777619);
        }
        counts.push(painted);
        hashes.add(hash);
      }
      return {
        samples: counts.length,
        blankFrames: counts.filter(count => count === 0).length,
        minimumPaintedPixels: Math.min(...counts),
        maximumPaintedPixels: Math.max(...counts),
        uniqueImages: hashes.size,
        receivedFrames: terminal.stats.frames - framesBefore,
        gpu: terminal.stats.gpu,
        warnings: terminal.stats.warnings
      };
    }, card);
    if (result.samples < 180 || result.blankFrames ||
        result.minimumPaintedPixels < result.maximumPaintedPixels * .4 ||
        result.uniqueImages < 5 || result.receivedFrames < 10 ||
        result.gpu !== "ready" || result.warnings.length || errors.length) {
      throw new Error(JSON.stringify({ ...result, errors }));
    }
    return { passed: true, sample: "WindowingDemo -> Bash -> KittySearch", ...result };
  } finally {
    try {
      if (instance) await test.request.delete(`${origin}/api/terminals/${instance}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
