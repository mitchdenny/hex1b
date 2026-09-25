async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0];
  if (!origin) throw new Error("Open the running WebTerminalDemo before executing this fixture");
  const context = await page.context().browser().newContext({
    viewport: { width: 900, height: 650 }, deviceScaleFactor: 2
  });
  const test = await context.newPage();
  const errors = [], reports = [];
  const check = (value, message) => { if (!value) throw new Error(message); };
  test.on("pageerror", error => errors.push(error.message));
  try {
    await test.goto(`${origin}/?empty=1`);
    await test.evaluate(async () => {
      const { ScrollbarController, normalizeScrollbar, renderDefaultScrollbar } =
        await import("/web-terminal-test/scrollbar.js");
      const element = document.createElement("div");
      element.id = "capsule-fixture";
      element.style.cssText = "position:fixed;left:100px;top:80px;width:380px;height:420px;z-index:10000";
      const content = document.createElement("pre");
      content.textContent = Array.from({ length: 24 }, (_, i) => `History row ${i + 1} ...`).join("\n");
      content.style.cssText = "margin:0;font:14px/17px monospace";
      const canvas = document.createElement("canvas"), accessibility = document.createElement("div");
      canvas.style.cssText = "position:absolute;inset:0;width:100%;height:100%;pointer-events:none";
      accessibility.style.cssText = "pointer-events:none;outline:none";
      element.append(content, canvas, accessibility);
      document.body.append(element);
      const state = {
        connected: true, configuration: false,
        layout: {
          width: 380, height: 420, content: { left: 0, top: 0, width: 360, height: 420 },
          scrollbar: { left: 348, top: 0, width: 12, height: 420 },
          padding: { left: 0, right: 20, top: 0, bottom: 0 }, cellWidth: 8, cellHeight: 17
        },
        viewport: {
          available: true, generation: "fixture", buffer: "main", totalRows: 1000000,
          liveTop: 999976, top: 500000, requestId: 1, following: false, pending: false
        },
        markers: Array.from({ length: 200 }, (_, i) => ({
          id: `mark-${i}`, source: "custom", row: i * 5000, column: 0, buffer: "main",
          label: `Retained mark ${i}`, color: "#ee4444"
        }))
      };
      window.capsuleFixture = { element, canvas, state, frame: null, hover: null, jumps: [], errors: [] };
      const fixture = capsuleFixture;
      const controller = new ScrollbarController({
        element, canvas, accessibility, getState: () => state,
        scrollToRow: top => { state.viewport.top = top; controller.refresh(); },
        scrollToLive: () => { state.viewport.top = state.viewport.liveTop; controller.refresh(); },
        scrollToMarker: async id => { fixture.jumps.push(id); },
        reportError: error => { fixture.errors.push(String(error)); },
        onMarkerHover: tick => { fixture.hover = tick; }
      });
      fixture.controller = controller;
      fixture.configure = (width, top, placement, theme) => {
        state.layout.scrollbar = { left: 360 - width, top: 0, width, height: 420 };
        state.layout.content.width = placement === "beside" ? 360 - width : 360;
        state.viewport.top = top;
        element.style.background = theme === "light" ? "#eeeeee" : "#202020";
        element.style.color = theme === "light" ? "#333333" : "#cccccc";
        state.configuration = normalizeScrollbar({ placement, width, hideDelay: 60000, render(frame) {
          fixture.frame = frame;
          renderDefaultScrollbar(frame);
        } });
        fixture.frame = null;
        controller.refresh();
      };
      fixture.point = (x, y) => {
        const bounds = element.getBoundingClientRect();
        return { x: bounds.left + x, y: bounds.top + y };
      };
    });
    for (const theme of ["dark", "light"])
      for (const placement of ["overlay", "beside"])
        for (const width of [4, 12, 64])
          for (const top of [0, 500000, 999976]) {
            await test.evaluate(({ width, top, placement, theme }) =>
              capsuleFixture.configure(width, top, placement, theme), { width, top, placement, theme });
            await test.waitForFunction(() => capsuleFixture.frame !== null);
            const pixels = await test.evaluate(() => {
              const { canvas, frame } = capsuleFixture;
              const { thumb, markers, track } = frame;
              const ctx = canvas.getContext("2d"), scale = canvas.width / frame.layout.width;
              const pixel = (x, y) => [...ctx.getImageData(Math.floor(x * scale), Math.floor(y * scale), 1, 1).data];
              const near = markers.filter(tick => tick.bounds.top >= thumb.top + 3 &&
                tick.bounds.top + tick.bounds.height <= thumb.top + thumb.height - 3);
              return {
                aligned: canvas.getBoundingClientRect().width === frame.layout.width &&
                  canvas.getBoundingClientRect().height === frame.layout.height,
                thumb: near.map(tick => pixel(thumb.left + thumb.width / 2, tick.bounds.top + tick.bounds.height / 2)),
                wing: near.map(tick => pixel(track.left - 2, tick.bounds.top + tick.bounds.height / 2)),
                far: markers.some(tick => tick.bounds.width === tick.bounds.height),
                narrow: markers.every(tick => tick.bounds.height < thumb.width - Math.min(2, thumb.width / 4) * 2)
              };
            });
            check(pixels.thumb.length > 0, "Fixture must actually overlap marks with the thumb");
            check(pixels.aligned, "Canvas CSS pixels and input geometry must coincide");
            check(pixels.thumb.every(rgba => rgba.join(",") === "153,153,153,255"),
              `Dense marks interrupted the ${width}px ${theme}/${placement} thumb: ${JSON.stringify(pixels.thumb)}`);
            check(pixels.wing.every(rgba => rgba[0] > rgba[1] && rgba[3] > 0), "Exposed wings were not painted");
            check(pixels.far && pixels.narrow, "Distant circles or diameter constraints were lost");
            reports.push({ theme, placement, width, top });
          }

    await test.evaluate(() => capsuleFixture.configure(12, 500000, "overlay", "dark"));
    await test.waitForFunction(() => capsuleFixture.frame !== null);
    const points = await test.evaluate(() => {
      const f = capsuleFixture, { track, thumb, markers } = f.frame;
      const tick = markers.find(tick => tick.bounds.top > thumb.top + 5 &&
        tick.bounds.top + tick.bounds.height < thumb.top + thumb.height - 5);
      const y = tick.bounds.top + tick.bounds.height / 2;
      return { wing: f.point(track.left - 3, y), thumb: f.point(track.left + track.width / 2, y) };
    });
    await test.mouse.move(points.wing.x, points.wing.y);
    await test.waitForFunction(() => capsuleFixture.hover !== null);
    const id = await test.evaluate(() => capsuleFixture.hover.marker.id);
    await test.mouse.click(points.wing.x, points.wing.y);
    check(await test.evaluate(id => capsuleFixture.jumps[0] === id, id), "Wing click did not navigate");
    await test.mouse.move(points.thumb.x, points.thumb.y);
    check(await test.evaluate(() => capsuleFixture.hover === null), "Occluded marker stole thumb hover");
    await test.mouse.down();
    await test.mouse.move(points.thumb.x, points.thumb.y - 50, { steps: 5 });
    await test.waitForFunction(() => capsuleFixture.frame.interaction.dragging);
    check(await test.evaluate(() => capsuleFixture.state.viewport.top < 500000 &&
      capsuleFixture.jumps.length === 1), "Dense marks prevented thumb dragging");
    await test.locator("#capsule-fixture").screenshot({ path: ".playwright-cli/scrollbar-mark-capsules-drag.png" });
    await test.mouse.up();
    await test.mouse.move(1, 1);
    check(await test.evaluate(() => capsuleFixture.errors.length === 0), "Controller reported an error");
    check(errors.length === 0, errors.join("; "));
    return { passed: true, cases: reports.length, reports };
  } finally {
    await context.close();
  }
}
