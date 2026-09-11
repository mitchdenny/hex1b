async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1100 }, deviceScaleFactor: 2 });
  const test = await context.newPage();
  const errors = [];
  const commands = [];
  let instanceId;
  let stage = "creating views";
  test.on("pageerror", error => errors.push(error.message));
  test.on("websocket", socket => socket.on("framesent", frame => {
    if (typeof frame.payload === "string") {
      const command = JSON.parse(frame.payload);
      if (command.type !== "ack") commands.push(command);
    }
  }));
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const waitGrid = async (columns, rows) => test.waitForFunction(({ columns, rows }) =>
    [...webTerminalViews.values()].every(view => view.terminal.geometry.columns === columns &&
      view.terminal.geometry.rows === rows && view.stats.columns === columns && view.stats.rows === rows),
  { columns, rows });
  const waitAuto = async () => test.waitForFunction(() => {
    const primary = [...webTerminalViews.values()].find(view => view.terminal.peer.isPrimary);
    if (!primary || primary.terminal.sizing.mode !== "auto") return false;
    const box = primary.element.querySelector(".terminal-mount").getBoundingClientRect();
    const scale = primary.terminal.sizing.fontSize / 16;
    const columns = Math.max(20, Math.min(300, Math.floor(box.width / (10 * scale))));
    const rows = Math.max(10, Math.min(100, Math.floor(box.height / (20 * scale))));
    return [...webTerminalViews.values()].every(view => view.terminal.geometry.columns === columns &&
      view.terminal.geometry.rows === rows && view.stats.columns === columns && view.stats.rows === rows);
  });
  const resizeCount = () => commands.filter(command => command.type === "resize").length;
  try {
    await test.goto(`${origin}/?empty=1`);
    const created = test.waitForResponse(response => response.url() === `${origin}/api/terminals` &&
      response.request().method() === "POST" && response.status() === 201);
    await test.locator("#create").click();
    instanceId = (await (await created).json()).id;
    await test.waitForFunction(() => webTerminalViews.get("1")?.terminal?.peer.isPrimary);
    const first = test.locator('.terminal-window[data-view="1"]');
    await first.locator(".thumbnail").click();
    await test.waitForFunction(() => webTerminalViews.get("2")?.terminal?.connected);
    const second = test.locator('.terminal-window[data-view="2"]');
    await waitAuto();
    await test.request.post(`${origin}/api/terminals/${instanceId}/controls`, {
      headers: { Origin: origin }, data: { paused: true }
    });
    const windowBefore = await first.boundingBox();
    const original = await test.evaluate(() => webTerminalViews.get("1").terminal.geometry);
    stage = "decreasing font size";
    await first.locator(".font-smaller").click({ clickCount: 3 });
    await test.waitForFunction(() => webTerminalViews.get("1").terminal.sizing.fontSize === 13);
    await waitAuto();
    const smaller = await test.evaluate(() => webTerminalViews.get("1").terminal.geometry);
    check(smaller.columns > original.columns && smaller.rows > original.rows, "Smaller text did not fit more cells");
    stage = "increasing font size";
    await first.locator(".font-larger").click();
    await waitAuto();
    const larger = await test.evaluate(() => webTerminalViews.get("1").terminal.geometry);
    check(larger.columns < smaller.columns && larger.rows < smaller.rows, "Larger text did not fit fewer cells");
    const windowAfter = await first.boundingBox();
    check(windowBefore.width === windowAfter.width && windowBefore.height === windowAfter.height, "Font controls resized the window");
    check(await second.locator(".font-smaller").isDisabled() && await second.locator(".font-larger").isDisabled() &&
      await second.locator(".view-resolution").isDisabled(), "Secondary sizing controls were enabled");
    check(await test.evaluate(() => {
      try { webTerminalViews.get("2").terminal.setSizing({ mode: "fixed", columns: 80, rows: 24 }); return false; }
      catch (error) { return error.message.includes("primary"); }
    }), "Secondary API changed sizing");

    stage = "font size limits";
    await test.evaluate(() => webTerminalViews.get("1").terminal.setSizing({ mode: "auto", fontSize: 8 }));
    await waitAuto();
    check(await first.locator(".font-smaller").isDisabled(), "Minimum font size was not reflected in controls");
    await test.evaluate(() => webTerminalViews.get("1").terminal.setSizing({ mode: "auto", fontSize: 32 }));
    await waitAuto();
    check(await first.locator(".font-larger").isDisabled(), "Maximum font size was not reflected in controls");
    stage = "selecting a fixed grid";
    await first.locator(".view-resolution").selectOption("120x40");
    await waitGrid(120, 40);
    check(await first.locator(".font-smaller").isDisabled() && await first.locator(".font-larger").isDisabled(),
      "Fixed-grid mode left Auto font controls enabled");
    check(await first.locator(".font-size").textContent() === "Fit", "Fixed-grid mode did not advertise fit scaling");

    stage = "resizing a fixed-grid window";
    await first.locator(".view-title").click();
    const beforeFixedResize = resizeCount();
    const handle = await first.locator(".resize-handle").boundingBox();
    await test.mouse.move(handle.x + 5, handle.y + 5);
    await test.mouse.down();
    await test.mouse.move(handle.x - 175, handle.y - 95, { steps: 10 });
    await test.mouse.up();
    await test.waitForFunction(() => {
      const view = webTerminalViews.get("1");
      const box = view.element.querySelector(".terminal-mount").getBoundingClientRect();
      const surface = view.terminal.element.shadowRoot.querySelector(".surface").getBoundingClientRect();
      const scale = Math.min(box.width / 1200, box.height / 800);
      return Math.abs(surface.width - 1200 * scale) < .1 && Math.abs(surface.height - 800 * scale) < .1;
    });
    await test.waitForTimeout(200);
    await waitGrid(120, 40);
    check(resizeCount() === beforeFixedResize, "Resizing a pinned grid sent a new producer resize");
    const fixedWindow = await first.boundingBox();
    check(fixedWindow.width < windowBefore.width && fixedWindow.height < windowBefore.height, "Fixed-grid window did not resize");

    stage = "returning to Auto with the keyboard";
    await first.locator(".view-resolution").focus();
    await test.keyboard.press("a");
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => webTerminalViews.get("1").terminal.sizing.mode === "auto");
    await waitAuto();
    check(await test.evaluate(() => webTerminalViews.get("1").terminal.sizing.fontSize === 32), "Auto did not restore its previous font size");
    stage = "taking primary from a fixed-grid view";
    await first.locator(".view-resolution").selectOption("80x24");
    await waitGrid(80, 24);
    await second.locator(".take-primary").click();
    await test.waitForFunction(() => webTerminalViews.get("2").terminal.peer.isPrimary);
    await waitAuto();
    check(await first.locator(".view-resolution").inputValue() === "follow" &&
      await first.locator(".view-resolution").isDisabled(), "Former primary did not switch to follow-primary controls");
    stage = "restoring fixed sizing on takeover";
    await first.locator(".take-primary").click();
    await test.waitForFunction(() => webTerminalViews.get("1").terminal.peer.isPrimary);
    await waitGrid(80, 24);
    check(await first.locator(".view-resolution").inputValue() === "80x24", "Takeover did not restore the view's fixed-grid policy");
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, original, smaller, larger, fixedGrid: "120x40", retainedGridAfterTakeover: "80x24",
      fixedWindow: { width: fixedWindow.width, height: fixedWindow.height }, browserErrors: errors };
  } catch (error) {
    const state = await test.evaluate(() => ({
      status: document.getElementById("status")?.textContent,
      views: [...(window.webTerminalViews?.values() ?? [])].map(view => ({
        id: view.id, sizing: view.terminal?.sizing, geometry: view.terminal?.geometry,
        peer: view.terminal?.peer, statsGrid: [view.stats.columns, view.stats.rows]
      }))
    }));
    throw new Error(`${stage}: ${error.message}; ${JSON.stringify({ state, commands })}`, { cause: error });
  } finally {
    try {
      if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
