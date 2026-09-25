async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0];
  if (!origin) throw new Error("Open the running WebTerminalDemo before executing this fixture");
  const reports = [];
  const check = (value, message) => { if (!value) throw new Error(message); };
  for (const deviceScaleFactor of [1, 2]) {
    const context = await page.context().browser().newContext({
      viewport: { width: 900, height: 650 }, deviceScaleFactor
    });
    const test = await context.newPage();
    const errors = [];
    test.on("pageerror", error => errors.push(error.message));
    try {
      await test.goto(`${origin}/?empty=1`);
      await test.locator("#close-terminal-controls").click();
      await test.evaluate(async () => {
        const { ScrollbarController, normalizeScrollbar, renderDefaultScrollbar } =
          await import("/web-terminal-test/scrollbar.js");
        const element = document.createElement("div");
        element.id = "circle-fixture";
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
        window.circleFixture = {
          element, canvas, state, frame: null, frames: [], rows: [], hover: null, jumps: [], errors: []
        };
        const fixture = circleFixture;
        const controller = new ScrollbarController({
          element, canvas, accessibility, getState: () => state,
          scrollToRow: top => { fixture.rows.push(top); state.viewport.top = top; controller.refresh(); },
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
            fixture.frames.push(frame);
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
                circleFixture.configure(width, top, placement, theme), { width, top, placement, theme });
              await test.waitForFunction(() => circleFixture.frame !== null);
              const pixels = await test.evaluate(() => {
                const { canvas, frame } = circleFixture;
                const { thumb, markers, track } = frame;
                const ctx = canvas.getContext("2d"), scale = canvas.width / frame.layout.width;
                const pixel = (x, y) => [...ctx.getImageData(Math.floor(x * scale), Math.floor(y * scale), 1, 1).data];
                const near = markers.filter(tick => tick.bounds.top >= thumb.top + 3 &&
                  tick.bounds.top + tick.bounds.height <= thumb.top + thumb.height - 3);
                const fadeLeft = near[0].bounds.left - 6;
                const fadeY = near[0].bounds.top + near[0].bounds.height / 2;
                return {
                  aligned: canvas.getBoundingClientRect().width === frame.layout.width &&
                    canvas.getBoundingClientRect().height === frame.layout.height,
                  thumb: near.map(tick => pixel(thumb.left + thumb.width / 2, tick.bounds.top + tick.bounds.height / 2)),
                  circle: near.map(tick => pixel(tick.bounds.left + tick.bounds.width / 2,
                    tick.bounds.top + tick.bounds.height / 2)),
                  gap: near.map(tick => pixel(track.left - 1, tick.bounds.top + tick.bounds.height / 2)),
                  fade: [fadeLeft - 1, fadeLeft + 0.75, near[0].bounds.left - 1, track.left - 1, track.left]
                    .map(x => pixel(x, fadeY)[3]),
                  unshaded: pixel(track.left - 1, thumb.top < track.top + track.height / 2 ?
                    track.top + track.height - 1 : track.top)[3] === 0,
                  circular: markers.every(tick => tick.bounds.width === tick.bounds.height),
                  far: markers.some(tick => tick.bounds.left > track.left),
                  narrow: markers.every(tick => tick.bounds.height < thumb.width - Math.min(2, thumb.width / 4) * 2)
                };
              });
              check(pixels.thumb.length > 0, "Fixture must actually overlap marks with the thumb");
              check(pixels.aligned, "Canvas CSS pixels and input geometry must coincide");
              check(pixels.thumb.every(rgba => rgba.join(",") === "153,153,153,255"),
                `Dense marks interrupted the ${width}px ${theme}/${placement} thumb: ${JSON.stringify(pixels.thumb)}`);
              check(pixels.circle.every(rgba => rgba[0] > rgba[1] && rgba[3] > 0), "Displaced circles were not painted");
              check(pixels.gap.every(rgba => rgba[0] === rgba[1] && rgba[1] === rgba[2] &&
                rgba[3] > 0 && rgba[3] < 90), "Gap must show only the fading track, not stretched marks");
              check(pixels.fade[0] === 0 && pixels.fade[1] > 0 &&
                pixels.fade.every((alpha, i) => i === 0 || alpha > pixels.fade[i - 1]) &&
                Math.abs(pixels.fade.at(-1) - 255 * 0.35) <= 1,
                `${width}px ${theme}/${placement} at row ${top}: track must fade from transparent to its configured opacity: ${pixels.fade}`);
              check(pixels.circular && pixels.far && pixels.narrow, "Circle shape, centering or diameter constraints were lost");
              check(pixels.unshaded, "Track shadow extends beyond the floated marks");
              reports.push({ deviceScaleFactor, theme, placement, width, top });
            }

      await test.evaluate(() => {
        const f = circleFixture;
        f.denseMarkers = f.state.markers;
        f.state.markers = [{ id: "isolated", source: "custom", row: 500000, column: 0, buffer: "main" }];
        f.configure(12, 500000, "beside", "light");
      });
      await test.waitForFunction(() => circleFixture.frame !== null);
      const isolatedShadow = await test.evaluate(() => {
        const f = circleFixture, { bounds } = f.frame.markers[0], { track } = f.frame;
        const ctx = f.canvas.getContext("2d"), scale = f.canvas.width / f.frame.layout.width;
        f.alpha = (x, y) => ctx.getImageData(Math.floor(x * scale), Math.floor(y * scale), 1, 1).data[3];
        return {
          buffer: f.alpha(bounds.left - 2, bounds.top + bounds.height / 2),
          outside: f.alpha(bounds.left - 7, bounds.top + bounds.height / 2),
          above: f.alpha(track.left - 1, bounds.top - 24),
          below: f.alpha(track.left - 1, bounds.top + bounds.height + 24),
          width: bounds.width
        };
      });
      check(isolatedShadow.width === 5 && isolatedShadow.buffer > 0 && isolatedShadow.outside === 0 &&
        isolatedShadow.above === 0 && isolatedShadow.below === 0,
        `Isolated marker needs a bounded, gently tapered shadow: ${JSON.stringify(isolatedShadow)}`);
      await test.locator("#circle-fixture").screenshot({
        path: `.playwright-cli/scrollbar-mark-circles-isolated-${deviceScaleFactor}.png`
      });
      await test.evaluate(() => {
        const f = circleFixture;
        f.state.markers = Array.from({ length: 40 }, (_, i) => ({ ...f.state.markers[0], id: `duplicate-${i}` }));
        f.configure(12, 500000, "beside", "light");
      });
      await test.waitForFunction(() => circleFixture.frame !== null);
      check(await test.evaluate(expected => {
        const f = circleFixture, { bounds } = f.frame.markers[0];
        return f.alpha(bounds.left - 2, bounds.top + bounds.height / 2) === expected;
      }, isolatedShadow.buffer), "Overlapping shadows darkened with marker density");
      await test.evaluate(() => {
        const f = circleFixture;
        f.state.markers = [480000, 520000].map((row, i) =>
          ({ id: `pair-${i}`, source: "custom", row, column: 0, buffer: "main" }));
        f.configure(12, 500000, "beside", "light");
      });
      await test.waitForFunction(() => circleFixture.frame !== null);
      const sharedShadow = await test.evaluate(() => {
        const f = circleFixture, [a, b] = f.frame.markers.map(tick => tick.bounds);
        const from = a.top + a.height / 2, to = b.top + b.height / 2;
        return Array.from({ length: Math.floor(to - from) + 1 }, (_, i) =>
          f.alpha(a.left - 2, from + i));
      });
      check(sharedShadow.length > 15 && sharedShadow.every(alpha => alpha === isolatedShadow.buffer),
        `Nearby marks need a shared shadow without individual scallops: ${sharedShadow}`);
      await test.locator("#circle-fixture").screenshot({
        path: `.playwright-cli/scrollbar-mark-circles-shared-${deviceScaleFactor}.png`
      });
      await test.evaluate(() => {
        circleFixture.state.markers = [];
        circleFixture.configure(12, 500000, "beside", "light");
      });
      await test.waitForFunction(() => circleFixture.frame !== null);
      check(await test.evaluate(() => {
        const f = circleFixture, { track, thumb } = f.frame;
        return f.alpha(track.left - 1, thumb.top + thumb.height / 2) === 0;
      }), "Empty marker inventory retained an extended shadow");
      await test.evaluate(() => {
        circleFixture.state.markers = circleFixture.denseMarkers;
        circleFixture.configure(12, 500000, "overlay", "dark");
      });
      await test.waitForFunction(() => circleFixture.frame !== null);
      const points = await test.evaluate(() => {
        const f = circleFixture, { track, thumb, markers } = f.frame;
        const tick = markers.find(tick => tick.bounds.top > thumb.top + 5 &&
          tick.bounds.top + tick.bounds.height < thumb.top + thumb.height - 5);
        const y = tick.bounds.top + tick.bounds.height / 2;
        return {
          circle: f.point(tick.bounds.left + tick.bounds.width / 2, y),
          gap: f.point(track.left - 1, y),
          thumb: f.point(track.left + track.width / 2, y)
        };
      });
      await test.mouse.move(points.circle.x, points.circle.y);
      await test.waitForFunction(() => circleFixture.hover !== null);
      const id = await test.evaluate(() => circleFixture.hover.marker.id);
      await test.mouse.click(points.circle.x, points.circle.y);
      check(await test.evaluate(id => circleFixture.jumps[0] === id, id), "Circle click did not navigate");
      await test.mouse.move(points.gap.x, points.gap.y);
      check(await test.evaluate(() => circleFixture.hover === null), "Empty gap stole content hover");
      await test.mouse.move(points.thumb.x, points.thumb.y);
      check(await test.evaluate(() => circleFixture.hover === null), "Displaced marker stole thumb hover");
      await test.mouse.down();
      await test.mouse.move(points.thumb.x, points.thumb.y - 50, { steps: 5 });
      await test.waitForFunction(() => circleFixture.frame.interaction.dragging);
      check(await test.evaluate(() => circleFixture.state.viewport.top < 500000 &&
        circleFixture.jumps.length === 1), "Dense marks prevented thumb dragging");
      await test.locator("#circle-fixture").screenshot({
        path: `.playwright-cli/scrollbar-mark-circles-drag-${deviceScaleFactor}.png`
      });
      await test.mouse.up();
      await test.mouse.move(1, 1);
      await test.waitForFunction(() => !circleFixture.frame.interaction.dragging);
      await test.clock.install();
      await test.evaluate(() => {
        const f = circleFixture;
        Object.assign(f.state.viewport, { totalRows: 25, liveTop: 5, top: 0 });
        f.state.markers = [{ id: "short-history", source: "custom", row: 20, column: 0, buffer: "main" }];
        f.rows = [];
        f.configure(12, 0, "beside", "dark");
      });
      await test.clock.runFor(16);
      const start = await test.evaluate(() => {
        const f = circleFixture, { thumb } = f.frame;
        return { point: f.point(thumb.left + thumb.width / 2, thumb.top + thumb.height / 2),
          top: thumb.top, left: f.frame.markers[0].bounds.left };
      });
      await test.mouse.move(start.point.x, start.point.y);
      await test.mouse.down();
      let lastLeft = start.left;
      for (const offset of [4, 5, 6]) {
        await test.mouse.move(start.point.x, start.point.y + offset);
        await test.clock.runFor(16);
        const motion = await test.evaluate(() => ({
          top: circleFixture.frame.thumb.top, left: circleFixture.frame.markers[0].bounds.left,
          row: circleFixture.state.viewport.top, requests: circleFixture.rows.length
        }));
        check(Math.abs(motion.top - start.top - offset) < 0.001, "Thumb snapped during fractional dragging");
        check(motion.left < lastLeft, "Marks did not move between whole-row requests");
        check(motion.row === 0 && motion.requests === 0, "Fractional drag must not send fractional row requests");
        lastLeft = motion.left;
      }
      await test.mouse.up();
      await test.clock.runFor(160);
      await test.evaluate(() => {
        const f = circleFixture, { thumb } = f.frame;
        f.frames = [];
        const point = f.point(thumb.left + thumb.width / 2, thumb.top + thumb.height / 2);
        f.element.dispatchEvent(new WheelEvent("wheel", {
          clientX: point.x, clientY: point.y, deltaY: 1, deltaMode: 1, bubbles: true, cancelable: true
        }));
      });
      await test.clock.runFor(160);
      const transition = await test.evaluate(() => ({
        positions: circleFixture.frames.map(frame => frame.thumb.top),
        lefts: circleFixture.frames.map(frame => frame.markers[0].bounds.left),
        rows: circleFixture.rows,
        fixed: circleFixture.frames.every(frame => frame.markers[0].bounds.top === 415 * 20 / 24 &&
          frame.markers[0].bounds.width === 5 && frame.markers[0].bounds.height === 5)
      }));
      check(transition.positions.some(top => top > 0 && top < 16.8) &&
        Math.abs(transition.positions.at(-1) - 16.8) < 0.001, "Row step skipped intermediate visual positions");
      check(new Set(transition.lefts).size > 2 && transition.fixed, "Circle motion must animate horizontally only");
      check(transition.rows.join(",") === "1", "Animation generated extra navigation requests");
      await test.clock.runFor(160);
      check(await test.evaluate(count => circleFixture.frames.length === count, transition.positions.length),
        "Settled scrollbar kept scheduling frames");
      await test.emulateMedia({ reducedMotion: "reduce" });
      await test.clock.runFor(16);
      await test.evaluate(() => {
        const f = circleFixture;
        f.frames = [];
        f.state.viewport.top = 2;
        f.controller.refresh();
      });
      await test.clock.runFor(160);
      check(await test.evaluate(() => circleFixture.frames.length === 1 &&
        Math.abs(circleFixture.frame.thumb.top - 33.6) < 0.001), "Reduced motion did not disable row-step animation");
      check(await test.evaluate(() => circleFixture.errors.length === 0), "Controller reported an error");
      check(errors.length === 0, errors.join("; "));
    } finally {
      await context.close();
    }
  }
  return { passed: true, cases: reports.length, reports };
}
