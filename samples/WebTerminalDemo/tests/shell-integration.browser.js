async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1000 } });
  const test = await context.newPage();
  const errors = [];
  const commands = [
    "echo 'Building project...' && sleep 0.3 && echo 'Build succeeded.'",
    "ls /no/such/path",
    "echo 'Deploying dist/...' && sleep 0.3 && echo 'Deploy complete.'"
  ];
  const check = (value, message) => { if (!value) throw new Error(message); };
  let instanceId, stage = "shell startup";
  test.on("pageerror", error => errors.push(error.message));
  try {
    await test.goto(`${origin}/?scene=shell&renderer=webgl2`);
    await test.waitForFunction(() => [...webTerminalViews.values()][0]?.instance.id);
    instanceId = await test.evaluate(() => [...webTerminalViews.values()][0].instance.id);
    await test.waitForFunction(() => [...webTerminalViews.values()][0]?.phase === "connected" &&
      /([#$%>]|[^\x00-\x7f])$/.test([...webTerminalViews.values()][0].terminal.screenText.trimEnd()));
    check(await test.evaluate(() => [...webTerminalViews.values()][0].instance.tapes.some(
      tape => tape.id === "shell-integration")), "Shell integration tape requires a POSIX shell");
    await test.evaluate(() => {
      window.shellTapeTerminal = [...webTerminalViews.values()][0].terminal;
      window.shellTapeInitialMarks = new Set(shellTapeTerminal.markers.map(mark => mark.id));
    });

    stage = "playing the actual shell integration tape";
    await test.locator("#toggle-terminal-controls").click();
    await test.locator("#tapes").selectOption("shell-integration");
    const started = test.waitForResponse(response => response.url().endsWith("/tape") &&
      response.request().method() === "POST");
    await test.locator("#play-tape").click();
    check((await started).status() === 202, "Tape playback was not accepted");
    await test.waitForFunction(() => ["completed", "failed"].includes(
      document.querySelector("#tape-status").dataset.state), null, { timeout: 120000 });
    check(await test.locator("#tape-status").getAttribute("data-state") === "completed",
      await test.locator("#tape-status").innerText());
    await test.waitForFunction(() => shellTapeTerminal.screenText.split("\n").some(
      row => row.trim() === "Finished ticks show exit status only.") && shellTapeTerminal.viewport.liveTop > 0);

    stage = "retrieving retained details for every tape phase";
    const details = await test.evaluate(async () => {
      const marks = shellTapeTerminal.markers.filter(mark =>
        mark.source === "command" && !shellTapeInitialMarks.has(mark.id));
      return Promise.all(marks.map(async mark => ({
        id: mark.id, row: mark.row, ...await shellTapeTerminal.getCommandMarkDetails(mark.id)
      })));
    });
    for (const phase of ["prompt", "commandLine", "executing"]) {
      const marks = details.filter(mark => mark.phase === phase).sort((a, b) => a.row - b.row);
      // Prompt/input anchors may be collected when shell redraws erase their backing content.
      if (phase === "executing")
        check(marks.length === commands.length, `${phase}: expected three retained marks, got ${marks.length}`);
      for (const [index, mark] of marks.entries()) {
        const encoded = mark.rawParameters?.match(/^cmdline_url=(.*)$/)?.[1];
        check(!!encoded && !encoded.includes(" ") && !encoded.includes("'") && !encoded.includes("&"),
          `${phase}: command metadata was not percent-encoded: ${mark.rawParameters}`);
        const command = decodeURIComponent(encoded);
        check(phase === "executing" ? command === commands[index] : commands.includes(command),
          `${phase}: command detail does not match the command actually run: ${mark.rawParameters}`);
      }
    }
    const finished = details.filter(mark => mark.phase === "finished").sort((a, b) => a.row - b.row);
    check(finished.length === 3 && finished[0].exitCode === 0 &&
      finished[1].exitCode > 0 && finished[2].exitCode === 0,
      `Tape lost real command exit statuses: ${JSON.stringify(finished)}`);
    check(finished.every(mark => mark.rawParameters === null),
      "OSC 133 D must remain status-only, not fabricate grouped command details");

    stage = "hovering decoded command text including spaces, quotes, ampersands and slashes";
    await test.locator("#close-terminal-controls").click();
    await test.evaluate(() => shellTapeTerminal.setScrollbar({ hideDelay: 60000 }));
    await test.waitForFunction(() => shellTapeTerminal.viewport.liveTop > 0);
    const executing = details.filter(mark => mark.phase === "executing").sort((a, b) => a.row - b.row);
    check(executing.every((mark, index) => mark.row < finished[index].row),
      "Command ticks are obscured by co-located finished ticks");
    const tooltip = test.locator(".hex1b-scrollbar-tooltip-overlay");
    for (const [index, mark] of executing.entries()) {
      await test.mouse.move(1, 1);
      const point = await test.evaluate(id => {
        const terminal = shellTapeTerminal, marker = terminal.markers.find(mark => mark.id === id);
        const layout = terminal.layout, track = layout.scrollbar, bounds = terminal.element.getBoundingClientRect();
        return {
          x: bounds.left + (track.left + track.width / 2) * bounds.width / layout.width,
          y: bounds.top + (track.top + (track.height - 3) * marker.row /
            Math.max(1, terminal.viewport.totalRows - 1) + 1.5) * bounds.height / layout.height
        };
      }, mark.id);
      await test.mouse.move(point.x, point.y);
      await tooltip.getByRole("tooltip").waitFor({ state: "visible" });
      await test.waitForFunction(command =>
        shellTapeTerminal.element.querySelector(".hex1b-scrollbar-tooltip-overlay")?.textContent
          .split("\n").includes(command), commands[index]);
      const text = await tooltip.textContent();
      check(!text.includes("did not provide command text") && !text.includes("cmdline_url=") &&
        !text.includes("%20") && !text.includes("npm run build"), `Tooltip did not decode faithful details: ${text}`);
    }
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, commands, retainedDetails: details.length, errors };
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
