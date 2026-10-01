async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0];
  if (!origin) throw new Error("Open the running WebTerminalDemo before executing this fixture");
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1000 } });
  const test = await context.newPage();
  const errors = [];
  const check = (value, message) => { if (!value) throw new Error(message); };
  let instanceId, stage = "creating marked history from the scene selector";
  test.on("pageerror", error => errors.push(error.message));
  try {
    await test.goto(`${origin}/?empty=1&renderer=webgl2`);
    await test.locator("#scene").selectOption("marks");
    const created = test.waitForResponse(response => response.url() === `${origin}/api/terminals` &&
      response.request().method() === "POST");
    await test.locator("#create").click();
    const response = await created;
    check(response.status() === 201, "Marks scene was rejected");
    instanceId = (await response.json()).id;
    await test.waitForFunction(() => [...webTerminalViews.values()][0]?.terminal?.markers.length === 48 &&
      [...webTerminalViews.values()][0].terminal.screenText.includes("MARKS_READY"), null, { timeout: 60000 });
    for (const id of ["pause", "apply-rate", "rate", "batch"])
      check(await test.locator(`#${id}`).isDisabled(), `Static scene exposes meaningless ${id} control`);
    const conflict = await test.request.post(`${origin}/api/terminals/${instanceId}/controls`,
      { headers: { Origin: origin }, data: { paused: true } });
    check(conflict.status() === 409, "Static scene accepted rate/pause controls");
    await test.locator("#close-terminal-controls").click();
    await test.evaluate(async () => {
      window.populatedTerminal = [...webTerminalViews.values()][0].terminal;
      const { renderDefaultScrollbar } = await import("/web-terminal-test/scrollbar.js");
      populatedTerminal.setScrollbar({ placement: "beside", render(frame) {
        window.populatedFrame = frame;
        renderDefaultScrollbar(frame);
      } });
    });
    await test.waitForFunction(() => window.populatedFrame?.markers.length === 36);

    stage = "verifying real tokens and full-track distribution";
    const result = await test.evaluate(async () => ({
      totalRows: populatedTerminal.viewport.totalRows,
      marks: populatedTerminal.markers.map(({ id, row, phase, column, exitCode }) => ({ id, row, phase, column, exitCode })),
      ticks: populatedFrame.markers.map(({ marker, bounds }) => ({ row: marker.row, ...bounds })),
      track: populatedFrame.track,
      details: await Promise.all([2, 26, 46].map(index =>
        populatedTerminal.getCommandMarkDetails(populatedTerminal.markers[index].id)))
    }));
    check(result.totalRows >= 20000 && result.totalRows < 25000, `Unexpected history size: ${result.totalRows}`);
    let row = 4;
    const phases = ["prompt", "commandLine", "executing", "finished"];
    for (let command = 0; command < 12; command++) {
      const lines = [0, 120, 960, 5580][command % 4], rows = [row, row, row + 1, row + 1 + lines];
      for (let phase = 0; phase < 4; phase++) {
        const mark = result.marks[command * 4 + phase];
        check(mark.id === `command:${command * 4 + phase + 1}` && mark.row === rows[phase] &&
          mark.phase === phases[phase] && mark.column === (phase === 1 ? 2 : 0),
        `Wrong emitted mark at command ${command + 1}, phase ${phase}: ${JSON.stringify(mark)}`);
      }
      check(result.marks[command * 4 + 3].exitCode === ((command + 1) % 5 === 0 ? 1 : 0),
        "Failure marks lost their exit status");
      row += lines + 2;
    }
    for (const [index, command] of [1, 7, 12].entries())
      check(result.details[index]?.rawParameters === `cmdline_url=${encodeURIComponent(`demo-check --case ${String(command).padStart(4, "0")}`)}`,
        "Command details are missing or belong to another mark");
    for (const tick of result.ticks)
      check(tick.width === tick.height && Math.abs(tick.top - (result.track.top +
        (result.track.height - tick.height) * tick.row / (result.totalRows - 1))) < 1e-6,
        "Circle position does not match its retained history row");
    check(result.ticks[0].top < result.track.top + result.track.height * .01 &&
      result.ticks.at(-1).top > result.track.top + result.track.height * .98,
      "Marks have bunched up instead of spanning the track");

    stage = "navigating to old and middle commands";
    await test.evaluate(() => populatedTerminal.focus());
    for (const command of [1, 7, 12]) {
      await test.evaluate(async command => {
        await populatedTerminal.scrollToMarker(`command:${(command - 1) * 4 + 1}`);
      }, command);
      await test.waitForFunction(command => !populatedTerminal.viewport.pending &&
        populatedTerminal.screenText.split("\n")[0].trim() === `> demo-check --case ${String(command).padStart(4, "0")}`,
      command);
    }

    stage = "late attachment and stable resize";
    const scrolledTop = await test.evaluate(() => populatedTerminal.viewport.top);
    await test.locator("#toggle-terminal-controls").click();
    await test.waitForFunction(() => getComputedStyle(document.querySelector("#terminal-controls")).transform === "matrix(1, 0, 0, 1, 0, 0)");
    check(await test.evaluate(() => populatedTerminal.viewport.top) === scrolledTop,
      "Opening controls changed the scrolled viewport");
    await test.locator("#attach").click();
    await test.waitForFunction(() => [...webTerminalViews.values()].length === 2 &&
      [...webTerminalViews.values()].every(view => view.terminal?.markers.length === 48), null, { timeout: 60000 });
    check(await test.evaluate(() => JSON.stringify([...webTerminalViews.values()][0].terminal.markers) ===
      JSON.stringify([...webTerminalViews.values()][1].terminal.markers)), "Late attachment lost retained history marks");
    await test.locator("#close-terminal-controls").click();
    await test.evaluate(() => {
      window.originalMarks = JSON.stringify(populatedTerminal.markers);
      populatedTerminal.setSizing({ mode: "fixed", columns: 90, rows: 24 });
    });
    await test.waitForFunction(() => populatedTerminal.geometry.columns === 90 &&
      populatedTerminal.geometry.rows === 24 && !populatedTerminal.viewport.pending);
    await test.evaluate(() => populatedTerminal.focus());
    await test.keyboard.type("ignored input");
    await test.keyboard.press("Enter");
    // A deliberate quiet interval catches accidentally restarting the periodic dashboard after resize/input.
    await test.waitForTimeout(300);
    check(await test.evaluate(() => JSON.stringify(populatedTerminal.markers) === originalMarks &&
      populatedTerminal.connected), "Resize or input regenerated the fixed history");
    check(errors.length === 0, errors.join("; "));
    return { passed: true, commands: 12, marks: result.marks.length, paintedMarks: result.ticks.length,
      totalRows: result.totalRows, errors };
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
