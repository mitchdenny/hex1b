async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0];
  if (!origin) throw new Error("Navigate to the running WebTerminalDemo before invoking this fixture.");
  const context = await page.context().browser().newContext({ viewport: { width: 1600, height: 1100 } });
  const test = await context.newPage();
  const instances = new Set();
  const errors = [];
  const sockets = [];
  const results = [];
  const strategies = ["Default", "None", "Auto", "Alacritty", "Foot", "Ghostty", "ITerm2",
    "Kitty", "Vte", "WezTerm", "WindowsTerminal", "Xterm"];
  let stage = "API options";
  test.on("pageerror", error => errors.push(error.message));
  test.on("websocket", socket => sockets.push(socket.url()));
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const list = async () => {
    const response = await test.request.get(`${origin}/api/terminals`);
    check(response.ok(), `Listing terminals failed: ${response.status()}`);
    return response.json();
  };
  const remove = async id => {
    const response = await test.request.delete(`${origin}/api/terminals/${id}`, { headers: { Origin: origin } });
    check(response.status() === 204, `Deleting ${id} failed: ${response.status()}`);
    instances.delete(id);
  };
  const create = async data => {
    const response = await test.request.post(`${origin}/api/terminals`, {
      headers: { Origin: origin }, data: { scene: "text", columns: 80, rows: 24, ...data }
    });
    check(response.status() === 201, `Creation failed: ${response.status()} ${await response.text()}`);
    const instance = await response.json();
    instances.add(instance.id);
    return instance;
  };
  const waitViews = async (id, count) => test.waitForFunction(({ id, count }) => {
    const views = [...webTerminalViews.values()].filter(view => view.instance.id === id);
    return views.length === count && views.every(view => view.terminal?.connected && view.stats.gpu === "ready");
  }, { id, count }, { timeout: 30000 });
  const state = async () => test.evaluate(() => [...(window.webTerminalViews?.values() ?? [])].map(view => ({
    id: view.id, instance: view.instance, transport: view.transport, text: view.terminal?.screenText,
    geometry: view.terminal?.geometry, peer: view.terminal?.peer, stats: view.terminal?.stats,
    selection: view.terminal?.selection
  })));
  const resize = async columns => {
    await test.evaluate(columns => [...webTerminalViews.values()].find(view => view.terminal.peer.isPrimary)
      .terminal.setSizing({ mode: "fixed", columns, rows: 24 }), columns);
    await test.waitForFunction(columns => [...webTerminalViews.values()].every(view =>
      view.terminal.geometry.columns === columns && view.terminal.stats.columns === columns &&
      view.terminal.geometry.rows === 24), columns);
  };
  const shell = async (command, marker) => {
    await test.evaluate(() => [...webTerminalViews.values()].find(view => view.terminal.peer.isPrimary).terminal.focus());
    await test.keyboard.type(command);
    await test.keyboard.press("Enter");
    await test.waitForFunction(marker => {
      const terminal = [...webTerminalViews.values()].find(view => view.terminal.peer.isPrimary).terminal;
      return terminal.screenText.split("\n").some(row => row.trimEnd() === marker) &&
        terminal.screenText.trimEnd().endsWith("OPT>");
    }, marker, { timeout: 30000 });
  };
  const lines = ["__OPT_BEGIN__", `SHORT_${"0123456789".repeat(5)}_END`,
    `WRAPPED_${"abcdefghij".repeat(10)}_END`, `UNI_${"界e\u0301".repeat(36)}_END!`,
    "HARD_BOUNDARY", "__OPT_END__"];
  const graphemes = line => [...new Intl.Segmenter("en", { granularity: "grapheme" }).segment(line)]
    .map(item => item.segment);
  const wrap = columns => lines.flatMap(line => {
    const rows = [];
    let row = "", width = 0;
    for (const segment of graphemes(line)) {
      const cells = segment === "界" ? 2 : 1;
      if (width + cells > columns) { rows.push(row); row = ""; width = 0; }
      row += segment;
      width += cells;
    }
    rows.push(row);
    return rows;
  });
  const crop = (line, columns) => {
    let row = "", width = 0;
    for (const segment of graphemes(line)) {
      const cells = segment === "界" ? 2 : 1;
      if (width + cells > columns) break;
      row += segment;
      width += cells;
    }
    return row;
  };
  const assertLogicalSelections = async () => {
    for (const view of await state()) {
      await test.evaluate(id => webTerminalViews.get(id).terminal.focus(), view.id);
      for (const prefix of ["WRAPPED_", "UNI_"]) {
        const expected = lines.find(line => line.startsWith(prefix));
        const point = await test.evaluate(({ id, prefix }) => {
          const terminal = webTerminalViews.get(id).terminal;
          const row = terminal.screenText.split("\n").findIndex(line => line.startsWith(prefix));
          if (row < 0) throw new Error(`Missing logical-line prefix ${prefix}`);
          const rect = terminal.element.shadowRoot.querySelector("canvas").getBoundingClientRect();
          return { x: rect.x + 2.5 * rect.width / terminal.geometry.columns,
            y: rect.y + (row + .5) * rect.height / terminal.geometry.rows };
        }, { id: view.id, prefix });
        await test.mouse.click(point.x, point.y, { clickCount: 3 });
        await test.waitForFunction(id => webTerminalViews.get(id).terminal.selection.status === "valid", view.id);
        const selection = await test.evaluate(id => webTerminalViews.get(id).terminal.selection, view.id);
        check(selection.mode === "line" && selection.text?.trimEnd() === expected,
          `Logical selection ${view.transport} view ${view.id} ${prefix}: expected ${JSON.stringify(expected)} ` +
          `(${expected.length} UTF-16 units), actual ${JSON.stringify(selection.text)} (${selection.text?.trimEnd().length})`);
      }
    }
  };
  const assertRows = async expected => {
    const views = await state();
    for (const view of views) {
      const actual = view.text.split("\n").map(row => row.trimEnd());
      const start = actual.indexOf("__OPT_BEGIN__");
      check(start >= 0 && JSON.stringify(actual.slice(start, start + expected.length)) === JSON.stringify(expected),
        `View ${view.id}: expected ${JSON.stringify(expected)}, actual ${JSON.stringify(actual)}`);
    }
  };
  try {
    await test.goto(`${origin}/health`);
    for (const strategy of strategies) {
      const instance = await create({ reflowStrategy: strategy });
      const resolved = strategy === "Default" ? "None" : strategy;
      check(instance.reflowStrategy === resolved, `POST resolved ${strategy} to ${instance.reflowStrategy}`);
      check((await list()).find(item => item.id === instance.id)?.reflowStrategy === resolved,
        `GET did not preserve ${strategy}`);
      await remove(instance.id);
    }
    for (const scene of ["shell", "text"]) {
      const instance = await create({ scene });
      const resolved = scene === "shell" ? "Ghostty" : "None";
      check(instance.reflowStrategy === resolved &&
        (await list()).find(item => item.id === instance.id)?.reflowStrategy === resolved,
      `Omitted strategy resolved incorrectly for ${scene}`);
      await remove(instance.id);
    }
    const explicitDefaultShell = await create({ scene: "shell", reflowStrategy: "Default" });
    check(explicitDefaultShell.reflowStrategy === "Ghostty", "Explicit shell Default did not resolve to Ghostty");
    await remove(explicitDefaultShell.id);
    for (const invalid of ["NotAReflowStrategy", 999]) {
      const before = (await list()).map(instance => instance.id).sort();
      const response = await test.request.post(`${origin}/api/terminals`, {
        headers: { Origin: origin }, data: { scene: "text", columns: 80, rows: 24, reflowStrategy: invalid }
      });
      check(response.status() === 400, `Invalid strategy ${invalid} returned ${response.status()}`);
      check(JSON.stringify(before) === JSON.stringify((await list()).map(instance => instance.id).sort()),
        `Invalid strategy ${invalid} created a producer`);
    }
    results.push({ apiStrategies: strategies, defaults: { shell: "Ghostty", text: "None" }, invalidValuesRejected: 2 });

    stage = "query automatic creation does not attach unrelated producer";
    const unrelated = await create({ reflowStrategy: "None" });
    const automatic = test.waitForResponse(response => response.url() === `${origin}/api/terminals` &&
      response.request().method() === "POST" && response.status() === 201);
    await test.goto(`${origin}/?renderer=webgl2&reflow=Vte`);
    const automaticResponse = await automatic;
    const autoInstance = await automaticResponse.json();
    instances.add(autoInstance.id);
    check(autoInstance.id !== unrelated.id && autoInstance.reflowStrategy === "Vte" &&
      automaticResponse.request().postDataJSON().reflowStrategy === "Vte",
    "Reflow query attached an existing producer instead of creating the requested policy");
    await waitViews(autoInstance.id, 1);
    check((await state()).every(view => view.instance.id === autoInstance.id), "Automatic view attached unrelated producer");
    await test.goto(`${origin}/health`);
    await remove(autoInstance.id);
    await remove(unrelated.id);

    const scenarios = ["Vte", "None", "Ghostty"].flatMap(strategy =>
      [["direct", "direct"], ["hmp1", "hmp1"], ["direct", "hmp1"], ["hmp1", "direct"]]
        .map(([primaryTransport, secondaryTransport]) => ({ strategy, primaryTransport, secondaryTransport })));
    for (const { strategy, primaryTransport, secondaryTransport } of scenarios) {
      const scenario = `${strategy} primary=${primaryTransport} secondary=${secondaryTransport}`;
      stage = `${scenario}: actual creation controls`;
      await test.goto(`${origin}/?empty=1&scene=shell&renderer=webgl2&reflow=Vte`);
      check(await test.locator("#scene").inputValue() === "shell" &&
        await test.locator("#reflow-strategy").inputValue() === "Vte", "Query did not preselect creation controls");
      const options = await test.locator("#reflow-strategy option").evaluateAll(options => options.map(option => option.value));
      check(JSON.stringify([...options].sort()) === JSON.stringify([...strategies].sort()),
        `Incorrect strategy dropdown: ${JSON.stringify(options)}`);
      await test.locator("#reflow-strategy").selectOption(strategy);
      await test.locator("#transport").selectOption(primaryTransport);
      const created = test.waitForResponse(response => response.url() === `${origin}/api/terminals` &&
        response.request().method() === "POST" && response.status() === 201);
      await test.locator("#create").click();
      const response = await created;
      const instance = await response.json();
      instances.add(instance.id);
      check(response.request().postDataJSON().reflowStrategy === strategy && instance.reflowStrategy === strategy,
        "New terminal did not send and resolve the selected policy");
      await waitViews(instance.id, 1);
      await test.waitForFunction(() => [...webTerminalViews.values()].some(view => view.terminal.peer.isPrimary));
      check((await test.locator(`#instances option[value="${instance.id}"]`).textContent()).includes(`reflow: ${strategy}`),
        "Existing-instance picker does not display the producer policy");
      await test.locator(".view-resolution").selectOption("80x24");
      await resize(80);
      await test.waitForFunction(() => /[$#%>]$/.test([...webTerminalViews.values()][0].terminal.screenText.trimEnd()));
      await shell("exec /usr/bin/env -i PATH=/usr/bin:/bin TERM=xterm-256color LC_ALL=en_US.UTF-8 PS1='OPT> ' /bin/bash --noprofile --norc -i", "OPT>");
      await shell("stty -echo; printf '\\033[2J\\033[H__OPT_CLEAN__\\n'", "__OPT_CLEAN__");
      await shell(`printf '\\033[2J\\033[H'; printf '%s\\n' '${lines.join("' '")}'`, "__OPT_END__");
      await assertRows(wrap(80));

      stage = `${scenario}: late Attach after soft wrapping ignores changed creation selector`;
      await test.locator("#reflow-strategy").selectOption(strategy === "None" ? "Ghostty" : "None");
      await test.locator("#transport").selectOption(secondaryTransport);
      await test.locator("#instances").selectOption(instance.id);
      await test.locator("#attach").click();
      await waitViews(instance.id, 2);
      await test.waitForFunction(() => {
        const views = [...webTerminalViews.values()];
        return views[0].terminal.screenText === views[1].terminal.screenText;
      });
      check((await list()).find(item => item.id === instance.id)?.reflowStrategy === strategy &&
        (await state()).every(view => view.instance.id === instance.id && view.instance.reflowStrategy === strategy),
      "Changing creation policy mutated the producer or the attached view");
      check((await state()).filter(view => view.peer.isPrimary).length === 1, "Attach changed primary authority");
      const attached = await state();
      check(attached.find(view => view.peer.isPrimary)?.transport === primaryTransport &&
        attached.find(view => !view.peer.isPrimary)?.transport === secondaryTransport,
      "Transport picker did not apply independently to the primary and secondary views");
      for (const transport of [primaryTransport, secondaryTransport])
        check(sockets.some(url => url.includes(`instance=${instance.id}`) && url.includes(`transport=${transport}`)),
          `Missing actual ${transport} WebSocket for producer ${instance.id}`);
      await assertRows(wrap(80));
      await assertLogicalSelections();
      const cropped = wrap(80).map(row => crop(row, 20));
      for (const columns of [20, 40, 80]) {
        stage = `${scenario}: exact output after resize to ${columns}`;
        await resize(columns);
        await test.waitForFunction(() => {
          const views = [...webTerminalViews.values()];
          return views[0].terminal.screenText === views[1].terminal.screenText;
        });
        await assertRows(strategy === "None" ? cropped : wrap(columns));
        if (strategy !== "None") await assertLogicalSelections();
      }
      stage = `${scenario}: reattach retains producer policy despite new picker value`;
      const secondary = (await state()).find(view => !view.peer.isPrimary);
      await test.locator(`.terminal-window[data-view="${secondary.id}"] .close-view`).click();
      await waitViews(instance.id, 1);
      await test.locator("#reflow-strategy").selectOption("Auto");
      await test.locator("#attach").click();
      await waitViews(instance.id, 2);
      await test.waitForFunction(() => {
        const views = [...webTerminalViews.values()];
        return views[0].terminal.screenText === views[1].terminal.screenText;
      });
      check((await list()).find(item => item.id === instance.id)?.reflowStrategy === strategy &&
        (await state()).every(view => view.instance.reflowStrategy === strategy),
      "Reattachment applied the new creation policy to the existing producer");
      check((await state()).find(view => !view.peer.isPrimary)?.peer.id !== secondary.peer.id,
        "Reattachment reused the previous peer");
      await assertRows(strategy === "None" ? cropped : wrap(80));
      if (strategy !== "None") await assertLogicalSelections();
      for (const columns of [20, 80]) {
        stage = `${scenario}: reattached exact output after resize to ${columns}`;
        await resize(columns);
        await test.waitForFunction(() => {
          const views = [...webTerminalViews.values()];
          return views[0].terminal.screenText === views[1].terminal.screenText;
        });
        await assertRows(strategy === "None" ? cropped : wrap(columns));
        if (strategy !== "None") await assertLogicalSelections();
      }
      check((await state()).every(view => view.stats.gpu === "ready" && view.stats.renderer === "webgl2" &&
        view.stats.warnings.length === 0 && view.stats.discardedFrames === 0), "GPU worker diagnostics failed");
      results.push({ strategy, primaryTransport, secondaryTransport, widths: [80, 20, 40, 80, 20, 80],
        views: 2, lateAttach: true, reattached: true,
        logicalSelection: ["ASCII soft wraps", "wide/combining soft wraps with geometric padding"],
        behavior: strategy === "None" ? "crop remains after grow" : "exact text reflow preserved" });
      await test.goto(`${origin}/health`);
      await remove(instance.id);
    }
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, results, automaticQueryCreatedNewProducer: true, browserErrors: errors };
  } catch (error) {
    throw new Error(`${stage}: ${error.message}; ${JSON.stringify(await state())}`);
  } finally {
    try {
      for (const id of instances)
        await test.request.delete(`${origin}/api/terminals/${id}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
