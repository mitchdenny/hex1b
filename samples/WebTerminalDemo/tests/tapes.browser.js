async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1100 } });
  const test = await context.newPage();
  const instances = [];
  const errors = [];
  const headers = { Origin: origin };
  let stage = "shell catalog";
  test.on("pageerror", error => errors.push(error.message));
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const list = async () => (await test.request.get(`${origin}/api/terminals`)).json();
  const creation = () => test.waitForResponse(response =>
    response.url() === `${origin}/api/terminals` && response.request().method() === "POST");
  const play = async tapeId => {
    await test.locator("#tapes").selectOption(tapeId);
    const response = test.waitForResponse(response => response.url().endsWith("/tape") &&
      response.request().method() === "POST");
    await test.locator("#play-tape").click();
    check((await response).status() === 202, "Tape did not start");
    await test.waitForFunction(() => document.querySelector("#tape-status").dataset.state === "running");
  };
  const completed = async () => {
    await test.waitForFunction(() => ["completed", "failed"].includes(document.querySelector("#tape-status").dataset.state));
    check(await test.locator("#tape-status").getAttribute("data-state") === "completed",
      await test.locator("#tape-status").innerText());
  };
  const marker = async text => test.waitForFunction(({ id, text }) =>
    [...webTerminalViews.values()].filter(view => view.instance.id === id).every(view =>
      view.terminal.screenText.split("\n").some(line => line.trim() === text)),
    { id: instances[0], text });
  try {
    const before = await list();
    const created = creation();
    await test.goto(`${origin}/?scene=shell&renderer=webgl2`);
    const shell = await (await created).json();
    instances.push(shell.id);
    check(shell.tapes.length >= 2 && shell.tapes.every(tape => tape.scene === "shell"), "Shell catalog is missing or crosses scenes");
    check(shell.tapePlayback === null, "A new shell already has playback state");
    await test.waitForFunction(() => webTerminalViews.get("1")?.terminal?.peer.isPrimary &&
      /([#$%>]|[^\x00-\x7f])$/.test(webTerminalViews.get("1").terminal.screenText.trimEnd()));
    await test.evaluate(() => {
      window.tapeOriginalTerminal = webTerminalViews.get("1").terminal;
      window.tapeOriginalTerminal.setSizing({ mode: "fixed", columns: 120, rows: 32 });
    });
    await test.waitForFunction(() => webTerminalViews.get("1").stats.columns === 120 && webTerminalViews.get("1").stats.rows === 32);
    await test.locator("#attach").click();
    await test.waitForFunction(() => webTerminalViews.get("2")?.terminal?.connected);
    check(await test.evaluate(() => !webTerminalViews.get("2").terminal.peer.isPrimary), "Attaching a view claimed primary");

    stage = "request restrictions";
    check((await test.request.post(`${origin}/api/terminals/${shell.id}/tape`, {
      headers, data: { tapeId: "../../README" }
    })).status() === 400, "An arbitrary server path was accepted");
    check((await test.request.post(`${origin}/api/terminals/${shell.id}/tape`, {
      headers, data: {}
    })).status() === 400, "A missing tape ID was accepted");
    check((await test.request.post(`${origin}/api/terminals/${shell.id}/tape`, {
      headers: { Origin: "http://unrelated.invalid" }, data: { tapeId: "hello" }
    })).status() === 403, "Cross-origin playback was accepted");
    check((await test.request.delete(`${origin}/api/terminals/${shell.id}/tape`, { headers })).status() === 409,
      "An idle terminal reported an active tape");

    stage = "existing-terminal playback from a secondary view";
    await test.locator("#scene").selectOption("text");
    check(await test.locator("#tapes option[value=hello]").count() === 1, "New-terminal scene selection changed the existing terminal's catalog");
    await play("hello");
    check(await test.locator("#play-tape").isDisabled(), "Play stayed enabled during playback");
    check((await test.request.post(`${origin}/api/terminals/${shell.id}/tape`, {
      headers, data: { tapeId: "hello" }
    })).status() === 409, "Overlapping playback was accepted");
    await completed();
    await marker("Hello from Tape");
    const after = await list();
    check(after.length === before.length + 1, "Playback created another terminal");
    const current = after.find(instance => instance.id === shell.id);
    check(current.createdAt === shell.createdAt && current.columns === 120 && current.rows === 32 &&
      current.tapePlayback.completedCommands > 0, "Playback replaced or resized the terminal");
    check(await test.evaluate(() => webTerminalViews.get("1").terminal === window.tapeOriginalTerminal),
      "Playback replaced the mounted terminal");

    stage = "editing and history";
    await play("line-editing");
    await completed();
    await test.waitForFunction(() => ["1", "2"].every(id =>
      webTerminalViews.get(id).terminal.screenText.split("\n").filter(line => line.trim() === "Tape editing works").length === 2));
    if (shell.tapes.some(tape => tape.id === "ansi-colors")) {
      stage = "ANSI colors";
      await play("ansi-colors");
      await completed();
      await marker("Red Green Blue");
    }

    stage = "scene filtering and selection retention";
    await test.locator("#tapes").selectOption("line-editing");
    const createdText = creation();
    await test.locator("#create").click();
    const text = await (await createdText).json();
    instances.push(text.id);
    await test.waitForFunction(() => webTerminalViews.get("3")?.terminal?.connected);
    check(text.tapes.length === 0 && await test.locator("#play-tape").isDisabled() &&
      await test.locator("#tapes").isDisabled(), "Generated scene offers shell tapes");
    check((await test.request.post(`${origin}/api/terminals/${text.id}/tape`, {
      headers, data: { tapeId: "hello" }
    })).status() === 400, "A shell tape played against a generated scene");
    await test.locator("#instances").selectOption(shell.id);
    check(await test.locator("#tapes").inputValue() === "line-editing", "Switching instances lost tape selection");

    stage = "cancelling without interrupting the shell";
    await test.locator('.terminal-window[data-view="1"] .view-title').click();
    await test.keyboard.type("pending_input");
    await test.waitForFunction(() => webTerminalViews.get("1").terminal.screenText.trimEnd().endsWith("pending_input"));
    await play("hello");
    const cancelled = test.waitForResponse(response => response.url().endsWith("/tape") &&
      response.request().method() === "DELETE");
    await test.locator("#stop-tape").click();
    check((await cancelled).status() === 202, "Cancellation was rejected");
    await test.waitForFunction(() => document.querySelector("#tape-status").dataset.state === "cancelled");
    check(await test.evaluate(() => webTerminalViews.get("1").terminal.connected &&
      webTerminalViews.get("1").terminal.screenText.trimEnd().endsWith("pending_input")),
      "Stopping a tape interrupted the shell or erased existing input");

    stage = "visible runtime failure";
    await play("hello");
    await test.waitForFunction(() => document.querySelector("#tape-status").dataset.state === "failed");
    check(await test.locator("#tape-status").getAttribute("data-level") === "error" &&
      (await test.locator("#tape-status").innerText()).includes("hello.tape:"), "Runtime failure lacks source diagnostics");
    check(await test.locator("#play-tape").isEnabled() && await test.locator("#stop-tape").isDisabled(),
      "A failed tape left controls locked");
    check(await test.evaluate(() => !webTerminalViews.get("3").terminal.screenText.includes("pending_input")),
      "Input crossed independent terminal instances");

    stage = "zero-view playback and terminal shutdown";
    await test.locator('.terminal-window[data-view="2"] .close-view').click();
    await test.locator('.terminal-window[data-view="1"] .close-view').click();
    await test.locator("#instances").selectOption(shell.id);
    await play("hello");
    check((await test.request.delete(`${origin}/api/terminals/${shell.id}`, { headers })).status() === 204,
      "Ending a terminal did not drain its playback");
    check(!(await list()).some(instance => instance.id === shell.id), "Ended terminal remains in the registry");
    check((await test.request.post(`${origin}/api/terminals/${shell.id}/tape`, {
      headers, data: { tapeId: "hello" }
    })).status() === 404, "Playback started after terminal disposal");
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return {
      passed: true,
      covered: ["scene catalog", "same-origin admission", "secondary-view playback", "shared output",
        "retained terminal and geometry", "duplicate rejection", "editing and history", "platform-filtered colors",
        "selection retention", "cancellation", "visible runtime failure", "zero-view playback", "shutdown cleanup"],
      browserErrors: errors
    };
  } catch (error) {
    const state = await test.evaluate(() => ({
      playback: document.querySelector("#tape-status")?.textContent,
      views: [...(window.webTerminalViews?.values() || [])].map(view => ({
        id: view.id, status: view.element.querySelector(".view-status").textContent,
        text: view.terminal?.screenText.slice(-2000)
      }))
    }));
    throw new Error(`${stage}: ${error.message}; ${JSON.stringify(state)}`);
  } finally {
    try {
      for (const id of instances) await test.request.delete(`${origin}/api/terminals/${id}`, { headers });
    } finally {
      await context.close();
    }
  }
}
