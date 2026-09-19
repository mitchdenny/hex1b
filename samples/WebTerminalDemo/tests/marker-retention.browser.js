async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1000 } });
  const test = await context.newPage();
  let instanceId, stage = "mount";
  const errors = [];
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  test.on("pageerror", error => errors.push(error.message));
  try {
    await test.goto(`${origin}/?scene=shell&renderer=webgl2`);
    await test.waitForFunction(() => [...webTerminalViews.values()][0]?.instance.id);
    instanceId = await test.evaluate(() => [...webTerminalViews.values()][0].instance.id);
    await test.waitForFunction(() => [...webTerminalViews.values()][0]?.phase === "connected" &&
      /[\u276f$#%>]$/.test([...webTerminalViews.values()][0].terminal.screenText.trimEnd()));
    await test.evaluate(() => {
      window.retentionTerminal = [...webTerminalViews.values()][0].terminal;
      retentionTerminal.focus();
    });
    await test.keyboard.type("printf '%b' '\\033]133;C;cmdline_url=retained-seed\\007'; i=1; while [ \"$i\" -le 60 ]; do printf 'SEED-%03d\\n' \"$i\"; i=$((i+1)); done; printf '__SEED_READY__\\n'");
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => retentionTerminal.screenText.split("\n").some(line =>
      line.trim() === "__SEED_READY__") && retentionTerminal.markers.some(mark => mark.phase === "executing"));
    await test.evaluate(() => retentionTerminal.scrollToRow(15));
    await test.waitForFunction(() => !retentionTerminal.viewport.pending && retentionTerminal.viewport.top === 15);
    await test.evaluate(async () => {
      window.retentionCommand = retentionTerminal.markers.find(mark => mark.phase === "executing").id;
      const viewport = retentionTerminal.viewport;
      window.retentionBookmark = await retentionTerminal.addMarker({
        position: { generation: viewport.generation, rowId: viewport.rowIds[0], column: 0 },
        label: "Collect with this retained row"
      });
    });
    stage = "evicting retained content";
    await test.evaluate(() => retentionTerminal.focus());
    await test.keyboard.type("i=1; while [ \"$i\" -le 1100 ]; do printf 'EVICT-%04d\\n' \"$i\"; i=$((i+1)); done; printf '__EVICT_READY__\\n'");
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => retentionTerminal.screenText.split("\n").some(line =>
      line.trim() === "__EVICT_READY__") && !retentionTerminal.markers.some(mark =>
      mark.id === retentionBookmark.id || mark.id === retentionCommand));
    check(await test.evaluate(() => retentionTerminal.viewport.liveTop === 1000), "Producer retention bound changed");
    const marks = test.locator(".terminal-window").first().locator(".view-marker-details");
    await marks.locator("summary").click();
    check(await test.getByRole("button", { name: /Collect with this retained row/ }).count() === 0,
      "The Marks menu retains collected content");
    const rejected = await test.evaluate(async () => {
      try { await retentionTerminal.scrollToMarker(retentionBookmark.id); return false; }
      catch (error) { return error.message === "unknown-marker"; }
    });
    check(rejected, "A collected marker still resolves or navigates to unrelated content");
    await test.evaluate(async () => {
      const viewport = retentionTerminal.viewport;
      await retentionTerminal.addMarker({
        position: { generation: viewport.generation, rowId: viewport.rowIds[0], column: 0 },
        label: "Still retained"
      });
    });
    check(await test.evaluate(() => retentionTerminal.markers.some(mark => mark.label === "Still retained")),
      "Fresh marker registration failed after collection");
    check(errors.length === 0, errors.join("; "));
    return { passed: true, errors };
  } catch (error) {
    throw new Error(`${stage}: ${error.message}`);
  } finally {
    if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    await context.close();
  }
}
