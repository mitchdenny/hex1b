async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const transport = await page.evaluate(() => new URL(location.href).searchParams.get("transport") || "direct");
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1100 }, deviceScaleFactor: 2 });
  const test = await context.newPage();
  const errors = [];
  const instanceIds = [];
  const sent = [];
  test.on("pageerror", error => errors.push(error.message));
  test.on("websocket", socket => socket.on("framesent", frame => {
    if (typeof frame.payload === "string") {
      const value = JSON.parse(frame.payload);
      if (value.type !== "ack") sent.push({ url: socket.url(), ...value });
    }
  }));
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const waitView = async count => {
    await test.waitForFunction(count => webTerminalViews.size === count &&
      [...webTerminalViews.values()].every(view => view.terminal?.connected && view.stats.revision > 0), count, { timeout: 30000 });
  };
  const authoritative = async id => {
    const response = await test.request.get(`${origin}/api/terminals`);
    return (await response.json()).find(instance => instance.id === id);
  };
  try {
    await test.goto(`${origin}/?empty=1&scale=auto&transport=${encodeURIComponent(transport)}`);
    await test.locator("#scene").selectOption("mixed");
    const created = test.waitForResponse(response => response.url() === `${origin}/api/terminals` &&
      response.request().method() === "POST" && response.status() === 201);
    await test.locator("#create").click();
    const instanceId = (await (await created).json()).id;
    instanceIds.push(instanceId);
    await waitView(1);
    await test.waitForFunction(() => [...webTerminalViews.values()][0].terminal.peer.isPrimary);
    const first = test.locator('.terminal-window[data-view="1"]');
    await first.locator(".thumbnail").click();
    await waitView(2);
    const second = test.locator('.terminal-window[data-view="2"]');
    await test.waitForFunction(() => {
      const [a, b] = [...webTerminalViews.values()];
      return !b.terminal.peer.isPrimary && b.terminal.geometry.columns === a.terminal.geometry.columns &&
        b.terminal.geometry.rows === a.terminal.geometry.rows;
    });
    const readFit = () => test.evaluate(() => [...webTerminalViews.values()].map(view => {
      const { columns, rows, cellWidth, cellHeight } = view.terminal.geometry;
      const surface = view.terminal.element.shadowRoot.querySelector(".surface").getBoundingClientRect();
      const outer = view.element.querySelector(".terminal-mount").getBoundingClientRect();
      const windowBox = view.element.getBoundingClientRect();
      const close = view.element.querySelector(".close-view").getBoundingClientRect();
      const sizingFits = [...view.element.querySelectorAll(".font-smaller, .font-size, .font-larger, .view-resolution")]
        .every(control => {
          const bounds = control.getBoundingClientRect();
          return bounds.left >= windowBox.left && bounds.right <= windowBox.right - 20 &&
            bounds.top >= windowBox.top && bounds.bottom <= windowBox.bottom;
        });
      return { gridRatio: columns * cellWidth / (rows * cellHeight), ratio: surface.width / surface.height,
        fits: surface.width <= outer.width + .1 && surface.height <= outer.height + .1,
        centered: Math.abs((surface.left - outer.left) - (outer.right - surface.right)) < .1 &&
          Math.abs((surface.top - outer.top) - (outer.bottom - surface.bottom)) < .1,
        chromeFits: sizingFits && close.right <= windowBox.right && outer.right <= windowBox.right && outer.left >= windowBox.left,
        backingScale: view.stats.backingScale, rasterScale: view.stats.rasterScale, images: view.stats.imageCount };
    }));
    const fit = await readFit();
    check(fit.every(item => item.fits && item.centered && item.chromeFits && Math.abs(item.gridRatio - item.ratio) < .01 && item.rasterScale === 2), "DPR2 aspect fit or window chrome containment failed");

    // Pause output so the remaining resize/role tests cannot depend on new bytes.
    await test.request.post(`${origin}/api/terminals/${instanceId}/controls`, {
      headers: { Origin: origin }, data: { paused: true }
    });
    const original = await authoritative(instanceId);
    const secondaryHandle = await second.locator(".resize-handle").boundingBox();
    const resizeBefore = sent.filter(command => command.type === "resize").length;
    await test.mouse.move(secondaryHandle.x + 5, secondaryHandle.y + 5);
    await test.mouse.down();
    await test.mouse.move(secondaryHandle.x + 115, secondaryHandle.y + 55, { steps: 8 });
    await test.mouse.up();
    await test.waitForTimeout(250);
    const afterSecondary = await authoritative(instanceId);
    check(original.columns === afterSecondary.columns && original.rows === afterSecondary.rows, "Secondary resizing changed producer grid");
    check(sent.filter(command => command.type === "resize").length === resizeBefore, "Secondary emitted resize");

    // Keep the thumbnail and its resize handle inside the viewport.
    const title = await second.locator(".view-title").boundingBox();
    const beforeMove = await second.boundingBox();
    await test.mouse.move(title.x + 15, title.y + 8);
    await test.mouse.down();
    await test.mouse.move(title.x - 250, title.y + 70, { steps: 8 });
    await test.mouse.up();
    const afterMove = await second.boundingBox();
    check(afterMove.x < beforeMove.x - 200 && afterMove.y > beforeMove.y + 50, "Title bar did not drag");

    const restoredSize = await second.boundingBox();
    const thumbnailSizes = [];
    for (const [width, height] of [[600, 180], [240, 180], [240, 500], [restoredSize.width, restoredSize.height]]) {
      const box = await second.boundingBox();
      const handle = await second.locator(".resize-handle").boundingBox();
      await test.mouse.move(handle.x + 5, handle.y + 5);
      await test.mouse.down();
      await test.mouse.move(handle.x + 5 + width - box.width, handle.y + 5 + height - box.height, { steps: 8 });
      await test.mouse.up();
      await test.waitForFunction(({ width, height }) => {
        const view = webTerminalViews.get("2");
        const box = view.element.getBoundingClientRect();
        const mount = view.element.querySelector(".terminal-mount").getBoundingClientRect();
        const surface = view.terminal.element.shadowRoot.querySelector(".surface").getBoundingClientRect();
        const { columns, rows, cellWidth, cellHeight } = view.terminal.geometry;
        const scale = Math.min(mount.width / (columns * cellWidth), mount.height / (rows * cellHeight));
        return Math.abs(box.width - width) < .1 && Math.abs(box.height - height) < .1 &&
          Math.abs(surface.width - columns * cellWidth * scale) < .1 &&
          Math.abs(surface.height - rows * cellHeight * scale) < .1;
      }, { width, height });
      const resized = (await readFit())[1];
      check(resized.fits && resized.centered && resized.chromeFits &&
        Math.abs(resized.gridRatio - resized.ratio) < .01,
      `Thumbnail overflow at ${width}x${height}: ${JSON.stringify(resized)}`);
      thumbnailSizes.push({ width, height, ...resized });
    }
    check(sent.filter(command => command.type === "resize").length === resizeBefore, "Shrinking a thumbnail emitted resize");

    await first.locator(".view-title").click();
    const primaryHandle = await first.locator(".resize-handle").boundingBox();
    await test.mouse.move(primaryHandle.x + 5, primaryHandle.y + 5);
    await test.mouse.down();
    await test.mouse.move(primaryHandle.x - 195, primaryHandle.y - 95, { steps: 10 });
    await test.mouse.up();
    await test.waitForFunction(() => {
      const [a, b] = [...webTerminalViews.values()];
      const box = a.element.querySelector(".terminal-mount").getBoundingClientRect();
      const columns = Math.max(20, Math.min(300, Math.floor(box.width / 10)));
      const rows = Math.max(10, Math.min(100, Math.floor(box.height / 20)));
      return a.terminal.geometry.columns === columns && a.terminal.geometry.rows === rows &&
        b.terminal.geometry.columns === columns && b.terminal.geometry.rows === rows &&
        a.stats.columns === columns && a.stats.rows === rows && b.stats.columns === columns && b.stats.rows === rows;
    });
    const primaryResize = await authoritative(instanceId);
    check(primaryResize.columns !== original.columns || primaryResize.rows !== original.rows, "Primary resize was not authoritative");
    const noEcho = sent.filter(command => command.type === "resize").length;
    await test.waitForTimeout(300);
    check(sent.filter(command => command.type === "resize").length === noEcho, `Geometry update caused resize loop: ${JSON.stringify(sent.filter(command => command.type === "resize"))}`);

    // Explicit takeover changes the grid to the new primary's host box.
    await second.locator(".view-title").click();
    await second.locator(".take-primary").click();
    await test.waitForFunction(() => {
      const [a, b] = [...webTerminalViews.values()];
      return !a.terminal.peer.isPrimary && b.terminal.peer.isPrimary &&
        a.terminal.geometry.columns === b.terminal.geometry.columns &&
        a.terminal.geometry.rows === b.terminal.geometry.rows;
    });
    const promoted = await authoritative(instanceId);
    check(promoted.primaryPeerId === await test.evaluate(() => webTerminalViews.get("2").terminal.peer.id), "HMP authority disagreed with view role");
    await second.locator(".close-view").click();
    await waitView(1);
    await test.waitForFunction(() => webTerminalViews.get("1").terminal.peer.primaryId === null);
    const afterClose = await authoritative(instanceId);
    check(afterClose.columns === promoted.columns && afterClose.rows === promoted.rows && afterClose.primaryPeerId === null, "Primary closure changed grid or auto-promoted");
    await first.locator(".close-view").click();
    await test.waitForFunction(() => webTerminalViews.size === 0);
    check(!!await authoritative(instanceId), "Closing final view terminated producer");
    await test.locator("#attach").click();
    await waitView(1);
    check(await test.evaluate(() => [...webTerminalViews.values()].every(view => !view.terminal.peer.isPrimary && view.terminal.peer.primaryId === null)), "Reattachment auto-claimed primary");
    const reattached = await test.evaluate(() => [...webTerminalViews.values()][0].terminal.geometry);
    check(reattached.columns === promoted.columns && reattached.rows === promoted.rows, "Reattachment lost authoritative grid");

    // Independent second producer and shared workload controls.
    await test.locator("#scene").selectOption("text");
    const createdSecond = test.waitForResponse(response => response.url() === `${origin}/api/terminals` &&
      response.request().method() === "POST" && response.status() === 201);
    await test.locator("#create").click();
    const secondId = (await (await createdSecond).json()).id;
    instanceIds.push(secondId);
    await waitView(2);
    await test.waitForFunction(() => [...webTerminalViews.values()].some(view => view.instance.scene === "text" && view.terminal.peer.isPrimary));
    check(secondId !== instanceId && (await authoritative(instanceId)).primaryPeerId === null, "Independent instances shared authority");
    const finalStats = await test.evaluate(() => [...webTerminalViews.values()].map(view => ({
      instance: view.instance.scene, peer: view.terminal.peer, geometry: view.terminal.geometry,
      revision: view.stats.revision, frames: view.stats.frames, images: view.stats.imageCount, gpu: view.stats.gpu
    })));
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, initialFit: fit, thumbnailSizes, original, primaryResize, promoted, reattached, finalStats, browserErrors: errors, resizeCommands: sent.filter(command => command.type === "resize").length };
  } finally {
    try {
      for (const id of instanceIds) await test.request.delete(`${origin}/api/terminals/${id}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
