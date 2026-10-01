async page => {
  const check = (condition, message) => {
    if (!condition) throw new Error(message);
  };
  const origin = await page.evaluate(() => location.origin);
  const requestOptions = { headers: { Origin: origin } };
  let instanceId;
  await page.setViewportSize({ width: 1400, height: 1000 });
  await page.goto(`${origin}/?empty=1`);
  try {
    await page.locator("#scene").selectOption("text");
    await page.locator("#create").click();
    await page.waitForFunction(() => [...window.webTerminalViews.values()].some(view => view.phase === "connected"));
    const initial = await page.evaluate(() => {
      const view = [...window.webTerminalViews.values()][0];
      return {
        id: view.id, instance: view.instance.id, connection: view.connectionId,
        bounds: ["left", "top", "width", "height"].map(key => view.element.style[key])
      };
    });
    instanceId = initial.instance;
    const first = page.locator(`[data-view="${initial.id}"]`);
    const restore = page.getByRole("button", { name: "Restore controls", exact: true });
    const waitForGrid = (columns, rows) => page.waitForFunction(
      ({ id, columns, rows }) => {
        const geometry = window.webTerminalViews.get(id).terminal.geometry;
        return geometry.columns === columns && geometry.rows === rows;
      }, { id: initial.id, columns, rows });
    const assertPageFilling = async () => {
      const geometry = await page.locator(".terminal-window.minimal-chrome .terminal-mount").boundingBox();
      const viewport = page.viewportSize();
      check(geometry && geometry.x === 0 && geometry.y === 0 &&
        Math.abs(geometry.width - viewport.width) <= 1 &&
        Math.abs(geometry.height - viewport.height) <= 1, "Terminal must fill the page");
      check(await restore.isVisible(), "Restore button must remain visible");
      check(!await page.getByRole("region", { name: "Terminal controls", exact: true }).isVisible(),
        "Playground controls must be hidden");
    };

    await first.locator(".view-resolution").selectOption("80x24");
    await waitForGrid(80, 24);
    await first.getByRole("button", { name: "Minimal chrome", exact: true }).click();
    await assertPageFilling();
    check(await page.evaluate(id => {
      const view = window.webTerminalViews.get(id);
      return view.element.contains(document.activeElement) && view.terminal.connected;
    }, initial.id), "Entering minimal mode must preserve terminal focus and connection");
    await page.keyboard.press("Escape");
    check(await first.evaluate(element => element.classList.contains("minimal-chrome")),
      "Escape must remain available to terminal applications");
    await page.setViewportSize({ width: 390, height: 844 });
    await assertPageFilling();
    await waitForGrid(80, 24);
    await page.setViewportSize({ width: 1400, height: 1000 });
    await restore.focus();
    await page.keyboard.press("Enter");
    check(!await restore.isVisible(), "Keyboard activation must restore the controls");
    check(await first.evaluate((element, bounds) =>
      ["left", "top", "width", "height"].every((key, index) => element.style[key] === bounds[index]),
    initial.bounds), "Floating window bounds must survive minimal mode");
    check(await page.evaluate(({ id, connection }) =>
      window.webTerminalViews.get(id).connectionId === connection, initial),
    "Toggling chrome must not reconnect the terminal");

    await first.locator(".view-resolution").selectOption("auto");
    await page.waitForFunction(id => {
      const geometry = window.webTerminalViews.get(id).terminal.geometry;
      return geometry.columns !== 80 || geometry.rows !== 24;
    }, initial.id);
    const before = await page.evaluate(id => window.webTerminalViews.get(id).terminal.geometry, initial.id);
    await first.getByRole("button", { name: "Minimal chrome", exact: true }).click();
    await page.waitForFunction(({ id, rows }) => window.webTerminalViews.get(id).terminal.geometry.rows > rows,
      { id: initial.id, rows: before.rows });
    await assertPageFilling();
    await restore.click();
    await waitForGrid(before.columns, before.rows);

    await page.locator("#attach").click();
    await page.waitForFunction(() => window.webTerminalViews.size === 2 &&
      [...window.webTerminalViews.values()].every(view => view.phase === "connected"));
    const secondary = await page.evaluate(id => {
      const view = [...window.webTerminalViews.values()].find(view => view.id !== id);
      return { id: view.id, connection: view.connectionId };
    }, initial.id);
    const second = page.locator(`[data-view="${secondary.id}"]`);
    await second.getByRole("button", { name: "Minimal chrome", exact: true }).click();
    await assertPageFilling();
    check(!await first.isVisible(), "Other views must be hidden");
    check(await page.evaluate(({ firstId, secondId }) => {
      const first = window.webTerminalViews.get(firstId).terminal;
      const second = window.webTerminalViews.get(secondId).terminal;
      return first.connected && second.connected && first.peer.isPrimary && !second.peer.isPrimary;
    }, { firstId: initial.id, secondId: secondary.id }), "Minimal mode must not disconnect peers or take primary ownership");
    await waitForGrid(before.columns, before.rows);

    const failure = await page.request.post(
      `${origin}/api/terminals/${instanceId}/views/${secondary.connection}/failure`,
      { ...requestOptions, data: { mode: "close" } });
    check(failure.ok(), "Failure injection must succeed");
    await second.locator(".closed-overlay").waitFor({ state: "visible" });
    check(await restore.isVisible(), "Restore controls must remain available after disconnection");
    await second.getByRole("button", { name: "Reconnect view", exact: true }).click();
    await page.waitForFunction(id => window.webTerminalViews.get(id).phase === "connected", secondary.id);
    await assertPageFilling();
    const ended = await page.request.delete(`${origin}/api/terminals/${instanceId}`, requestOptions);
    check(ended.ok(), "Ending the test terminal must succeed");
    await second.getByRole("heading", { name: "Terminal ended", exact: true }).waitFor();
    await second.locator(".dismiss-view").click();
    check(!await restore.isVisible(), "Closing the expanded view must exit minimal mode");
    check(await first.isVisible(), "Remaining views must become visible again");
    await first.locator(".dismiss-view").click();
    check(await page.locator("#workspace.empty").count() === 1, "Closing all views must restore the empty workspace");
    return "Minimal chrome: page filling, mobile layout, focus, Escape, fixed/Auto sizing, peer ownership, reconnect, and cleanup passed.";
  } finally {
    if (instanceId) {
      const response = await page.request.delete(`${origin}/api/terminals/${instanceId}`, requestOptions);
      check(response.ok() || response.status() === 404, "Test terminal cleanup failed");
    }
  }
}
