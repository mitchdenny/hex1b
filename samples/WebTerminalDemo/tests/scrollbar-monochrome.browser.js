async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0];
  if (!origin) throw new Error("Open the running WebTerminalDemo before executing this fixture");
  const context = await page.context().browser().newContext({
    viewport: { width: 1440, height: 1100 }, deviceScaleFactor: 2
  });
  const test = await context.newPage();
  const errors = [], reports = [];
  const check = (value, message) => { if (!value) throw new Error(message); };
  let instanceId, stage = "seeding real retained shell marks";
  test.on("pageerror", error => errors.push(error.message));
  const instrument = async () => {
    await test.evaluate(() => {
      const configuration = monoTerminal.scrollbar;
      const paint = configuration.render ?? monoApi.renderDefaultScrollbar;
      window.monoPaint = null;
      monoTerminal.setScrollbar({ ...configuration, tooltip: false, hideDelay: 60000, render(frame) {
        const strokes = [], context = frame.context;
        const stroke = context.stroke, strokeRect = context.strokeRect;
        context.stroke = function (...args) {
          strokes.push({ operation: "stroke", color: this.strokeStyle });
          return stroke.apply(this, args);
        };
        context.strokeRect = function (...args) {
          strokes.push({ operation: "strokeRect", color: this.strokeStyle });
          return strokeRect.apply(this, args);
        };
        try {
          const result = paint(frame);
          window.monoPaint = { frame, strokes };
          return result;
        } finally {
          context.stroke = stroke;
          context.strokeRect = strokeRect;
        }
      } });
      monoTerminal.refreshScrollbar();
    });
    await test.waitForFunction(() => window.monoPaint?.frame.opacity === 1);
  };
  const keyboardFocus = async () => {
    await test.locator(".terminal-window .scrollbar-accessibility").focus();
    await test.keyboard.press("ArrowUp");
    await test.waitForFunction(() => {
      const shadow = monoTerminal.element.shadowRoot, scrollbar = shadow.querySelector(".scrollbar-accessibility");
      return shadow.activeElement === scrollbar && scrollbar.matches(":focus-visible") &&
        monoPaint?.frame.interaction.focused && !monoTerminal.viewport.pending;
    });
    const focus = await test.evaluate(() => {
      const scrollbar = monoTerminal.element.shadowRoot.querySelector(".scrollbar-accessibility");
      const style = getComputedStyle(scrollbar);
      return { width: parseFloat(style.outlineWidth), style: style.outlineStyle,
        color: monoCssPixel(style.outlineColor), active: scrollbar.dataset.pointerActive };
    });
    check(focus.width > 0 && focus.style !== "none" && focus.color[3] > 0 &&
      focus.color[0] === focus.color[1] && focus.color[1] === focus.color[2],
    `Keyboard focus is not visibly neutral: ${JSON.stringify(focus)}`);
    check(focus.active !== "true", "Pointer gesture state survived release");
    return focus;
  };
  try {
    await test.goto(`${origin}/?scene=shell&renderer=webgl2`);
    await test.waitForFunction(() => [...webTerminalViews.values()][0]?.instance.id);
    instanceId = await test.evaluate(() => [...webTerminalViews.values()][0].instance.id);
    await test.waitForFunction(() => [...webTerminalViews.values()][0]?.terminal?.peer.isPrimary &&
      /([#$%>]|[^\x00-\x7f])$/.test([...webTerminalViews.values()][0].terminal.screenText.trimEnd()));
    await test.evaluate(async () => {
      window.monoTerminal = [...webTerminalViews.values()][0].terminal;
      window.monoApi = await import("/web-terminal/index.js");
      monoTerminal.focus();
    });
    const gap = "i=1; while [ \"$i\" -le 14 ]; do printf 'MONO-SPACING-%02d\\n' \"$i\"; i=$((i+1)); done";
    await test.keyboard.type(
      `printf '\\033]133;B\\007MONO-COMMAND-LINE\\n'; ${gap}; ` +
      `printf '\\033]133;C\\007MONO-EXECUTING\\n'; ${gap}; ` +
      `true; printf '\\033]133;D;%s\\007MONO-SUCCESS\\n' $?; ${gap}; ` +
      `printf '\\033]133;D\\007MONO-UNKNOWN-STATUS\\n'; ${gap}; ` +
      "printf '\\033]133;C\\007MONO-FAILING\\n'; (exit 7); printf '\\033]133;D;%s\\007MONO-ERROR\\n' $?; " +
      "i=1; while [ \"$i\" -le 90 ]; do printf 'MONO-TAIL-%03d\\n' \"$i\"; i=$((i+1)); done; printf '__MONO_READY__\\n'");
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => monoTerminal.viewport.liveTop > 100 &&
      monoTerminal.screenText.split("\n").some(row => row.trim() === "__MONO_READY__") &&
      monoTerminal.markers.some(mark => mark.phase === "finished" && mark.exitCode === 7));
    await test.evaluate(async () => {
      const viewport = monoTerminal.viewport;
      window.monoBookmark = await monoTerminal.addMarker({
        position: { generation: viewport.generation, rowId: viewport.rowIds[4], column: 0 },
        label: "Monochrome bookmark"
      });
      const marks = monoTerminal.markers;
      window.monoKinds = {
        commandLine: marks.find(mark => mark.phase === "commandLine").id,
        executing: marks.find(mark => mark.phase === "executing").id,
        success: marks.find(mark => mark.phase === "finished" && mark.exitCode === 0).id,
        error: marks.find(mark => mark.phase === "finished" && mark.exitCode === 7).id,
        unknown: marks.find(mark => mark.phase === "finished" && mark.exitCode === null).id,
        custom: monoBookmark.id
      };
      window.monoCssPixel = color => {
        const canvas = document.createElement("canvas");
        canvas.width = canvas.height = 1;
        const context = canvas.getContext("2d");
        context.fillStyle = color;
        context.fillRect(0, 0, 1, 1);
        return [...context.getImageData(0, 0, 1, 1).data];
      };
      window.monoPixel = (x, y) => {
        const { canvas, layout } = monoPaint.frame;
        return [...canvas.getContext("2d").getImageData(
          Math.floor(x * canvas.width / layout.width), Math.floor(y * canvas.height / layout.height), 1, 1).data];
      };
      window.monoThumbPoint = () => {
        const { thumb, markers } = monoPaint.frame;
        const y = Array.from({ length: 15 }, (_, index) => thumb.top + thumb.height * (index + 1) / 16)
          .find(y => Math.abs(y - (thumb.top + thumb.height / 2)) > 5 &&
            markers.every(tick => Math.abs(y - (tick.bounds.top + tick.bounds.height / 2)) > 6));
        if (y === undefined) throw new Error("No unmarked thumb area available for real dragging");
        return { x: thumb.left + 3, y };
      };
      window.monoTickPixel = id => {
        const tick = monoPaint.frame.markers.find(tick => tick.marker.id === id);
        return monoPixel(tick.bounds.left + tick.bounds.width / 2, tick.bounds.top + tick.bounds.height / 2);
      };
      window.monoCanvasIsGray = () => {
        const canvas = monoPaint.frame.canvas;
        const pixels = canvas.getContext("2d").getImageData(0, 0, canvas.width, canvas.height).data;
        for (let index = 0; index < pixels.length; index += 4)
          if (pixels[index + 3] && (pixels[index] !== pixels[index + 1] || pixels[index + 1] !== pixels[index + 2])) return false;
        return true;
      };
    });
    const picker = test.locator(".terminal-window .view-scrollbar-fade");
    for (const theme of ["light", "dark"]) {
      await test.evaluate(theme => { document.documentElement.dataset.theme = theme; }, theme);
      for (const painter of ["default", "styled", "custom", "drawn"]) {
        stage = `${theme}/${painter}: neutral thumb and phase-specific marker pixels`;
        await picker.selectOption(painter);
        await instrument();
        await test.evaluate(() => monoTerminal.scrollToLive());
        await test.waitForFunction(() => !monoTerminal.viewport.pending && monoTerminal.viewport.following);
        const pixels = await test.evaluate(() => {
          const { thumb, markers, colors } = monoPaint.frame;
          const point = monoThumbPoint();
          return {
            thumb: monoPixel(thumb.left + thumb.width / 2, point.y),
            kinds: Object.fromEntries(Object.entries(monoKinds).map(([kind, id]) => [kind, monoTickPixel(id)])),
            resolved: Object.fromEntries(Object.entries(monoKinds).map(([kind, id]) =>
              [kind, markers.find(tick => tick.marker.id === id).color])),
            colors: Object.fromEntries(Object.entries(colors).map(([name, color]) => [name, monoCssPixel(color)])),
            entireCanvasGray: monoCanvasIsGray()
          };
        });
        for (const [kind, rgba] of Object.entries({ thumb: pixels.thumb, ...pixels.kinds, ...pixels.colors }))
          check(rgba[3] > 0 && rgba[0] === rgba[1] && rgba[1] === rgba[2],
            `${theme}/${painter} ${kind} is not neutral RGBA: ${rgba}`);
        check(pixels.entireCanvasGray, `${theme}/${painter} canvas inherited a colored accent or danger token`);
        check(Object.values(pixels.resolved).every(Boolean), "Custom painters did not receive resolved tick colors");
        check(new Set(Object.values(pixels.kinds).map(rgba => rgba.slice(0, 3).join(","))).size === 6,
          `${theme}/${painter} marker kinds/outcomes are not visually distinct: ${JSON.stringify(pixels.kinds)}`);

        stage = `${theme}/${painter}: real thumb drag suppresses canvas and DOM outlines`;
        await keyboardFocus();
        const drag = await test.evaluate(() => {
          const point = monoThumbPoint(), bounds = monoTerminal.element.getBoundingClientRect(), layout = monoTerminal.layout;
          return { x: bounds.left + point.x * bounds.width / layout.width,
            y: bounds.top + point.y * bounds.height / layout.height };
        });
        await test.mouse.move(drag.x, drag.y);
        await test.mouse.down();
        await test.mouse.move(drag.x, drag.y - 60, { steps: 5 });
        await test.waitForFunction(() => monoPaint?.frame.interaction.dragging && !monoTerminal.viewport.pending);
        const dragging = await test.evaluate(() => {
          const scrollbar = monoTerminal.element.shadowRoot.querySelector(".scrollbar-accessibility");
          const style = getComputedStyle(scrollbar);
          return { active: scrollbar.dataset.pointerActive, strokes: monoPaint.strokes,
            outline: style.outlineStyle, outlineWidth: parseFloat(style.outlineWidth), gray: monoCanvasIsGray() };
        });
        check(dragging.active === "true", "Real scrollbar drag did not set data-pointer-active");
        check(dragging.strokes.length === 0, `Painter stroked an outline while dragging: ${JSON.stringify(dragging.strokes)}`);
        check(dragging.outline === "none" || dragging.outlineWidth === 0, "DOM focus-visible outline remained during pointer drag");
        check(dragging.gray, "Dragging introduced a colored scrollbar pixel");
        let screenshot;
        if (painter === "default" || painter === "drawn") {
          screenshot = `.playwright-cli/scrollbar-monochrome-drag-${theme}-${painter}.png`;
          await test.screenshot({ path: screenshot });
        }
        await test.mouse.up();
        await test.waitForFunction(() => !monoPaint.frame.interaction.dragging &&
          monoTerminal.element.shadowRoot.querySelector(".scrollbar-accessibility").dataset.pointerActive === "false");
        const focus = await keyboardFocus();
        const top = await test.evaluate(() => monoTerminal.viewport.top);
        check(top > 0, "Drag did not leave scrollback available for keyboard navigation");
        await test.keyboard.press("ArrowUp");
        await test.waitForFunction(top => !monoTerminal.viewport.pending && monoTerminal.viewport.top < top, top);
        reports.push({ theme, painter, pixels, dragging, focus, screenshot });
      }
    }

    stage = "explicit CSS, renderer factory, and per-marker colors remain supported";
    await picker.selectOption("default");
    await test.evaluate(() => {
      monoTerminal.element.style.setProperty("--cp-scrollbar-thumb", "#336699");
      monoTerminal.element.style.setProperty("--cp-scrollbar-executing", "#228844");
    });
    await instrument();
    const css = await test.evaluate(() => {
      const { thumb } = monoPaint.frame;
      return { thumb: monoPixel(thumb.left + thumb.width / 2, monoThumbPoint().y),
        executing: monoTickPixel(monoKinds.executing) };
    });
    check(css.thumb.slice(0, 3).join(",") === "51,102,153" &&
      css.executing.slice(0, 3).join(",") === "34,136,68", `Explicit CSS colors were ignored: ${JSON.stringify(css)}`);
    await test.evaluate(async () => {
      monoTerminal.element.style.removeProperty("--cp-scrollbar-thumb");
      monoTerminal.element.style.removeProperty("--cp-scrollbar-executing");
      window.monoExplicit = await monoTerminal.addMarker({
        position: { generation: monoTerminal.viewport.generation, rowId: monoTerminal.viewport.rowIds[7], column: 0 },
        label: "Explicit colored bookmark", color: "#2244cc"
      });
      monoTerminal.setScrollbar({ render: monoApi.createDefaultScrollbarRenderer({
        track: { opacity: 0 }, thumb: { color: "#336699" },
        markers: { color: "#228844", errorColor: "#aa2244" }
      }) });
    });
    await instrument();
    const explicit = await test.evaluate(() => ({
      marker: monoTickPixel(monoExplicit.id), executing: monoTickPixel(monoKinds.executing),
      error: monoTickPixel(monoKinds.error),
      thumb: monoPixel(monoPaint.frame.thumb.left + monoPaint.frame.thumb.width / 2, monoThumbPoint().y)
    }));
    check(explicit.marker.slice(0, 3).join(",") === "34,68,204", "Per-marker color lost precedence over factory overrides");
    check(explicit.executing.slice(0, 3).join(",") === "34,136,68" &&
      explicit.error.slice(0, 3).join(",") === "170,34,68" &&
      explicit.thumb.slice(0, 3).join(",") === "51,102,153", `Factory colors were ignored: ${JSON.stringify(explicit)}`);
    check(errors.length === 0, errors.join("; "));
    return { passed: true, reports, css, explicit, errors };
  } catch (error) {
    throw new Error(`${stage}: ${error.message}; errors=${JSON.stringify(errors)}`);
  } finally {
    await test.mouse.up().catch(() => {});
    try {
      if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
