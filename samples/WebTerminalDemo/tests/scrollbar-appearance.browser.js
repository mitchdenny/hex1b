async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({
    viewport: { width: 1440, height: 1000 }, deviceScaleFactor: 2
  });
  const test = await context.newPage();
  const errors = [], reports = [];
  let instanceId, stage = "mount";
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  test.on("pageerror", error => errors.push(error.message));
  try {
    for (const renderer of ["webgpu", "webgl2"]) {
      stage = `${renderer}: mount`;
      await test.goto(`${origin}/?scene=shell&renderer=${renderer}`);
      await test.waitForFunction(() => [...webTerminalViews.values()][0]?.instance.id);
      instanceId = await test.evaluate(() => [...webTerminalViews.values()][0].instance.id);
      await test.waitForFunction(() => [...webTerminalViews.values()][0]?.phase === "connected" &&
        /[\u276f$#%>]$/.test([...webTerminalViews.values()][0].terminal.screenText.trimEnd()));
      await test.evaluate(() => {
        window.appearanceView = [...webTerminalViews.values()][0];
        window.appearanceTerminal = appearanceView.terminal;
        window.originalAppearanceConnection = appearanceView.connectionId;
        appearanceTerminal.focus();
      });
      await test.keyboard.type("printf '%b' '\\033]133;C;cmdline_url=printf%20%3Chello%3E\\007'; i=1; while [ \"$i\" -le 140 ]; do printf 'APPEARANCE-%03d\\n' \"$i\"; i=$((i+1)); done; printf '%b' '\\033]133;D;7\\007__APPEARANCE_READY__\\n'");
      await test.keyboard.press("Enter");
      await test.waitForFunction(() => appearanceTerminal.viewport.liveTop > 80 &&
        appearanceTerminal.screenText.split("\n").some(row => row.trim() === "__APPEARANCE_READY__"));

      stage = `${renderer}: capsule and translucent track pixels`;
      await test.evaluate(async () => {
        window.scrollbarApi = await import("/web-terminal/index.js");
        appearanceTerminal.setScrollbar({ markers: false, hideDelay: 60000, render(frame) {
          window.appearanceFrame = frame;
          scrollbarApi.renderDefaultScrollbar(frame);
        } });
        window.chromePixels = () => {
          const { canvas, track, thumb, layout } = appearanceFrame;
          const scale = canvas.width / layout.width;
          const sample = (x, y) => [...canvas.getContext("2d").getImageData(
            Math.floor(x * scale), Math.floor(y * scale), 1, 1).data];
          return {
            track: sample(track.left + 0.5, thumb.top + thumb.height / 2),
            center: sample(thumb.left + thumb.width / 2, thumb.top + thumb.height / 2),
            corner: sample(thumb.left + 2.25, thumb.top + 0.25)
          };
        };
        window.markPoint = id => {
          const t = appearanceTerminal, marker = t.markers.find(mark => mark.id === id);
          const l = t.layout, track = l.scrollbar, b = t.element.getBoundingClientRect();
          const y = track.top + (track.height - 3) * marker.row / Math.max(1, t.viewport.totalRows - 1) + 1.5;
          const thumbHeight = Math.min(track.height, Math.max(24,
            track.height * (t.viewport.totalRows - t.viewport.liveTop) / t.viewport.totalRows));
          const thumbTop = track.top + (track.height - thumbHeight) * t.viewport.top / t.viewport.liveTop;
          const x = y >= thumbTop && y < thumbTop + thumbHeight ? track.left - 3 : track.left + track.width / 2;
          return {
            x: b.left + x * b.width / l.width,
            y: b.top + y * b.height / l.height
          };
        };
      });
      await test.waitForFunction(() => window.appearanceFrame?.opacity === 1);
      const defaults = await test.evaluate(() => chromePixels());
      check(defaults.track[3] >= 88 && defaults.track[3] <= 90, `Track alpha is not 35%: ${defaults.track}`);
      check(defaults.center[3] === 255, `Thumb center is not opaque: ${defaults.center}`);
      check(defaults.corner[3] < 140, `Thumb corner is rectangular: ${defaults.corner}`);

      stage = `${renderer}: configured colors and opacity`;
      await test.evaluate(() => {
        const paint = scrollbarApi.createDefaultScrollbarRenderer({
          track: { color: "#00ff00", opacity: 0.2 }, thumb: { color: "#ff0000", opacity: 0.5 }
        });
        window.appearanceFrame = null;
        appearanceTerminal.setScrollbar({ markers: false, hideDelay: 60000, render(frame) {
          window.appearanceFrame = frame;
          paint(frame);
        } });
      });
      await test.waitForFunction(() => window.appearanceFrame?.opacity === 1);
      const configured = await test.evaluate(() => chromePixels());
      check(configured.track[0] === 0 && configured.track[1] === 255 &&
        configured.track[3] >= 50 && configured.track[3] <= 52, "Configured track color/opacity was ignored");
      check(configured.center[3] >= 152 && configured.center[3] <= 154,
        `Thumb opacity did not composite over the track: ${configured.center}`);

      stage = `${renderer}: safe default hover content`;
      await test.evaluate(() => appearanceTerminal.scrollToRow(40));
      await test.waitForFunction(() => !appearanceTerminal.viewport.pending && appearanceTerminal.viewport.top === 40);
      await test.evaluate(async () => {
        const viewport = appearanceTerminal.viewport;
        window.appearanceBookmark = await appearanceTerminal.addMarker({
          position: { generation: viewport.generation, rowId: viewport.rowIds[5], column: 0 },
          label: "<img src=x> & bookmark", color: "#00ffff"
        });
        appearanceTerminal.setScrollbar({ hideDelay: 60000 });
      });
      const bookmarkPoint = await test.evaluate(() => markPoint(appearanceBookmark.id));
      await test.mouse.move(bookmarkPoint.x, bookmarkPoint.y);
      const tooltip = test.locator(".hex1b-scrollbar-tooltip-overlay");
      await tooltip.getByRole("tooltip").waitFor({ state: "visible" });
      check((await tooltip.innerText()).includes("<img src=x> & bookmark"), "Default tooltip lost the label");
      check(await tooltip.locator("img").count() === 0, "Tooltip interpreted marker text as HTML");
      await test.mouse.down();
      check(await tooltip.locator("*").count() === 0, "Tooltip remained visible during a scrollbar gesture");
      await test.mouse.move(bookmarkPoint.x, bookmarkPoint.y + 10);
      check(await tooltip.locator("*").count() === 0, "Dragging reopened a tooltip");
      await test.mouse.up();
      await test.mouse.move(1, 1);
      check(await tooltip.locator("*").count() === 0, "Pointer leave retained the tooltip");

      stage = `${renderer}: retained command details`;
      const commandPoint = await test.evaluate(() => {
        const mark = appearanceTerminal.markers.find(mark => mark.source === "command" && mark.phase === "executing");
        if (!mark) throw new Error("Missing retained command mark");
        return markPoint(mark.id);
      });
      await test.mouse.move(commandPoint.x, commandPoint.y);
      await test.waitForFunction(() =>
        appearanceTerminal.element.querySelector(".hex1b-scrollbar-tooltip-overlay")?.textContent.includes("printf <hello>"));
      check(await tooltip.locator("hello").count() === 0, "Command text was interpreted as HTML");

      stage = `${renderer}: custom HTML placement and cleanup under ancestor scaling`;
      await test.evaluate(() => {
        window.tooltipSignals = [];
        appearanceTerminal.element.style.transform = "scale(.85)";
        appearanceTerminal.element.style.transformOrigin = "top left";
        appearanceTerminal.setScrollbar({ hideDelay: 60000, tooltip(state) {
          tooltipSignals.push(state.signal);
          const element = document.createElement("div");
          element.className = "fixture-tooltip";
          element.textContent = `Custom: ${state.marker.label ?? state.marker.id}`;
          element.style.cssText = "width:200px;height:48px;background:#123;color:white";
          return element;
        } });
      });
      const scaledPoint = await test.evaluate(() => markPoint(appearanceBookmark.id));
      await test.mouse.move(scaledPoint.x, scaledPoint.y);
      await tooltip.locator(".fixture-tooltip").waitFor({ state: "visible" });
      check(await test.evaluate(() => {
        const tip = appearanceTerminal.element.querySelector(".fixture-tooltip").getBoundingClientRect();
        const root = appearanceTerminal.element.getBoundingClientRect();
        const point = markPoint(appearanceBookmark.id);
        return tip.left >= root.left && tip.top >= root.top && tip.right <= root.right + 1 &&
          tip.bottom <= root.bottom + 1 && tip.right < point.x;
      }), "Custom tooltip escaped scaled terminal bounds or covered its mark");
      await test.mouse.move(1, 1);
      check(await test.evaluate(() => tooltipSignals.every(signal => signal.aborted)),
        "Custom tooltip lifetime was not aborted on leave");
      await test.evaluate(() => { appearanceTerminal.element.style.transform = ""; });

      stage = `${renderer}: live demo renderer and tooltip controls`;
      const view = test.locator(".terminal-window").first();
      for (const painter of ["default", "styled", "drawn", "custom"]) {
        await view.locator(".view-scrollbar-fade").selectOption(painter);
        await test.waitForFunction(() => {
          const canvas = appearanceTerminal.element.shadowRoot.querySelector(".scrollbar-canvas");
          const pixels = canvas.getContext("2d").getImageData(0, 0, canvas.width, canvas.height).data;
          return pixels.some((value, index) => index % 4 === 3 && value > 0);
        });
        check(await test.evaluate(() => !/Scrollbar(?: tooltip)?:/.test(
          appearanceTerminal.element.shadowRoot.querySelector(".inspection-message").textContent)),
        `${painter} painter reported an isolated rendering error`);
      }
      await view.locator(".view-scrollbar-tooltip").selectOption("custom");
      const demoPoint = await test.evaluate(() => markPoint(appearanceBookmark.id));
      await test.mouse.move(demoPoint.x, demoPoint.y);
      await test.waitForFunction(() =>
        appearanceTerminal.element.querySelector(".hex1b-scrollbar-tooltip-overlay")?.childElementCount > 0);
      await view.locator(".view-scrollbar-tooltip").selectOption("off");
      await test.mouse.move(demoPoint.x, demoPoint.y);
      check(await tooltip.locator("*").count() === 0, "Tooltip off mode still renders content");
      await view.locator(".view-scrollbar-tooltip").selectOption("default");
      await test.mouse.move(demoPoint.x, demoPoint.y);
      await tooltip.getByRole("tooltip").waitFor({ state: "visible" });
      await test.evaluate(() => appearanceTerminal.setScrollbar(false));
      check(await tooltip.locator("*").count() === 0, "Disabling canvas chrome retained the tooltip");
      check(await test.evaluate(() => appearanceTerminal === appearanceView.terminal &&
        originalAppearanceConnection === appearanceView.connectionId), "Appearance controls remounted the terminal");
      reports.push({ renderer, defaults, configured });
      await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
      instanceId = null;
    }
    check(errors.length === 0, errors.join("; "));
    return { passed: true, reports, errors };
  } catch (error) {
    throw new Error(`${stage}: ${error.message}`);
  } finally {
    if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    await context.close();
  }
}
