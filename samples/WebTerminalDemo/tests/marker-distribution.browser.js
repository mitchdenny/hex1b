async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0];
  if (!origin) throw new Error("Open the running WebTerminalDemo before executing this fixture");
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1000 } });
  const test = await context.newPage();
  const errors = [];
  const check = (value, message) => { if (!value) throw new Error(message); };
  let instanceId, stage = "starting a POSIX shell";
  test.on("pageerror", error => errors.push(error.message));
  try {
    await test.goto(`${origin}/?scene=shell&renderer=webgl2`);
    await test.waitForFunction(() => [...webTerminalViews.values()][0]?.terminal?.connected &&
      /([#$%>]|[^\x00-\x7f])$/.test([...webTerminalViews.values()][0].terminal.screenText.trimEnd()));
    instanceId = await test.evaluate(() => [...webTerminalViews.values()][0].instance.id);
    check(await test.evaluate(() => [...webTerminalViews.values()][0].instance.tapes.some(
      tape => tape.id === "shell-integration")), "This emitted-token fixture requires a POSIX shell");
    await test.evaluate(async () => {
      const { renderDefaultScrollbar } = await import("/web-terminal-test/scrollbar.js");
      window.distributionTerminal = [...webTerminalViews.values()][0].terminal;
      distributionTerminal.setSizing({ mode: "fixed", columns: 80, rows: 24 });
      distributionTerminal.setScrollbar({ placement: "beside", render(frame) {
        window.distributionFrame = frame;
        renderDefaultScrollbar(frame);
      } });
      distributionTerminal.focus();
    });
    await test.waitForFunction(() => distributionTerminal.geometry.columns === 80 &&
      distributionTerminal.geometry.rows === 24);
    stage = "emitting 2560 marks with uneven output";
    const script = String.raw`printf "\033c"; i=0; while [ "$i" -lt 640 ]; do printf "\033]133;A\007> \033]133;B\007in\033]133;C;cmdline_url=distribution-$i\007out\033]133;D;0\007ok\r\n"; if [ "$i" -eq 320 ]; then j=0; while [ "$j" -lt 200 ]; do printf "gap\r\n"; j=$((j+1)); done; fi; i=$((i+1)); done; j=0; while [ "$j" -lt 50 ]; do printf "tail\r\n"; j=$((j+1)); done; printf "__MARK_DISTRIBUTION_READY__\r\n"`;
    await test.keyboard.type(`sh -c '${script}'`);
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => distributionTerminal.screenText.split("\n")
      .some(line => line.trim() === "__MARK_DISTRIBUTION_READY__"));
    await test.waitForFunction(() => distributionFrame?.markers.length >= 1920);

    stage = "checking authoritative rows and painted positions";
    const result = await test.evaluate(async () => {
      const terminal = distributionTerminal;
      const execution = terminal.markers.filter(mark => mark.phase === "executing" && mark.column === 4)
        .sort((a, b) => a.row - b.row);
      const details = await Promise.all([0, 320, 639].map(async index =>
        execution[index] ? terminal.getCommandMarkDetails(execution[index].id) : null));
      const firstId = BigInt(execution[0].id.split(":")[1]) - 2n;
      const emitted = terminal.markers.filter(mark => mark.source === "command" &&
        BigInt(mark.id.split(":")[1]) >= firstId && BigInt(mark.id.split(":")[1]) < firstId + 2560n)
        .sort((a, b) => BigInt(a.id.split(":")[1]) < BigInt(b.id.split(":")[1]) ? -1 : 1);
      const frame = distributionFrame;
      return {
        details, executionCount: execution.length, totalRows: terminal.viewport.totalRows,
        emitted: emitted.map(mark => ({ row: mark.row, column: mark.column, phase: mark.phase })),
        ticks: frame.markers.filter(tick => emitted.some(mark => mark.id === tick.marker.id)).map(tick => ({
          row: tick.marker.row, top: tick.bounds.top, width: tick.bounds.width, height: tick.bounds.height
        })),
        track: frame.track
      };
    });
    check(result.executionCount === 640, `Lost execution marks: ${result.executionCount}`);
    check(result.emitted.length === 2560, `Lost retained phase marks: ${result.emitted.length}`);
    for (const [i, command] of [0, 320, 639].entries())
      check(result.details[i]?.rawParameters === `cmdline_url=distribution-${command}`, "Mark details lost their command");
    const phases = ["prompt", "commandLine", "executing", "finished"], columns = [0, 2, 4, 7];
    result.emitted.forEach((mark, index) => {
      const command = Math.floor(index / 4), phase = index % 4;
      check(mark.row === command + (command > 320 ? 200 : 0) &&
        mark.column === columns[phase] && mark.phase === phases[phase],
      `Wrong emitted position at ${index}: ${JSON.stringify(mark)}`);
    });
    check(result.ticks.length === 1920, "Rendered ticks lost non-prompt phases");
    for (const tick of result.ticks)
      check(tick.width === tick.height && Math.abs(tick.top - (result.track.top +
        (result.track.height - tick.height) * tick.row / (result.totalRows - 1))) < 1e-6,
      "Rendered circle did not match its authoritative row");
    check(result.ticks[0].top === result.track.top &&
      result.ticks.at(-1).top > result.track.top + result.track.height * 0.9,
    "Retained marks do not span the expected track extent");

    stage = "navigating to early retained output";
    await test.evaluate(async () => {
      const mark = distributionTerminal.markers.find(mark => mark.phase === "executing" && mark.row === 0);
      await distributionTerminal.scrollToMarker(mark.id);
    });
    await test.waitForFunction(() => !distributionTerminal.viewport.pending && distributionTerminal.viewport.top === 0);
    check(await test.evaluate(() => distributionTerminal.screenText.split("\n")[0].trim() === "> inoutok"),
      "The earliest mark navigated to unrelated text");
    check(errors.length === 0, errors.join("; "));
    return { passed: true, emittedMarks: result.emitted.length, paintedMarks: result.ticks.length,
      totalRows: result.totalRows };
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
