async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0];
  if (!origin) throw new Error("Navigate to the running WebTerminalDemo before invoking this fixture.");
  const context = await page.context().browser().newContext({ viewport: { width: 1600, height: 1000 } });
  const test = await context.newPage();
  const errors = [];
  const results = [];
  let instanceId;
  let stage = "starting real shell";
  test.on("pageerror", error => errors.push(error.message));
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const state = () => test.evaluate(() => ({
    text: window.reflowPrimary?.screenText, viewport: window.reflowPrimary?.viewport,
    geometry: window.reflowPrimary?.geometry, stats: window.reflowPrimary?.stats,
    secondary: window.reflowSecondary && {
      text: reflowSecondary.screenText, viewport: reflowSecondary.viewport,
      peer: reflowSecondary.peer, selection: reflowSecondary.selection
    }
  }));
  const waitLive = async () => {
    await test.evaluate(() => reflowPrimary.scrollToLive());
    await test.waitForFunction(() => !reflowPrimary.viewport.pending && reflowPrimary.viewport.followTail);
  };
  const command = async (text, marker) => {
    await test.evaluate(() => reflowPrimary.focus());
    await test.keyboard.type(text);
    await test.keyboard.press("Enter");
    await test.waitForFunction(marker => reflowPrimary.screenText.split("\n").some(row => row.trimEnd() === marker) &&
      reflowPrimary.screenText.trimEnd().endsWith("RF>"), marker, { timeout: 30000 });
  };
  const resize = async columns => {
    await test.evaluate(columns => reflowPrimary.setSizing({ mode: "fixed", columns, rows: 24 }), columns);
    await test.waitForFunction(columns => reflowPrimary.geometry.columns === columns &&
      reflowPrimary.stats.columns === columns && !reflowPrimary.viewport.pending &&
      (!window.reflowSecondary || reflowSecondary.geometry.columns === columns), columns);
  };
  const lines = [
    "__RF_BEGIN__",
    `SHORT_${"0123456789".repeat(5)}_END`,
    `WRAPPED_${"abcdefghij".repeat(11)}_END`,
    "HARD_BOUNDARY",
    `WIDE_${"界e\u0301".repeat(10)}_END`,
    "__RF_END__"
  ];
  const wrapped = (columns, source = lines) => source.flatMap(line => {
    const rows = [];
    let row = "", width = 0;
    for (const { segment } of new Intl.Segmenter("en", { granularity: "grapheme" }).segment(line)) {
      const cells = segment === "界" ? 2 : 1;
      if (width + cells > columns) { rows.push(row); row = ""; width = 0; }
      row += segment;
      width += cells;
    }
    rows.push(row);
    return rows;
  });
  const assertText = async columns => {
    const actual = (await state()).text.split("\n").map(row => row.trimEnd());
    const start = actual.indexOf("__RF_BEGIN__");
    const expected = wrapped(columns);
    check(start >= 0 && JSON.stringify(actual.slice(start, start + expected.length)) === JSON.stringify(expected),
      `Exact ${columns}-column reflow mismatch: expected ${JSON.stringify(expected)}, actual ${JSON.stringify(actual)}`);
    results.push({ columns, rows: expected.length });
  };
  const history = async () => {
    await test.evaluate(() => reflowPrimary.scrollLines(-100000));
    await test.waitForFunction(() => !reflowPrimary.viewport.pending && reflowPrimary.viewport.top === 0);
    const rows = [];
    for (let pageIndex = 0; pageIndex < 100; pageIndex++) {
      const snapshot = await state();
      for (const [index, row] of snapshot.text.split("\n").entries())
        rows[snapshot.viewport.top + index] = row.trimEnd();
      if (snapshot.viewport.top === snapshot.viewport.liveTop) return rows.slice(0, snapshot.viewport.totalRows);
      await test.evaluate(() => reflowPrimary.scrollLines(24));
      await test.waitForFunction(() => !reflowPrimary.viewport.pending);
    }
    throw new Error("History traversal exceeded 100 pages");
  };
  try {
    await test.goto(`${origin}/health`);
    const created = await test.request.post(`${origin}/api/terminals`, {
      headers: { Origin: origin }, data: { scene: "shell", columns: 80, rows: 24, name: "Reflow browser regression" }
    });
    check(created.status() === 201, `Shell creation failed: ${created.status()}`);
    instanceId = (await created.json()).id;
    await test.evaluate(async id => {
      const { WebTerminal } = await import("/web-terminal/index.js");
      const layout = document.createElement("div");
      layout.style.cssText = "display:flex;gap:20px";
      for (const name of ["primary", "secondary"]) {
        const host = document.createElement("div");
        host.id = `reflow-${name}`;
        host.style.cssText = "width:760px;height:480px;flex:none";
        layout.append(host);
      }
      document.body.replaceChildren(layout);
      window.reflowPrimary = await WebTerminal.mount(document.getElementById("reflow-primary"), {
        url: `/ws?instance=${id}&name=ReflowPrimary`,
        renderer: "webgl2", sizing: { mode: "fixed", columns: 80, rows: 24 }
      });
      reflowPrimary.requestPrimary();
    }, instanceId);
    await test.waitForFunction(() => reflowPrimary.peer.isPrimary && reflowPrimary.stats.gpu === "ready" &&
      /[$#%>]$/.test(reflowPrimary.screenText.trimEnd()), null, { timeout: 30000 });
    await command("exec /usr/bin/env -i PATH=/usr/bin:/bin TERM=xterm-256color LC_ALL=en_US.UTF-8 PS1='RF> ' /bin/bash --noprofile --norc -i", "RF>");
    await command("stty -echo; printf '\\033[2J\\033[H__RF_CLEAN__\\n'", "__RF_CLEAN__");
    await command(`printf '\\033[2J\\033[H'; printf '%s\\n' '${lines.join("' '")}'`, "__RF_END__");
    stage = "initial 80-column output";
    await assertText(80);
    stage = "secondary authority and selection";
    await test.evaluate(async id => {
      const { WebTerminal } = await import("/web-terminal/index.js");
      window.reflowSecondary = await WebTerminal.mount(document.getElementById("reflow-secondary"), {
        url: `/ws?instance=${id}&name=ReflowSecondary`, renderer: "webgl2",
        sizing: { mode: "fixed", columns: 80, rows: 24 }
      });
    }, instanceId);
    await test.waitForFunction(() => reflowSecondary.connected && reflowSecondary.stats.gpu === "ready" &&
      reflowSecondary.screenText === reflowPrimary.screenText);
    check(await test.evaluate(() => {
      try { reflowSecondary.setSizing({ mode: "fixed", columns: 40, rows: 24 }); return false; }
      catch (error) { return error.message.includes("primary"); }
    }), "Secondary was allowed to resize the producer");
    const selectionBounds = await test.evaluate(() => {
      const rect = reflowSecondary.element.shadowRoot.querySelector("canvas").getBoundingClientRect();
      return { x: rect.x, y: rect.y, width: rect.width / 80, height: rect.height / 24 };
    });
    await test.mouse.move(selectionBounds.x + selectionBounds.width * .5, selectionBounds.y + selectionBounds.height * .5);
    await test.mouse.down();
    await test.mouse.move(selectionBounds.x + selectionBounds.width * 11.5, selectionBounds.y + selectionBounds.height * .5, { steps: 4 });
    await test.mouse.up();
    await test.waitForFunction(() => reflowSecondary.selection.status === "valid");
    check(await test.evaluate(() => reflowSecondary.selection.text === "__RF_BEGIN__"), "Selection did not resolve exact producer text");
    for (const columns of [20, 40, 80, 20, 80]) {
      stage = `resizing to ${columns} columns`;
      await resize(columns);
      await assertText(columns);
      await test.waitForFunction(() => reflowSecondary.screenText === reflowPrimary.screenText);
      check(await test.evaluate(() => reflowSecondary.selection.status === "invalidated"),
        "Resize retained a stale selection");
    }
    stage = "creating scrollback";
    const historyLines = Array.from({ length: 48 }, (_, index) =>
      `H${String(index).padStart(3, "0")}_${"abcdefghij".repeat(3)}_END`);
    await command(`printf '\\r\\033[2K'; printf '%s\\n' '${historyLines.join("' '")}' '__RF_HISTORY__'`, "__RF_HISTORY__");
    const expectedHistory = [...lines, ...historyLines, "__RF_HISTORY__", "RF>"];
    for (const columns of [80, 20, 40, 80, 20, 80]) {
      stage = `checking complete ${columns}-column history`;
      await waitLive();
      await resize(columns);
      const actual = await history();
      const expected = wrapped(columns, expectedHistory);
      check(JSON.stringify(actual.slice(0, expected.length)) === JSON.stringify(expected),
        `Complete history mismatch: expected ${JSON.stringify(expected)}, actual ${JSON.stringify(actual)}`);
      check(actual.slice(expected.length).every(row => row === ""), "Unexpected text after the prompt in history");
      results.push({ columns, historyRows: expected.length });
    }
    await waitLive();
    stage = "editing a pending prompt after resizing";
    await command("stty echo; printf '\\r\\033[2K__RF_EDIT_READY__\\n'", "__RF_EDIT_READY__");
    await test.evaluate(() => reflowPrimary.focus());
    await test.keyboard.type("printf '__EDIT_OK__\\n'X");
    await test.waitForFunction(() => reflowPrimary.screenText.includes("printf '__EDIT_OK__\\n'X"));
    for (const columns of [20, 40, 80]) await resize(columns);
    await test.keyboard.press("Backspace");
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => reflowPrimary.screenText.split("\n").some(row => row.trimEnd() === "__EDIT_OK__") &&
      reflowPrimary.screenText.trimEnd().endsWith("RF>"));
    stage = "transferring primary authority";
    await test.evaluate(() => reflowSecondary.requestPrimary());
    await test.waitForFunction(() => reflowSecondary.peer.isPrimary && !reflowPrimary.peer.isPrimary);
    await test.evaluate(() => reflowSecondary.setSizing({ mode: "fixed", columns: 40, rows: 24 }));
    await test.waitForFunction(() => reflowPrimary.geometry.columns === 40 && reflowSecondary.geometry.columns === 40);
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    check(await test.evaluate(() => [reflowPrimary, reflowSecondary].every(terminal =>
      terminal.stats.gpu === "ready" && terminal.stats.renderer === "webgl2" &&
      terminal.stats.warnings.length === 0 && terminal.stats.discardedFrames === 0)), "GPU rendering diagnostics failed");
    return { passed: true, results, covered: ["exact live text", "soft and hard wraps", "wide and combining cells",
      "complete scrollback", "secondary resize rejection", "selection invalidation", "pending prompt editing",
      "primary transfer", "real shell through HMP producer and HWT GPU worker"], stats: (await state()).stats };
  } catch (error) {
    throw new Error(`${stage}: ${error.message}; ${JSON.stringify(await state())}`);
  } finally {
    try {
      if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
