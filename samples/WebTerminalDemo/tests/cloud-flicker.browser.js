async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const transport = await page.evaluate(() => new URL(location.href).searchParams.get("transport") || "direct");
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1100 } });
  const test = await context.newPage();
  const instances = [];
  const results = [];
  const errors = [];
  test.on("pageerror", error => errors.push(error.message));
  try {
    for (const sample of ["SixelCloudDemo", "KgpCloudDemo"]) {
      const created = test.waitForResponse(response =>
        response.url() === `${origin}/api/terminals` && response.request().method() === "POST" && response.status() === 201);
      await test.goto(`${origin}/?scene=shell&scale=auto&transport=${encodeURIComponent(transport)}`);
      instances.push((await (await created).json()).id);
      await test.waitForFunction(() => webTerminalViews.get("1")?.terminal?.peer.isPrimary &&
        /[❯$#%>]$/.test(webTerminalViews.get("1").terminal.screenText.trimEnd()));
      await test.evaluate(() => webTerminalViews.get("1").terminal.setSizing({ mode: "fixed", columns: 80, rows: 24 }));
      await test.waitForFunction(() => webTerminalViews.get("1").stats.columns === 80 &&
        webTerminalViews.get("1").stats.rows === 24);
      await test.locator('.terminal-window[data-view="1"] canvas').click({ position: { x: 100, y: 100 } });
      await test.keyboard.type(`dotnet ../${sample}/bin/Release/net10.0/${sample}.dll --motes 700 --frame-ms 33`);
      await test.keyboard.press("Enter");
      await test.waitForFunction(() => webTerminalViews.get("1").stats.mouseTracking === 1003 &&
        webTerminalViews.get("1").stats.imageCount > 10, null, { timeout: 30000 });
      const result = await test.evaluate(async () => {
        const terminal = webTerminalViews.get("1").terminal;
        const canvas = terminal.element.shadowRoot.querySelector("canvas");
        const probe = document.createElement("canvas");
        probe.width = canvas.width;
        probe.height = canvas.height;
        const pixels = probe.getContext("2d", { willReadFrequently: true });
        const counts = [];
        const framesBefore = terminal.stats.frames;
        const started = performance.now();
        for (let frame = 0; performance.now() - started < 20000 &&
            (frame < 180 || terminal.stats.frames - framesBefore < 10); frame++) {
          await new Promise(requestAnimationFrame);
          pixels.drawImage(canvas, 0, 0);
          const data = pixels.getImageData(0, 0, probe.width, probe.height).data;
          let painted = 0;
          for (let offset = 0; offset < data.length; offset += 4) {
            if (Math.max(data[offset], data[offset + 1], data[offset + 2]) > 110) painted++;
          }
          counts.push(painted);
        }
        return {
          samples: counts.length,
          minimumPaintedPixels: Math.min(...counts),
          maximumPaintedPixels: Math.max(...counts),
          blankFrames: counts.filter(count => count === 0).length,
          receivedFrames: terminal.stats.frames - framesBefore,
          gpu: terminal.stats.gpu,
          warnings: terminal.stats.warnings
        };
      });
      if (result.blankFrames || result.minimumPaintedPixels < result.maximumPaintedPixels * .4 ||
          result.receivedFrames < 10 || result.gpu !== "ready" || result.warnings.length) {
        throw new Error(`${sample}: ${JSON.stringify(result)}`);
      }
      results.push({ sample, ...result });
      if (sample === "KgpCloudDemo") {
        for (let connection = 2; connection <= 21; connection++) {
          const id = String(connection);
          const thumbnail = connection > 3;
          await test.locator(thumbnail ? '[data-view="1"] .thumbnail' : "#attach").click();
          await test.waitForFunction(id => webTerminalViews.get(id)?.terminal?.connected &&
            webTerminalViews.get(id).stats.imageCount === 12, id, { timeout: 30000 });
          const before = await test.evaluate(id => webTerminalViews.get(id).terminal.stats, id);
          await test.waitForFunction(({ id, frames }) => webTerminalViews.get(id).stats.frames >= frames + 10,
            { id, frames: before.frames }, { timeout: 30000 });
          const after = await test.evaluate(id => webTerminalViews.get(id).terminal.stats, id);
          const text = await test.evaluate(id => webTerminalViews.get(id).terminal.screenText.trim(), id);
          if (after.imageCount !== 12 || after.imageUploadBytes !== before.imageUploadBytes ||
              after.discardedFrames !== 0 || after.warnings.length || text.length) {
            throw new Error(`KgpCloudDemo ${transport} late/reconnected view ${id}: ${JSON.stringify({ ...after, text })}`);
          }
          results.push({ sample, transport, view: id, thumbnail, images: after.imageCount,
            receivedFrames: after.frames - before.frames, text });
          await test.locator(`[data-view="${id}"] .close-view`).click();
          await test.waitForFunction(() => webTerminalViews.size === 1);
        }
      }
      await test.request.delete(`${origin}/api/terminals/${instances.at(-1)}`, { headers: { Origin: origin } });
      instances.pop();
    }
    if (errors.length) throw new Error(errors.join("; "));
    return { passed: true, results };
  } finally {
    try {
      for (const id of instances) await test.request.delete(`${origin}/api/terminals/${id}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
