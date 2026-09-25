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
    // A fast shell prompt can arrive before the initial automatic resize.
    await test.waitForFunction(() => {
      const { geometry, layout, stats, peer } = markerTerminal;
      return peer.isPrimary && stats.gpu === "ready" &&
        geometry.columns === Math.floor(layout.width / geometry.cellWidth) &&
        geometry.rows === Math.floor(layout.height / geometry.cellHeight);
    });
    const view = test.locator(".terminal-window").first();
    const controls = test.locator("#terminal-controls");
    const dimensions = () => test.evaluate(() => ({
      workspace: document.querySelector("#workspace").getBoundingClientRect().toJSON(),
      window: markerView.element.getBoundingClientRect().toJSON(),
      canvas: markerTerminal.element.shadowRoot.querySelector("canvas:not(.scrollbar-canvas)").getBoundingClientRect().toJSON(),
      columns: markerTerminal.geometry.columns, rows: markerTerminal.geometry.rows
    }));
    check(await controls.getAttribute("data-open") === "false", "Creation controls did not start closed");
    const compactHeight = await test.locator("#workspace").evaluate(element => element.clientHeight);
    check(compactHeight > 600, `Compact workspace is still letterboxed: ${compactHeight}px`);
    const before = await dimensions();
    await test.locator("#toggle-terminal-controls").click();
    await test.waitForFunction(() => getComputedStyle(document.querySelector("#terminal-controls")).transform === "matrix(1, 0, 0, 1, 0, 0)");
    check(await test.locator("#create").isVisible(), "Open controls cannot create a terminal");
    const expandedHeight = await test.locator("#workspace").evaluate(element => element.clientHeight);
    const opened = await dimensions();
    check(JSON.stringify(opened) === JSON.stringify(before),
      `Opening the drawer resized or moved terminal content: ${JSON.stringify({ before, opened })}`);
    check(await controls.evaluate(element => {
      const box = element.getBoundingClientRect();
      return box.right === innerWidth && box.top === 0 && box.height === innerHeight;
    }), "Controls did not slide in from the viewport edge");
    await test.locator("#close-terminal-controls").press("Escape");
    check(await controls.getAttribute("data-open") === "false" && await controls.evaluate(element => element.inert),
      "Escape did not close and deactivate the drawer");
    check(await test.locator("#toggle-terminal-controls").evaluate(element => document.activeElement === element),
      "Closing the drawer did not restore focus");
    check(JSON.stringify(await dimensions()) === JSON.stringify(before), "Closing the drawer resized terminal content");
    check(await test.evaluate(() => markerView.terminal === markerTerminal &&
      markerView.connectionId === markerConnection && markerTerminal.connected),
    "Closing controls replaced the terminal connection");
    await test.locator("#toggle-terminal-controls").click();
    await test.locator("#workspace").click({ position: { x: 5, y: 5 } });
    check(await controls.getAttribute("data-open") === "false", "Clicking outside did not dismiss the drawer");

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
    await test.waitForFunction(() => document.querySelector("#terminal-controls").dataset.open === "true");
    await test.setViewportSize({ width: 360, height: 640 });
    await test.emulateMedia({ reducedMotion: "reduce" });
    await test.locator("#close-terminal-controls").click();
    const narrowWorkspace = await test.locator("#workspace").boundingBox();
    await test.locator("#toggle-terminal-controls").click();
    check(JSON.stringify(await test.locator("#workspace").boundingBox()) === JSON.stringify(narrowWorkspace),
      "Narrow-screen drawer resized the workspace");
    check(await controls.evaluate(element => element.scrollWidth === element.clientWidth &&
      element.getBoundingClientRect().width <= innerWidth && getComputedStyle(element).transitionDuration === "0s"),
    "Narrow drawer overflows horizontally or ignores reduced motion");
    await test.locator("#play-tape").scrollIntoViewIfNeeded();
    check(await controls.evaluate(element => element.scrollTop > 0), "Small-screen controls cannot scroll independently");
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, compactHeight, expandedHeight, narrowWidth: 360, errors };
  } catch (error) {
    throw new Error(`${stage}: ${error.message}`);
  } finally {
    if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    await context.close();
  }
}
