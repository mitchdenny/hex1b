async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0];
  if (!origin) throw new Error("Open the demo before running the fixture");
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1000 } });
  const test = await context.newPage();
  const errors = [], reports = [];
  let instanceId, stage = "mount";
  test.on("pageerror", error => errors.push(error.message));
  try {
    for (const renderer of ["webgpu", "webgl2"]) {
      stage = `${renderer}: mount`;
      await test.goto(`${origin}/?scene=shell&renderer=${renderer}`);
      await test.waitForFunction(() => [...webTerminalViews.values()][0]?.instance.id);
      instanceId = await test.evaluate(() => [...webTerminalViews.values()][0].instance.id);
      await test.waitForFunction(() => [...webTerminalViews.values()][0]?.phase === "connected" &&
        /[\u276f$#%>]$/.test([...webTerminalViews.values()][0].terminal.screenText.trimEnd()));
      await test.evaluate(() => {
        window.besideTerminal = [...webTerminalViews.values()][0].terminal;
        window.besideAlpha = () => {
          const t = besideTerminal, track = t.layout.scrollbar, v = t.viewport;
          const canvas = t.element.shadowRoot.querySelector(".scrollbar-canvas");
          if (!track || !v.available || !canvas) return -1;
          const thumbHeight = Math.min(track.height, Math.max(24,
            track.height * Math.max(1, v.totalRows - v.liveTop) / v.totalRows));
          const top = track.top + (track.height - thumbHeight) * v.top / v.liveTop;
          const scale = canvas.width / t.layout.width;
          return canvas.getContext("2d").getImageData(
            Math.floor((track.left + track.width / 2) * scale),
            Math.floor((top + thumbHeight / 2) * scale), 1, 1).data[3];
        };
      });
      const view = test.locator(".terminal-window").first();
      await view.locator(".view-resolution").selectOption("80x24");
      await test.waitForFunction(() => besideTerminal.geometry.columns === 80 && besideTerminal.geometry.rows === 24);
      stage = `${renderer}: retained history`;
      await test.evaluate(() => besideTerminal.focus());
      await test.keyboard.type("i=1; while [ \"$i\" -le 120 ]; do printf 'BESIDE-%03d\\n' \"$i\"; i=$((i+1)); done; printf '__BESIDE_READY__\\n'");
      await test.keyboard.press("Enter");
      await test.waitForFunction(() => besideTerminal.viewport.liveTop > 100 &&
        besideTerminal.screenText.split("\n").some(line => line.trim() === "__BESIDE_READY__"));
      await test.mouse.move(1400, 980);
      for (const painter of ["default", "styled", "custom", "drawn"]) {
        stage = `${renderer}/${painter}: beside`;
        await view.locator(".view-scrollbar").selectOption("beside");
        await view.locator(".view-scrollbar-fade").selectOption(painter);
        await test.waitForFunction(() => besideTerminal.scrollbar.placement === "beside" && besideAlpha() > 0);
        const started = await test.evaluate(() => performance.now());
        // Exceed both the built-in 1.2s and custom 1.9s fade thresholds.
        await test.waitForFunction(start => performance.now() - start >= 2200, started);
        const alpha = await test.evaluate(() => besideAlpha());
        if (alpha <= 0) throw new Error(`${renderer}/${painter}: beside scrollbar faded`);
        reports.push({ renderer, painter, besideAlpha: alpha });

        stage = `${renderer}/${painter}: overlay`;
        await view.locator(".view-scrollbar").selectOption("overlay");
        await test.waitForFunction(() => besideTerminal.scrollbar.placement === "overlay" && besideAlpha() > 0);
        await test.waitForFunction(() => besideAlpha() === 0);
      }
      await test.evaluate(async id => {
        const response = await fetch(`/api/terminals/${id}`, { method: "DELETE" });
        if (!response.ok) throw new Error(`Could not remove fixture: ${response.status}`);
      }, instanceId);
      instanceId = undefined;
    }
    if (errors.length) throw new Error(errors.join("\n"));
    return { reports, errors };
  } catch (error) {
    throw new Error(`${stage}: ${error.message}`, { cause: error });
  } finally {
    if (instanceId) await test.evaluate(id => fetch(`/api/terminals/${id}`, { method: "DELETE" }), instanceId);
    await context.close();
  }
}
