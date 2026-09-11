async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const transport = await page.evaluate(() => new URL(location.href).searchParams.get("transport") || "direct");
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1100 }, deviceScaleFactor: 2 });
  const test = await context.newPage();
  const errors = [];
  const results = [];
  const instances = [];
  let stage = "";
  test.on("pageerror", error => errors.push(error.message));
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  try {
    for (const scene of ["sixel", "kgp", "animation"]) {
      const created = test.waitForResponse(response =>
        response.url() === `${origin}/api/terminals` && response.request().method() === "POST" && response.status() === 201);
      await test.goto(`${origin}/?scene=${scene}&scale=auto&transport=${encodeURIComponent(transport)}`);
      const instance = await (await created).json();
      instances.push(instance.id);
      stage = `${scene}/primary`;
      await test.waitForFunction(() => webTerminalViews.get("1")?.terminal?.peer.isPrimary &&
        webTerminalViews.get("1").stats.frames >= 12 && webTerminalViews.get("1").stats.imageCount > 0, null, { timeout: 30000 });
      await test.selectOption("#renderer", "webgl2");
      await test.locator('.terminal-window[data-view="1"] .thumbnail').click();
      stage = `${scene}/thumbnail`;
      await test.waitForFunction(() => webTerminalViews.get("2")?.terminal?.connected &&
        webTerminalViews.get("2").stats.frames >= 8 && webTerminalViews.get("2").stats.imageCount > 0, null, { timeout: 30000 });
      const before = await test.evaluate(() => [...webTerminalViews.values()].map(view => view.terminal.stats));
      stage = `${scene}/advance`;
      await test.waitForFunction(frames => [...webTerminalViews.values()].every((view, index) => view.stats.frames >= frames[index] + 5),
        before.map(stats => stats.frames));
      const after = await test.evaluate(() => [...webTerminalViews.values()].map(view => view.terminal.stats));
      check(after.every(stats => stats.gpu === "ready" && stats.imageCount > 0 && stats.warnings.length === 0), `${scene}: graphics warnings or disconnection`);
      check(after[0].renderer === "webgpu" && after[1].renderer === "webgl2" &&
        after.every(stats => !stats.rendererFallbackReason), "Default WebGPU or forced WebGL2 selection was ignored");
      check(await test.locator("#metric-renderer").textContent() === "webgl2", "Selected-view renderer diagnostic is incorrect");
      if (scene === "kgp") check(after.every((stats, index) => stats.imageUploadBytes === before[index].imageUploadBytes),
        "KGP movement retransmitted cached image pixels");
      if (scene === "animation") check(after.every((stats, index) => stats.workloadBytes === before[index].workloadBytes),
        "Animation relied on fresh HMP workload traffic");
      results.push({ scene, views: after.map(stats => ({
        renderer: stats.renderer,
        frames: stats.frames, images: stats.imageCount, textureBytes: stats.textureBytes,
        imageUploadBytes: stats.imageUploadBytes, backingScale: stats.backingScale,
        backingWidth: stats.backingWidth, backingHeight: stats.backingHeight
      })) });
      await test.request.delete(`${origin}/api/terminals/${instance.id}`, { headers: { Origin: origin } });
      instances.pop();
    }
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, results, browserErrors: errors };
  } catch (error) {
    const state = await test.evaluate(() => [...(window.webTerminalViews?.values() || [])].map(view => ({
      status: view.element.querySelector(".view-status").textContent,
      peer: view.terminal?.peer, stats: view.stats
    })));
    throw new Error(`${stage}: ${error.message}; ${JSON.stringify(state)}`);
  } finally {
    try {
      for (const id of instances) await test.request.delete(`${origin}/api/terminals/${id}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
