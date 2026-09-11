async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({ viewport: { width: 1400, height: 1000 } });
  const test = await context.newPage();
  const errors = [];
  let instanceId;
  test.on("pageerror", error => errors.push(error.message));
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const waitState = async (progress, phase) => test.waitForFunction(({ progress, phase }) =>
    [...webTerminalViews.values()].length > 0 && [...webTerminalViews.values()].every(view =>
      view.terminal?.connected && view.terminal.progress.state === progress &&
      view.terminal.shellIntegration.phase === phase &&
      view.element.dataset.progress === progress && view.element.dataset.shellPhase === phase),
  { progress, phase }, { timeout: 30000 });
  try {
    await test.goto(`${origin}/?scene=activity&renderer=webgl2`);
    await test.waitForFunction(() => [...webTerminalViews.values()].some(view => view.terminal?.connected),
      null, { timeout: 30000 });
    instanceId = await test.evaluate(() => [...webTerminalViews.values()][0].instance.id);
    await waitState("indeterminate", "executing");
    check(await test.evaluate(() => [...webTerminalViews.values()].every(view => {
      const bar = view.element.querySelector(".activity-progress");
      return !bar.hidden && !bar.hasAttribute("value") &&
        view.element.querySelector(".shell-status").textContent.includes("Command running");
    })), "Busy state did not update host-owned chrome");

    const paused = await test.request.post(`${origin}/api/terminals/${instanceId}/controls`, {
      headers: { Origin: origin }, data: { paused: true }
    });
    check(paused.status() === 204, "Failed to pause controlled activity");
    await test.selectOption("#transport", "hmp1");
    await test.click("#attach");
    await test.waitForFunction(() => [...webTerminalViews.values()].length === 2 &&
      [...webTerminalViews.values()].every(view => view.terminal?.connected),
    null, { timeout: 30000 });
    await test.waitForFunction(() => {
      const [first, second] = [...webTerminalViews.values()];
      return JSON.stringify(first.terminal.progress) === JSON.stringify(second.terminal.progress) &&
        JSON.stringify(first.terminal.shellIntegration) === JSON.stringify(second.terminal.shellIntegration);
    }, null, { timeout: 30000 });
    const retained = await test.evaluate(() => {
      const view = [...webTerminalViews.values()].find(view => view.transport === "hmp1");
      const state = { progress: view.terminal.progress, shell: view.terminal.shellIntegration,
        revision: view.terminal.stats.revision };
      view.terminal.resync();
      return state;
    });
    await test.waitForFunction(retained => [...webTerminalViews.values()]
      .filter(view => view.transport === "hmp1").every(view =>
        view.terminal.stats.revision > retained.revision &&
        JSON.stringify(view.terminal.progress) === JSON.stringify(retained.progress) &&
        JSON.stringify(view.terminal.shellIntegration) === JSON.stringify(retained.shell)),
    retained, { timeout: 30000 });

    await test.locator('[data-transport="hmp1"] .close-view').click();
    await test.waitForFunction(() => webTerminalViews.size === 1);
    await test.click("#attach");
    await test.waitForFunction(retained => webTerminalViews.size === 2 &&
      [...webTerminalViews.values()].filter(view => view.transport === "hmp1").every(view =>
        view.terminal?.connected &&
        JSON.stringify(view.terminal.progress) === JSON.stringify(retained.progress) &&
        JSON.stringify(view.terminal.shellIntegration) === JSON.stringify(retained.shell)),
    retained, { timeout: 30000 });

    const resumed = await test.request.post(`${origin}/api/terminals/${instanceId}/controls`, {
      headers: { Origin: origin }, data: { paused: false }
    });
    check(resumed.status() === 204, "Failed to resume activity");
    await waitState("normal", "executing");
    check(await test.evaluate(() => [...webTerminalViews.values()].every(view => {
      const bar = view.element.querySelector(".activity-progress");
      return !bar.hidden && bar.value === view.terminal.progress.percentage;
    })), "Determinate progress getter and host bar disagree");
    await waitState("warning", "executing");
    await waitState("error", "executing");
    await waitState("none", "finished");
    check(await test.evaluate(() => [...webTerminalViews.values()].every(view =>
      view.terminal.shellIntegration.lastExitCode === 1 &&
      view.element.querySelector(".activity-progress").hidden &&
      view.element.querySelector(".shell-status").textContent.includes("last exit 1"))),
    "Completion or progress clearing was lost");
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, directAndRelayed: true, lateAttachAndReconnect: true,
      states: ["indeterminate", "normal", "warning", "error", "none"] };
  } finally {
    try {
      if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
