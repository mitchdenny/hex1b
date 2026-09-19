async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1000 } });
  const test = await context.newPage();
  const errors = [];
  let instanceId, stage = "mounting compact workspace";
  const check = (value, message) => { if (!value) throw new Error(message); };
  test.on("pageerror", error => errors.push(error.message));
  try {
    await test.goto(`${origin}/?scene=shell&renderer=webgl2`);
    await test.waitForFunction(() => [...webTerminalViews.values()][0]?.instance.id);
    instanceId = await test.evaluate(() => [...webTerminalViews.values()][0].instance.id);
    await test.waitForFunction(() => [...webTerminalViews.values()][0]?.phase === "connected" &&
      /[\u276f$#%>]$/.test([...webTerminalViews.values()][0].terminal.screenText.trimEnd()));
    await test.evaluate(() => {
      window.markerView = [...webTerminalViews.values()][0];
      window.markerTerminal = markerView.terminal;
      window.markerConnection = markerView.connectionId;
    });
    const view = test.locator(".terminal-window").first();
    const controls = test.locator("#terminal-controls");
    check(!await controls.evaluate(element => element.open), "Creation controls did not start collapsed");
    const compactHeight = await test.locator("#workspace").evaluate(element => element.clientHeight);
    check(compactHeight > 600, `Compact workspace is still letterboxed: ${compactHeight}px`);
    await controls.locator("summary").click();
    check(await test.locator("#create").isVisible(), "Expanded controls cannot create a terminal");
    const expandedHeight = await test.locator("#workspace").evaluate(element => element.clientHeight);
    check(compactHeight - expandedHeight > 100, "Collapsing creation controls did not reclaim vertical space");
    await controls.locator("summary").click();
    check(await test.evaluate(() => markerView.terminal === markerTerminal &&
      markerView.connectionId === markerConnection && markerTerminal.connected),
    "Collapsing controls replaced the terminal connection");

    stage = "discoverable marks without scrollback";
    const marks = view.locator(".view-marker-details");
    await marks.locator("summary").click();
    check(await view.locator(".marker-hint").innerText().then(text => text.includes("OSC 133")),
      "Empty marks menu does not explain where shell marks come from");
    await marks.locator("summary").press("Escape");
    check(!await marks.evaluate(element => element.open), "Escape did not close the marks menu");
    check(await marks.locator("summary").evaluate(element => element === document.activeElement),
      "Escape did not restore focus to the marks toggle");
    await view.locator(".add-bookmark").click();
    await test.waitForFunction(() => markerTerminal.markers.some(marker => marker.source === "custom"));
    await marks.locator("summary").click();
    const bookmark = view.locator(".marker-jump[data-source=custom]");
    check(await bookmark.isVisible() && await bookmark.isEnabled(), "Bookmark is invisible without scrollback");
    await bookmark.click();
    await test.waitForFunction(() => !markerTerminal.viewport.pending);
    await marks.locator("summary").press("Escape");

    stage = "real shell marks and bookmark reveal";
    await test.evaluate(() => markerTerminal.focus());
    await test.keyboard.type("printf '\\033]133;C;cmdline_url=marker-demo\\007'; i=1; while [ \"$i\" -le 100 ]; do printf 'MARKER-ROW-%03d\\n' \"$i\"; i=$((i+1)); done; printf '\\033]133;D;7\\007__MARKERS_READY__\\n'");
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => markerTerminal.viewport.liveTop > 0 &&
      markerTerminal.screenText.split("\n").some(row => row.trim() === "__MARKERS_READY__") &&
      markerTerminal.markers.some(marker => marker.phase === "finished" && marker.exitCode === 7));
    await test.evaluate(() => {
      window.hasMarkerPixels = () => {
        const canvas = markerTerminal.element.shadowRoot.querySelector(".scrollbar-canvas");
        const pixels = canvas.getContext("2d").getImageData(0, 0, canvas.width, canvas.height).data;
        return pixels.some((value, index) => index % 4 === 3 && value > 0);
      };
    });
    await test.mouse.move(1, 1);
    await test.waitForFunction(() => !hasMarkerPixels());
    await view.locator(".add-bookmark").click();
    await test.waitForFunction(() => hasMarkerPixels());
    await marks.locator("summary").click();
    check(await view.locator(".marker-jump[data-error=true]").innerText().then(text => text.includes("exit 7")),
      "Failed command has no visible exit status");
    const command = await test.evaluate(() => markerTerminal.markers.find(marker => marker.phase === "executing"));
    check(!!command, "Executing command mark was not retained");
    await view.locator(`.marker-jump[data-marker="${command.id}"]`).click();
    await test.waitForFunction(id => {
      const marker = markerTerminal.markers.find(marker => marker.id === id);
      return !markerTerminal.viewport.pending &&
        markerTerminal.viewport.top === Math.min(marker.row, markerTerminal.viewport.liveTop);
    }, command.id);

    stage = "marks remain usable when scrollbar chrome is disabled";
    await marks.locator("summary").press("Escape");
    await view.locator(".view-scrollbar").selectOption("disabled");
    await test.evaluate(async () => {
      const viewport = markerTerminal.viewport;
      await markerTerminal.addMarker({
        position: { generation: viewport.generation, rowId: viewport.rowIds[0], column: 0 },
        label: "<img src=x> safe bookmark"
      });
    });
    await marks.locator("summary").click();
    check(await view.locator(".marker-panel img").count() === 0, "Marker labels were interpreted as HTML");
    check(await view.getByRole("button", { name: /<img src=x> safe bookmark/ }).isEnabled(),
      "Disabling scrollbar chrome disabled marker navigation");
    check(await test.evaluate(() => markerTerminal === markerView.terminal &&
      markerConnection === markerView.connectionId), "Marker UI remounted the terminal");
    await test.goto(`${origin}/?empty=1`);
    check(await controls.evaluate(element => element.open), "Empty workspace hides creation controls");
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, compactHeight, expandedHeight, errors };
  } catch (error) {
    throw new Error(`${stage}: ${error.message}`);
  } finally {
    if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    await context.close();
  }
}
