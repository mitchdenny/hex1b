async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5291";
  const context = await page.context().browser().newContext({
    viewport: { width: 1440, height: 1100 }, deviceScaleFactor: 2
  });
  const test = await context.newPage();
  const errors = [];
  const commands = [];
  let instanceId;
  let stage = "creating shell";
  test.on("pageerror", error => errors.push(error.message));
  test.on("websocket", socket => socket.on("framesent", frame => {
    if (typeof frame.payload === "string") {
      const command = JSON.parse(frame.payload);
      if (command.type !== "ack") commands.push(command);
    }
  }));
  const check = (value, message) => { if (!value) throw new Error(message); };
  const shell = async (command, marker) => {
    await test.evaluate(() => historyPrimary.focus());
    await test.keyboard.type(command);
    await test.keyboard.press("Enter");
    await test.waitForFunction(marker => historyPrimary.screenText.split("\n").some(line => line.trim() === marker) &&
      /[\u276f$#%>]$/.test(historyPrimary.screenText.trimEnd()), marker, { timeout: 30000 });
  };
  const cell = async (name, column, row) => test.evaluate(({ name, column, row }) => {
    const terminal = window[name];
    const box = terminal.element.shadowRoot.querySelector("canvas").getBoundingClientRect();
    return { x: box.x + (column + .5) * box.width / terminal.geometry.columns,
      y: box.y + (row + .5) * box.height / terminal.geometry.rows };
  }, { name, column, row });
  const selection = async name => test.evaluate(name => window[name].selection, name);
  const settledSelection = async name => test.waitForFunction(name => {
    const value = window[name].selection;
    return value?.active && !value.pending && typeof value.text === "string";
  }, name);
  try {
    await test.goto(`${origin}/health`);
    const created = await test.request.post(`${origin}/api/terminals`, {
      headers: { Origin: origin }, data: { scene: "shell", columns: 80, rows: 24 }
    });
    check(created.status() === 201, `Shell creation failed: ${created.status()}`);
    instanceId = (await created.json()).id;
    await test.evaluate(async id => {
      const { WebTerminal } = await import("/web-terminal/index.js");
      const layout = document.createElement("div");
      layout.style.cssText = "display:flex;gap:20px;align-items:flex-start";
      const primary = document.createElement("div");
      primary.style.cssText = "width:800px;height:480px;flex:none";
      const secondary = document.createElement("div");
      secondary.id = "history-secondary";
      secondary.style.cssText = "width:400px;height:240px;flex:none";
      layout.append(primary, secondary);
      document.body.replaceChildren(layout);
      window.clipboardWrites = [];
      window.clipboardReads = 0;
      Object.defineProperty(navigator, "clipboard", { configurable: true, value: {
        readText: async () => { clipboardReads++; return clipboardWrites.at(-1) ?? ""; },
        writeText: async text => { clipboardWrites.push(text); },
        write: async items => { clipboardWrites.push(await (await items[0].getType("text/plain")).text()); }
      } });
      window.historyPrimary = await WebTerminal.mount(primary, {
        url: `/ws?instance=${id}&name=HistoryPrimary`,
        sizing: { mode: "fixed", columns: 80, rows: 24 }
      });
      historyPrimary.requestPrimary();
    }, instanceId);
    await test.waitForFunction(() => historyPrimary.peer.isPrimary && historyPrimary.geometry.columns === 80 &&
      /[\u276f$#%>]$/.test(historyPrimary.screenText.trimEnd()), null, { timeout: 30000 });
    await shell("i=1; while [ \"$i\" -le 120 ]; do printf 'ROW-%03d alpha beta gamma\\n' \"$i\"; i=$((i+1)); done; printf '__HISTORY_READY__\\n'", "__HISTORY_READY__");

    stage = "late attachment and independent history";
    await test.evaluate(async id => {
      const { WebTerminal } = await import("/web-terminal/index.js");
      window.historySecondary = await WebTerminal.mount(document.getElementById("history-secondary"), {
        url: `/ws?instance=${id}&name=HistorySecondary`
      });
      historySecondary.scrollLines(-80);
    }, instanceId);
    await test.waitForFunction(() => !historySecondary.viewport.followTail && /ROW-0[0-7]\d/.test(historySecondary.screenText));
    check(await test.evaluate(() => historyPrimary.viewport.followTail && !historySecondary.peer.isPrimary),
      "Inspecting history moved the primary viewport or claimed resize authority");
    const row = await test.evaluate(() => historySecondary.screenText.split("\n").findIndex(line => line.startsWith("ROW-")));
    let start = await cell("historySecondary", 0, row);
    let end = await cell("historySecondary", 6, row);
    await test.mouse.move(start.x, start.y);
    await test.mouse.down();
    await test.mouse.move(end.x, end.y, { steps: 5 });
    await test.mouse.up();
    await settledSelection("historySecondary");
    const retained = await selection("historySecondary");
    check(/^ROW-\d{3}$/.test(retained.text), `Character selection was not cell accurate: ${JSON.stringify(retained)}`);
    check(await test.evaluate(() => {
      const shadow = historySecondary.element.shadowRoot;
      const highlight = shadow.querySelector(".highlight");
      if (!highlight) return false;
      const bounds = highlight.getBoundingClientRect();
      const canvas = shadow.querySelector("canvas").getBoundingClientRect();
      const style = getComputedStyle(highlight);
      return bounds.width > 0 && bounds.height > 0 && bounds.left >= canvas.left &&
        bounds.right <= canvas.right + .1 && bounds.top >= canvas.top && bounds.bottom <= canvas.bottom + .1 &&
        style.backgroundColor !== "rgba(0, 0, 0, 0)" && Number(style.opacity) > 0;
    }), "Selection was not visibly highlighted when mounted without playground CSS");
    check(await test.evaluate(() => clipboardWrites.length === 0), "Selection automatically overwrote the clipboard");
    await test.keyboard.press("Meta+c");
    await test.waitForFunction(text => clipboardWrites.at(-1) === text, retained.text);
    check((await selection("historySecondary")).text === retained.text, "Copy dismissed selection");

    const anchored = await test.evaluate(() => historySecondary.screenText);
    await shell("i=1; while [ \"$i\" -le 12 ]; do printf 'NEW-%03d\\n' \"$i\"; i=$((i+1)); done; printf '__OUTPUT_CONTINUES__\\n'", "__OUTPUT_CONTINUES__");
    check(await test.evaluate(text => historySecondary.screenText === text, anchored), "New output moved the historical viewport");
    check((await selection("historySecondary")).text === retained.text, "New output changed retained selected text");

    stage = "wheel during and after drag";
    start = await cell("historySecondary", 0, 3);
    end = await cell("historySecondary", 6, 5);
    await test.mouse.move(start.x, start.y);
    await test.mouse.down();
    await test.mouse.move(end.x, end.y, { steps: 5 });
    await settledSelection("historySecondary");
    const beforeWheel = await selection("historySecondary");
    await test.mouse.wheel(0, 40);
    await test.waitForFunction(text => historySecondary.selection.active && !historySecondary.selection.pending &&
      historySecondary.selection.text.length > text.length, beforeWheel.text);
    await test.mouse.up();
    await settledSelection("historySecondary");
    const released = await selection("historySecondary");
    const beforeReleasedScroll = await test.evaluate(() => historySecondary.screenText);
    await test.mouse.wheel(0, 40);
    await test.waitForFunction(text => historySecondary.screenText !== text, beforeReleasedScroll);
    check((await selection("historySecondary")).text === released.text, "Released selection extended while scrolling");

    stage = "edge autoscroll";
    start = await cell("historySecondary", 0, 18);
    end = await cell("historySecondary", 6, 26);
    const beforeEdge = await test.evaluate(() => historySecondary.screenText);
    await test.mouse.move(start.x, start.y);
    await test.mouse.down();
    await test.mouse.move(end.x, end.y, { steps: 5 });
    await test.waitForFunction(text => historySecondary.screenText !== text &&
      historySecondary.selection.active && !historySecondary.selection.pending, beforeEdge);
    await test.mouse.up();
    await test.waitForTimeout(250);
    const stoppedEdge = await test.evaluate(() => historySecondary.screenText);
    await test.waitForTimeout(250);
    check(await test.evaluate(text => historySecondary.screenText === text, stoppedEdge),
      "Edge autoscroll continued after release");

    stage = "word, logical-line, and rectangular selection";
    const logical = `LOGICAL_${"0123456789".repeat(10)}_END`;
    await shell(`printf '\\033[2J\\033[H%s\\r\\nalpha bravo charlie\\r\\nred green blue\\r\\nWIDE A\u{1f63a}e\u0301B END\\r\\n__CONTENT_READY__\\r\\n' '${logical}'`, "__CONTENT_READY__");
    start = await cell("historyPrimary", 8, 2);
    await test.mouse.click(start.x, start.y, { clickCount: 2 });
    await test.waitForFunction(() => !historyPrimary.selection.pending && historyPrimary.selection.text === "bravo");
    end = await cell("historyPrimary", 16, 2);
    await test.keyboard.down("Shift");
    await test.mouse.click(end.x, end.y);
    await test.keyboard.up("Shift");
    await test.waitForFunction(() => !historyPrimary.selection.pending && historyPrimary.selection.text === "bravo charlie");
    check((await selection("historyPrimary")).mode === "word", "Shift-click lost word granularity");
    start = await cell("historyPrimary", 10, 1);
    await test.mouse.click(start.x, start.y, { clickCount: 3 });
    await test.waitForFunction(text => !historyPrimary.selection.pending &&
      historyPrimary.selection.text?.trimEnd() === text, logical);
    const lineSelection = await selection("historyPrimary");
    check(lineSelection.mode === "line", "Triple-click did not choose logical-line mode");
    await test.keyboard.press("Meta+c");
    await test.waitForFunction(text => clipboardWrites.at(-1)?.trimEnd() === text, logical);

    await test.mouse.click(start.x, start.y, { clickCount: 2 });
    await test.mouse.down({ clickCount: 3 });
    end = await cell("historyPrimary", 5, 2);
    await test.mouse.move(end.x, end.y, { steps: 5 });
    await test.mouse.up();
    await test.waitForFunction(text => !historyPrimary.selection.pending &&
      historyPrimary.selection.text?.trimEnd() === `${text}\nalpha bravo charlie`, logical);
    check((await selection("historyPrimary")).mode === "line", "Triple-click drag lost logical-line granularity");

    start = await cell("historyPrimary", 4, 0);
    end = await cell("historyPrimary", 9, 1);
    await test.keyboard.down("Alt");
    await test.mouse.move(start.x, start.y);
    await test.mouse.down();
    await test.mouse.move(end.x, end.y, { steps: 5 });
    await test.mouse.up();
    await test.keyboard.up("Alt");
    const rectangleText = `${logical.slice(4, 10)}\n${logical.slice(84, 90)}`;
    await test.waitForFunction(text => !historyPrimary.selection.pending && historyPrimary.selection.text === text, rectangleText);
    check((await selection("historyPrimary")).mode === "rectangle", "Alt-drag did not choose rectangular mode");
    await test.keyboard.press("Meta+c");
    await test.waitForFunction(text => clipboardWrites.at(-1) === text, rectangleText);

    stage = "wide and combining cell ownership";
    start = await cell("historyPrimary", 7, 4);
    end = await cell("historyPrimary", 8, 4);
    await test.mouse.move(start.x, start.y);
    await test.mouse.down();
    await test.mouse.move(end.x, end.y, { steps: 3 });
    await test.mouse.up();
    await test.waitForFunction(() => !historyPrimary.selection.pending &&
      historyPrimary.selection.text === "\u{1f63a}e\u0301");
    const beforeInterruptCopy = await test.evaluate(() => clipboardWrites.length);
    await test.keyboard.press("Control+c");
    await test.waitForFunction(() => !historyPrimary.selection.active);
    check(commands.some(command => command.type === "key" && command.ctrl && command.key.toLowerCase() === "c"),
      "Ctrl+C was intercepted instead of sent to the shell");
    check(await test.evaluate(count => clipboardWrites.length === count, beforeInterruptCopy), "Ctrl+C copied instead of interrupting");

    stage = "Shift mouse-capture override and gesture ownership";
    await shell("printf '\\033[?1003h\\033[?1006h__CAPTURE_READY__\\n'", "__CAPTURE_READY__");
    await test.waitForFunction(() => historyPrimary.geometry.mouseTracking === 1003);
    await test.keyboard.down("Shift");
    const applicationMouseBefore = commands.filter(command => command.type === "mouse").length;
    start = await cell("historyPrimary", 0, 2);
    end = await cell("historyPrimary", 6, 3);
    await test.mouse.move(start.x, start.y);
    await test.mouse.down();
    await test.mouse.move(end.x, end.y, { steps: 5 });
    await settledSelection("historyPrimary");
    await test.keyboard.up("Shift");
    await test.mouse.wheel(0, -40);
    await test.waitForFunction(() => !historyPrimary.viewport.followTail && !historyPrimary.selection.pending);
    await test.mouse.up();
    check(commands.filter(command => command.type === "mouse").length === applicationMouseBefore,
      "A locally owned gesture sent application mouse events after Shift was released");
    await shell("printf '\\033[?1003l\\033[?1006l__CAPTURE_OFF__\\n'", "__CAPTURE_OFF__");

    stage = "read-only history inspection";
    await test.evaluate(async id => {
      const { WebTerminal } = await import("/web-terminal/index.js");
      const element = document.createElement("div");
      element.style.cssText = "width:800px;height:360px";
      document.body.append(element);
      window.historyReadOnly = await WebTerminal.mount(element, {
        url: `/ws?instance=${id}&name=ReadOnlyHistory`, readOnly: true
      });
      historyReadOnly.scrollLines(-80);
    }, instanceId);
    await test.waitForFunction(() => !historyReadOnly.viewport.followTail && historyReadOnly.screenText.includes("ROW-"));
    const readOnlyRow = await test.evaluate(() => historyReadOnly.screenText.split("\n").findIndex(line => line.startsWith("ROW-")));
    start = await cell("historyReadOnly", 0, readOnlyRow);
    end = await cell("historyReadOnly", 6, readOnlyRow);
    await test.mouse.move(start.x, start.y);
    await test.mouse.down();
    await test.mouse.move(end.x, end.y, { steps: 4 });
    await test.mouse.up();
    await settledSelection("historyReadOnly");
    check(/^ROW-\d{3}$/.test((await selection("historyReadOnly")).text), "Read-only view could not select history");

    stage = "history eviction invalidates selection";
    await shell("i=1; while [ \"$i\" -le 1200 ]; do printf 'EVICT-%04d\\n' \"$i\"; i=$((i+1)); done; printf '__EVICTION_DONE__\\n'", "__EVICTION_DONE__");
    await test.waitForFunction(() => !historySecondary.selection.active && historySecondary.selection.status === "invalidated");
    const writes = await test.evaluate(() => clipboardWrites.length);
    check(await test.evaluate(async () => {
      try { await historySecondary.copySelection(); return false; }
      catch { return true; }
    }), "Evicted selection copied silently");
    check(await test.evaluate(count => clipboardWrites.length === count, writes), "Expired copy changed the clipboard");

    stage = "typing returns live without taking primary";
    await test.evaluate(() => historySecondary.focus());
    await test.keyboard.type("printf '__SECONDARY_INPUT__\\n'");
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => historySecondary.viewport.followTail && !historySecondary.selection.active &&
      historyPrimary.screenText.split("\n").some(line => line.trim() === "__SECONDARY_INPUT__"));
    check(await test.evaluate(() => historyPrimary.peer.isPrimary && !historySecondary.peer.isPrimary),
      "History input changed primary authority");

    stage = "Windows-style right-click copy then paste";
    await shell("printf '__RIGHT_CLICK_COPY__\\n'", "__RIGHT_CLICK_COPY__");
    await test.waitForFunction(() => historySecondary.screenText.split("\n").some(line => line.trim() === "__RIGHT_CLICK_COPY__") &&
      /[\u276f$#%>]$/.test(historySecondary.screenText.trimEnd()));
    const copiedRow = await test.evaluate(() => historySecondary.screenText.split("\n")
      .findIndex(line => line.trim() === "__RIGHT_CLICK_COPY__"));
    start = await cell("historySecondary", 0, copiedRow);
    end = await cell("historySecondary", "__RIGHT_CLICK_COPY__".length - 1, copiedRow);
    await test.mouse.move(start.x, start.y);
    await test.mouse.down();
    await test.mouse.move(end.x, end.y, { steps: 4 });
    await test.mouse.up();
    await settledSelection("historySecondary");
    check((await selection("historySecondary")).text === "__RIGHT_CLICK_COPY__", "Right-click fixture selected wrong text");
    const pasteCount = commands.filter(command => command.type === "paste").length;
    await test.mouse.click(end.x, end.y, { button: "right" });
    await test.waitForFunction(() => clipboardWrites.at(-1) === "__RIGHT_CLICK_COPY__" &&
      historySecondary.selection.status === "none" && !historySecondary.selection.copying);
    check(commands.filter(command => command.type === "paste").length === pasteCount,
      "Copying by right-click also pasted");
    check(await test.evaluate(() => clipboardReads === 0), "Copy unexpectedly read the clipboard");
    await test.mouse.click(end.x, end.y, { button: "right" });
    await test.waitForFunction(() => clipboardReads === 1 &&
      historyPrimary.screenText.trimEnd().endsWith("__RIGHT_CLICK_COPY__"));
    check(commands.filter(command => command.type === "paste").length === pasteCount + 1 &&
      commands.filter(command => command.type === "paste").at(-1).text === "__RIGHT_CLICK_COPY__",
    "Second right-click did not send exactly one authoritative paste");
    await test.keyboard.press("Control+c");
    await shell("printf '__RIGHT_CLICK_DONE__\\n'", "__RIGHT_CLICK_DONE__");
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, covered: ["late shared history", "independent viewports", "character selection",
      "copy without clipboard side effects", "output while anchored", "held and released wheel", "edge autoscroll",
      "word selection and Shift extension", "triple-click logical-line drag", "rectangular soft-wrap copy", "wide and combining cells", "Ctrl+C passthrough",
      "Shift capture override", "latched gesture ownership", "read-only selection",
      "eviction notification", "typing returns live", "right-click copy then paste through real shell"], browserErrors: errors };
  } catch (error) {
    const state = await test.evaluate(() => ["historyPrimary", "historySecondary", "historyReadOnly"].map(name => ({
      name, peer: window[name]?.peer, viewport: window[name]?.viewport, selection: window[name]?.selection,
      text: window[name]?.screenText
    })));
    throw new Error(`${stage}: ${error.message}; ${JSON.stringify({ state, commands: commands.slice(-15) })}`);
  } finally {
    try {
      if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
