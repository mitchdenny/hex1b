async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0];
  if (!origin) throw new Error("Open the running WebTerminalDemo before executing this fixture");
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1100 } });
  const test = await context.newPage();
  const errors = [], sockets = [], input = [];
  const check = (value, message) => { if (!value) throw new Error(message); };
  let instanceId, stage = "mounting";
  test.on("pageerror", error => errors.push(error.message));
  test.on("websocket", socket => {
    sockets.push(socket.url());
    socket.on("framesent", ({ payload }) => {
      if (typeof payload !== "string") return;
      const frame = JSON.parse(payload);
      if (["input", "key", "mouse", "paste"].includes(frame.type)) input.push(frame);
    });
  });
  const verifyMode = async mode => {
    await test.waitForFunction(mode =>
      document.documentElement.dataset.theme === mode &&
      [...webTerminalViews.values()].every(view => view.terminal?.resolvedColorMode === mode), mode);
  };
  const pixels = async (ansi, background, viewIndex = 0, green) => {
    // Read an actual compositor screenshot: the production canvas belongs to a worker.
    const png = await test.locator(".terminal-window").nth(viewIndex)
      .locator("canvas:not(.scrollbar-canvas)").first().screenshot();
    return test.evaluate(async ({ png, ansi, background, green }) => {
      const bitmap = await createImageBitmap(new Blob([new Uint8Array(png)], { type: "image/png" }));
      const canvas = document.createElement("canvas");
      canvas.width = bitmap.width;
      canvas.height = bitmap.height;
      const context = canvas.getContext("2d");
      context.drawImage(bitmap, 0, 0);
      bitmap.close();
      const bytes = context.getImageData(0, 0, canvas.width, canvas.height).data;
      const count = hex => {
        const rgb = [1, 3, 5].map(offset => parseInt(hex.slice(offset, offset + 2), 16));
        let found = 0;
        for (let index = 0; index < bytes.length; index += 4)
          if (rgb.every((channel, offset) => bytes[index + offset] === channel)) found++;
        return found;
      };
      return { ansi: count(ansi), green: green ? count(green) : null,
        truecolor: count("#123abc"), background: count(background) };
    }, { png: [...png], ansi, background, green });
  };
  const verifyPixels = async (ansi, background, viewIndex = 0, green) => {
    let result;
    for (let attempt = 0; attempt < 30; attempt++) {
      result = await pixels(ansi, background, viewIndex, green);
      if (result.ansi > 20 && result.truecolor > 20 && result.background > 20 &&
          (green === undefined || result.green > 20)) return result;
      await test.waitForTimeout(100);
    }
    throw new Error(`Palette ${ansi}/${background} did not reach worker pixels: ${JSON.stringify(result)}`);
  };
  try {
    await test.goto(`${origin}/?scene=shell&renderer=webgl2&scoutTheme=dark`);
    await test.waitForFunction(() => [...webTerminalViews.values()][0]?.instance.id);
    instanceId = await test.evaluate(() => [...webTerminalViews.values()][0].instance.id);
    await test.waitForFunction(() => [...webTerminalViews.values()][0]?.terminal?.peer.isPrimary &&
      /[\u276f$#%>]$/.test([...webTerminalViews.values()][0].terminal.screenText.trimEnd()));
    check(await test.locator("#color-mode").isVisible() &&
      await test.locator("#light-palette").isVisible() && await test.locator("#dark-palette").isVisible(),
    "Palette controls are not discoverable while terminal creation controls are collapsed");
    const selectionContrast = await test.evaluate(async () => {
      const { palettePresets } = await import("/playground/palettes.js");
      const luminance = hex => hex.slice(1).match(/../gu)
        .map(value => parseInt(value, 16) / 255)
        .map(value => value <= .04045 ? value / 12.92 : ((value + .055) / 1.055) ** 2.4)
        .reduce((sum, value, i) => sum + value * [.2126, .7152, .0722][i], 0);
      return Object.entries(palettePresets).map(([name, { palette }]) => {
        const a = luminance(palette.selectionForeground ?? palette.background);
        const b = luminance(palette.selectionBackground ?? palette.foreground);
        return { name, contrast: (Math.max(a, b) + .05) / (Math.min(a, b) + .05) };
      });
    });
    check(selectionContrast.every(item => item.contrast >= 4.5),
      `A comparison palette has unreadable selected text: ${JSON.stringify(selectionContrast)}`);
    await test.evaluate(() => {
      window.paletteOriginalView = [...webTerminalViews.values()][0];
      window.paletteOriginalTerminal = paletteOriginalView.terminal;
      window.paletteOriginalConnection = paletteOriginalView.connectionId;
      paletteOriginalTerminal.focus();
    });
    // Seed once; switching appearance must never send these (or any other) inputs.
    await test.keyboard.type("printf '\\033[0m\\nDEFAULT PALETTE\\n\\033[41m ANSI RED         \\033[0m\\n\\033[42m ANSI GREEN       \\033[0m\\n\\033[48;2;18;58;188m TRUECOLOR        \\033[0m\\n__PALETTE_READY__\\n'");
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => paletteOriginalTerminal.screenText.split("\n")
      .some(row => row.trim() === "__PALETTE_READY__") &&
      /[\u276f$#%>]$/.test(paletteOriginalTerminal.screenText.trimEnd()));
    const before = { input: input.length, sockets: sockets.length };
    await test.evaluate(() => {
      window.paletteGeometry = JSON.stringify(paletteOriginalTerminal.geometry);
      window.paletteText = paletteOriginalTerminal.screenText;
    });
    stage = "Hex1b defaults and real ANSI/truecolor pixels";
    check(await test.locator("#dark-palette").inputValue() === "default-dark" &&
      await test.locator("#light-palette").inputValue() === "default-light", "Hex1b palettes are not the demo defaults");
    await verifyPixels("#ed888f", "#323232", 0, "#8dae82");
    await test.locator("#color-mode").selectOption("light");
    await verifyMode("light");
    await verifyPixels("#9e3141", "#d4d0c8", 0, "#3e6131");
    await test.locator("#color-mode").selectOption("dark");
    await verifyMode("dark");
    stage = "Ghostty SGR42 green and real ANSI/truecolor pixels";
    await test.locator("#dark-palette").selectOption("ghostty-dark");
    const ghostty = await verifyPixels("#cc6666", "#282c34", 0, "#b5bd68");
    stage = "dark preset and real ANSI/truecolor pixels";
    await test.locator("#dark-palette").selectOption("campbell-dark");
    await verifyMode("dark");
    const campbell = await verifyPixels("#c50f1f", "#0c0c0c");
    stage = "independent light choice";
    await test.locator("#light-palette").selectOption("fluent-light");
    await verifyPixels("#c50f1f", "#0c0c0c");
    await test.locator("#color-mode").selectOption("light");
    await verifyMode("light");
    const light = await verifyPixels("#b10e1c", "#ffffff");
    await test.locator("#dark-palette").selectOption("fluent-dark");
    await verifyPixels("#b10e1c", "#ffffff");
    await test.locator("#color-mode").selectOption("dark");
    await verifyMode("dark");
    const dark = await verifyPixels("#f1707b", "#1f1f1f");
    check(await test.locator("#palette-swatches .palette-swatch").count() === 16, "Missing ANSI swatches");
    check(await test.evaluate(() =>
      paletteOriginalView.terminal === paletteOriginalTerminal &&
      paletteOriginalView.connectionId === paletteOriginalConnection &&
      paletteOriginalTerminal.connected &&
      JSON.stringify(paletteOriginalTerminal.geometry) === paletteGeometry &&
      paletteOriginalTerminal.screenText === paletteText),
    "Appearance changes replaced the terminal, geometry, or retained text");
    check(input.length === before.input && sockets.length === before.sockets,
      "Appearance changes sent terminal input or reconnected a session");

    stage = "subsequent mounts and system preference";
    await test.locator("#terminal-controls > summary").click();
    await test.locator("#attach").click();
    await test.waitForFunction(() => webTerminalViews.size === 2 &&
      [...webTerminalViews.values()].every(view => view.phase === "connected"));
    await verifyMode("dark");
    check(await test.evaluate(() => [...webTerminalViews.values()]
      .every(view => view.terminal.colorMode === "dark")), "New mount lost the selected mode");
    await verifyPixels("#f1707b", "#1f1f1f", 1);
    const afterAttach = { input: input.length, sockets: sockets.length };
    await test.emulateMedia({ colorScheme: "light" });
    await test.locator("#color-mode").selectOption("system");
    await verifyMode("light");
    await verifyPixels("#b10e1c", "#ffffff");
    await verifyPixels("#b10e1c", "#ffffff", 1);
    await test.emulateMedia({ colorScheme: "dark" });
    await verifyMode("dark");
    await verifyPixels("#f1707b", "#1f1f1f");
    await verifyPixels("#f1707b", "#1f1f1f", 1);
    check(await test.evaluate(() => [...webTerminalViews.values()]
      .every(view => view.terminal.colorMode === "system")), "System mode did not update every current mount");
    check(input.length === afterAttach.input && sockets.length === afterAttach.sockets,
      "System appearance changes sent terminal input or opened a socket");

    stage = "retaining original ANSI/truecolor rows in scrollback";
    await test.locator(".terminal-window").first().locator(".view-titlebar")
      .click({ position: { x: 10, y: 10 } });
    await test.evaluate(() => paletteOriginalTerminal.focus());
    await test.keyboard.type("i=1; while [ \"$i\" -le 100 ]; do printf 'PALETTE-HISTORY-TAIL-%03d\\n' \"$i\"; i=$((i+1)); done; printf '__PALETTE_HISTORY_READY__\\n'");
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => paletteOriginalTerminal.viewport.liveTop > 50 &&
      paletteOriginalTerminal.screenText.split("\n").some(row => row.trim() === "__PALETTE_HISTORY_READY__") &&
      /[\u276f$#%>]$/.test(paletteOriginalTerminal.screenText.trimEnd()));
    await test.evaluate(() => paletteOriginalTerminal.scrollToRow(0));
    await test.waitForFunction(() => !paletteOriginalTerminal.viewport.pending &&
      paletteOriginalTerminal.viewport.top === 0 &&
      paletteOriginalTerminal.screenText.includes("__PALETTE_READY__") &&
      paletteOriginalTerminal.screenText.includes("ANSI GREEN"));
    await test.evaluate(() => {
      window.paletteHistoryText = paletteOriginalTerminal.screenText;
      window.paletteHistoryRows = JSON.stringify(paletteOriginalTerminal.viewport.rowIds);
      window.paletteHistoryTop = paletteOriginalTerminal.viewport.top;
    });
    const beforeHistory = { input: input.length, sockets: sockets.length };
    stage = "recoloring retained history without text loss";
    await test.locator("#color-mode").selectOption("dark");
    await test.locator("#dark-palette").selectOption("ghostty-dark");
    await verifyMode("dark");
    const ghosttyHistory = await verifyPixels("#cc6666", "#282c34", 0, "#b5bd68");
    await test.locator("#color-mode").selectOption("light");
    await verifyMode("light");
    const lightHistory = await verifyPixels("#b10e1c", "#ffffff", 0, "#107c10");
    check(await test.evaluate(() =>
      paletteOriginalView.terminal === paletteOriginalTerminal &&
      paletteOriginalView.connectionId === paletteOriginalConnection &&
      paletteOriginalTerminal.screenText === paletteHistoryText &&
      JSON.stringify(paletteOriginalTerminal.viewport.rowIds) === paletteHistoryRows &&
      paletteOriginalTerminal.viewport.top === paletteHistoryTop &&
      paletteOriginalTerminal.viewport.top < paletteOriginalTerminal.viewport.liveTop &&
      !paletteOriginalTerminal.viewport.following),
    "Recoloring history lost retained text/rows, changed viewport, or remounted the terminal");
    check(input.length === beforeHistory.input && sockets.length === beforeHistory.sockets,
      "History palette changes sent terminal input or reconnected a session");
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, ghostty, campbell, light, dark, ghosttyHistory, lightHistory,
      modeSelectors: true, truecolorUnchanged: true, retainedHistoryUnchanged: true, errors };
  } catch (error) {
    throw new Error(`${stage}: ${error.message}`);
  } finally {
    try {
      if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
