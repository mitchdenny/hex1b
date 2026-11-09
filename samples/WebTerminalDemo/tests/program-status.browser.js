async page => {
  const origin = new URL(page.url()).origin;
  const context = await page.context().browser().newContext({ viewport: { width: 1400, height: 1000 } });
  const test = await context.newPage();
  const errors = [];
  let instanceId;
  test.on("pageerror", error => errors.push(error.message));
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const waitStep = async step => test.waitForFunction(step => webTerminalViews.size > 0 &&
    [...webTerminalViews.values()].every(view => view.terminal?.connected &&
      view.terminal.screenText.includes(`Step ${step}:`)), step, { timeout: 30000 });
  const compare = async () => test.waitForFunction(() => {
    const views = [...webTerminalViews.values()];
    return views.length === 2 && views.every(view => view.terminal?.connected) &&
      JSON.stringify(views[0].terminal.programStatus.records) === JSON.stringify(views[1].terminal.programStatus.records);
  }, null, { timeout: 30000 });
  const advance = async step => {
    await test.evaluate(() => [...webTerminalViews.values()]
      .find(view => view.transport === "direct").terminal.focus());
    await test.keyboard.press("Enter");
    await waitStep(step);
  };
  try {
    await test.goto(`${origin}/?scene=program-status&renderer=webgl2`);
    await waitStep(0);
    instanceId = await test.evaluate(() => [...webTerminalViews.values()][0].instance.id);
    check(await test.evaluate(() => [...webTerminalViews.values()][0].terminal.programStatus.records.length === 0),
      "Initial inventory is not empty");
    await advance(1);
    check(await test.evaluate(() => {
      const view = [...webTerminalViews.values()][0];
      const status = view.terminal.programStatus;
      return status.records.length === 3 && status.find("deploy/west").kind === "permission" &&
        status.find("deploy/east").effectiveApp === "hex1b-demo" && !status.find("deploy") &&
        view.element.querySelector('.program-status-record[data-status-id="deploy"]').dataset.group === "true";
    }), "Nested inventory, inheritance or missing-parent grouping failed");
    await test.locator(".view-program-status > summary").click();
    check(await test.locator(".program-status-panel").isVisible(), "Per-view panel did not open");
    await test.locator(".view-program-status > summary").click();
    await test.locator("#toggle-terminal-controls").click();
    await test.selectOption("#transport", "hmp1");
    await test.click("#attach");
    await test.locator("#close-terminal-controls").click();
    await compare();
    await waitStep(1);
    for (const step of [2, 3, 4, 5, 6]) {
      await advance(step);
      await compare();
    }
    check(await test.evaluate(() => [...webTerminalViews.values()].every(view =>
      view.terminal.programStatus.records.length === 2 &&
      view.terminal.programStatus.find("").state === "done" &&
      view.terminal.programStatus.find("deploy/west").state === "error" &&
      !view.element.querySelector(".program-status-list b") &&
      view.element.querySelector(".program-status-list").textContent.includes("<b>Finished</b>"))),
    "Subtree clearing, independent state, or safe text rendering failed");
    const retained = await test.evaluate(() => {
      const view = [...webTerminalViews.values()].find(view => view.transport === "hmp1");
      const result = { revision: view.terminal.stats.revision, records: view.terminal.programStatus.records };
      view.terminal.resync();
      return result;
    });
    await test.waitForFunction(retained => [...webTerminalViews.values()]
      .filter(view => view.transport === "hmp1").every(view =>
        view.terminal.stats.revision > retained.revision &&
        JSON.stringify(view.terminal.programStatus.records) === JSON.stringify(retained.records)),
    retained, { timeout: 30000 });
    await test.locator('.terminal-window[data-transport="hmp1"]').focus();
    await test.locator('[data-transport="hmp1"] .close-view').click();
    await test.waitForFunction(() => webTerminalViews.size === 1);
    await test.locator("#toggle-terminal-controls").click();
    await test.click("#attach");
    await test.locator("#close-terminal-controls").click();
    await compare();
    await waitStep(6);
    for (const step of [7, 8, 9]) {
      await advance(step);
      await compare();
    }
    check(await test.evaluate(() => [...webTerminalViews.values()].every(view =>
      view.terminal.programStatus.records.length === 0 &&
      view.element.querySelector(".program-status-count").textContent === "0")),
    "Authoritative empty inventory did not clear both panels");
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, steps: 9, appCallbackEmission: true, lateAttach: true,
      hmp1Relay: true, reconnectAndResync: true, safeText: true };
  } finally {
    try {
      if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
