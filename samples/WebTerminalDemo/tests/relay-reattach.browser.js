async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0];
  if (!origin) throw new Error("Open the running WebTerminalDemo before executing this fixture");
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1000 } });
  const test = await context.newPage();
  const errors = [], snapshots = [];
  const commands = [
    "echo 'Building project...' && sleep 0.3 && echo 'Build succeeded.'",
    "ls /no/such/path",
    "echo 'Deploying dist/...' && sleep 0.3 && echo 'Deploy complete.'"
  ];
  const check = (value, message) => { if (!value) throw new Error(message); };
  let instanceId, createdAt, stage = "creating the sole interactive HMP1 view";
  test.on("pageerror", error => errors.push(error.message));
  const view = () => test.locator(".terminal-window").first();
  const bindView = async (requirePrimary = false) => {
    await test.waitForFunction(() => webTerminalViews.size === 1 &&
      [...webTerminalViews.values()][0].phase === "connected" &&
      [...webTerminalViews.values()][0].terminal?.viewport.available);
    await test.evaluate(() => { window.reattachTerminal = [...webTerminalViews.values()][0].terminal; });
    await test.waitForFunction(() => !reattachTerminal.viewport.pending);
    if (requirePrimary) await test.waitForFunction(() => reattachTerminal.peer.isPrimary);
    check(await view().getAttribute("data-transport") === "hmp1", "View bypassed HMP1 relay transport");
  };
  const historySnapshot = async cycle => {
    await test.evaluate(() => reattachTerminal.scrollToRow(0));
    await test.waitForFunction(() => !reattachTerminal.viewport.pending && reattachTerminal.viewport.top === 0);
    const snapshot = await test.evaluate(cycle => ({
      cycle, columns: reattachTerminal.geometry.columns, rows: reattachTerminal.geometry.rows,
      primary: reattachTerminal.peer.isPrimary,
      liveTop: reattachTerminal.viewport.liveTop, totalRows: reattachTerminal.viewport.totalRows,
      historyText: reattachTerminal.screenText,
      markers: reattachTerminal.markers.map(mark => ({ phase: mark.phase, row: mark.row, exitCode: mark.exitCode }))
    }), cycle);
    snapshots.push(snapshot);
    return snapshot;
  };
  const inspectCommands = async () => {
    await test.waitForFunction(() =>
      reattachTerminal.markers.filter(mark => mark.source === "command" && mark.phase === "executing").length === 3,
    null, { timeout: 10000 });
    const details = await test.evaluate(async () => Promise.all(reattachTerminal.markers
      .filter(mark => mark.source === "command")
      .map(async mark => ({ id: mark.id, row: mark.row, ...await reattachTerminal.getCommandMarkDetails(mark.id) }))));
    const executing = details.filter(mark => mark.phase === "executing").sort((a, b) => a.row - b.row);
    check(executing.length === 3, "Missing retained command examples");
    for (const [index, mark] of executing.entries()) {
      const encoded = mark.rawParameters?.match(/^cmdline_url=(.*)$/)?.[1];
      check(encoded && decodeURIComponent(encoded) === commands[index],
        `Command details changed after relay attachment: ${JSON.stringify(mark)}`);
    }
    const finished = details.filter(mark => mark.phase === "finished").sort((a, b) => a.row - b.row);
    check(finished.length === 3 && finished[0].exitCode === 0 && finished[1].exitCode > 0 &&
      finished[2].exitCode === 0, `Missing real exit statuses: ${JSON.stringify(finished)}`);

    for (const [index, mark] of executing.entries()) {
      const menu = view().locator(".view-marker-details");
      await menu.locator("summary").click();
      const jump = view().locator(`.marker-jump[data-marker="${mark.id}"]`);
      check(await jump.isEnabled(), "Retained command marker is not navigable");
      await jump.click();
      await test.waitForFunction(id => {
        const marker = reattachTerminal.markers.find(mark => mark.id === id);
        return marker && !reattachTerminal.viewport.pending &&
          reattachTerminal.viewport.top === Math.min(marker.row, reattachTerminal.viewport.liveTop);
      }, mark.id);
      await menu.locator("summary").press("Escape");
      const output = ["Building project...", "ls:", "Deploying dist/..."][index];
      check((await test.evaluate(() => reattachTerminal.screenText)).includes(output),
        `Marker jump did not reveal command output: ${output}`);
      await test.mouse.move(1, 1);
      const point = await test.evaluate(id => {
        const terminal = reattachTerminal, marker = terminal.markers.find(mark => mark.id === id);
        const layout = terminal.layout, track = layout.scrollbar, bounds = terminal.element.getBoundingClientRect();
        return {
          // Window resize handles overlap the outer edge of the scrollbar.
          x: bounds.left + (track.left + 1) * bounds.width / layout.width,
          y: bounds.top + (track.top + (track.height - 3) * marker.row /
            Math.max(1, terminal.viewport.totalRows - 1) + 1.5) * bounds.height / layout.height
        };
      }, mark.id);
      await test.mouse.move(point.x, point.y);
      await test.waitForFunction(command =>
        reattachTerminal.element.querySelector(".hex1b-scrollbar-tooltip-overlay")?.textContent
          .split("\n").includes(command), commands[index]);
    }
    await test.mouse.move(1, 1);
    await test.evaluate(() => reattachTerminal.scrollToLive());
    await test.waitForFunction(() => reattachTerminal.viewport.following && !reattachTerminal.viewport.pending);
  };
  try {
    await test.goto(`${origin}/?empty=1`);
    await test.locator("#scene").selectOption("shell");
    await test.locator("#transport").selectOption("hmp1");
    await test.locator("#renderer").selectOption("webgl2");
    const created = test.waitForResponse(response => response.url() === `${origin}/api/terminals` &&
      response.request().method() === "POST");
    await test.locator("#create").click();
    const instance = await (await created).json();
    instanceId = instance.id;
    createdAt = instance.createdAt;
    await bindView(true);
    await test.waitForFunction(() =>
      /([#$%>]|[^\x00-\x7f])$/.test(reattachTerminal.screenText.trimEnd()));

    stage = "populating history and markers with the actual shell integration tape";
    await test.locator("#tapes").selectOption("shell-integration");
    const started = test.waitForResponse(response => response.url().endsWith("/tape") &&
      response.request().method() === "POST");
    await test.locator("#play-tape").click();
    check((await started).status() === 202, "Shell integration tape did not start");
    await test.waitForFunction(() => ["completed", "failed"].includes(
      document.querySelector("#tape-status").dataset.state), null, { timeout: 120000 });
    check(await test.locator("#tape-status").getAttribute("data-state") === "completed",
      await test.locator("#tape-status").innerText());
    await test.waitForFunction(() => reattachTerminal.viewport.liveTop > 0 &&
      reattachTerminal.screenText.includes("Finished ticks show exit status only."));
    const before = await historySnapshot(0);
    check(before.historyText.includes("Building project..."), "Baseline history has no shell tape content");
    await inspectCommands();

    for (let cycle = 1; cycle <= 2; cycle++) {
      stage = `cycle ${cycle}: closing the last primary window without ending the producer`;
      check(await test.evaluate(() => webTerminalViews.size === 1 && reattachTerminal.peer.isPrimary),
        "Regression requires the sole primary view, not an additional read-only attachment");
      await view().locator(".close-view").click();
      await test.waitForFunction(() => webTerminalViews.size === 0 &&
        document.querySelectorAll(".terminal-window").length === 0);
      await test.waitForFunction(async ({ id, createdAt }) => {
        const instances = await (await fetch("/api/terminals")).json();
        const producer = instances.find(instance => instance.id === id);
        return producer?.createdAt === createdAt && producer.peerCount === 0 &&
          [...document.querySelector("#instances").options].some(option => option.value === id);
      }, { id: instanceId, createdAt });

      stage = `cycle ${cycle}: reattaching the active producer through the UI`;
      await test.locator("#instances").selectOption(instanceId);
      await test.locator("#transport").selectOption("hmp1");
      await test.locator("#attach").click();
      await bindView();
      const after = await historySnapshot(cycle);
      check(after.columns === before.columns && after.rows === before.rows,
        `UI reattachment resized the workload: ${before.columns}x${before.rows} -> ${after.columns}x${after.rows}`);
      check(after.liveTop === before.liveTop && after.totalRows === before.totalRows,
        `Reattachment lost history rows: ${before.liveTop}/${before.totalRows} -> ${after.liveTop}/${after.totalRows}`);
      check(after.historyText === before.historyText, "Reattachment changed the first retained history page");
      stage = `cycle ${cycle}: retained command details, marker jumps and decoded hover`;
      await inspectCommands();
      if (!await test.evaluate(() => reattachTerminal.peer.isPrimary)) {
        await view().locator(".take-primary").click();
        await test.waitForFunction(() => reattachTerminal.peer.isPrimary && !reattachTerminal.viewport.pending);
      }
    }
    check(errors.length === 0, errors.join("; "));
    return { passed: true, snapshots, commands, errors };
  } catch (error) {
    const current = await test.evaluate(() => [...(window.webTerminalViews?.values() || [])].map(view => ({
      phase: view.phase, peer: view.terminal?.peer, viewport: view.terminal?.viewport,
      geometry: view.terminal?.geometry, markers: view.terminal?.markers.length,
      tooltip: view.terminal?.element.querySelector(".hex1b-scrollbar-tooltip-overlay")?.textContent,
      scrollbar: view.terminal?.scrollbar, layout: view.terminal?.layout,
      inspection: view.terminal?.element.shadowRoot.querySelector(".inspection-message")?.textContent
    })));
    throw new Error(`${stage}: ${error.message}; snapshots=${JSON.stringify(snapshots)}; current=${JSON.stringify(current)}; errors=${JSON.stringify(errors)}`);
  } finally {
    try {
      if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
