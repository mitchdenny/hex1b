async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1100 }, deviceScaleFactor: 2 });
  const test = await context.newPage();
  const instances = [];
  const errors = [];
  let stage = "initial shell";
  test.on("pageerror", error => errors.push(error.message));
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const creationResponse = () => test.waitForResponse(response =>
    response.url() === `${origin}/api/terminals` && response.request().method() === "POST" && response.status() === 201);
  const marker = async text => {
    stage = text;
    await test.waitForFunction(text => webTerminalViews.get("1").terminal.screenText.split("\n").some(line => line.trim() === text) &&
      /[❯$#%>]$/.test(webTerminalViews.get("1").terminal.screenText.trimEnd()), text);
  };
  try {
    const created = creationResponse();
    await test.goto(`${origin}/?scene=shell&scale=auto`);
    instances.push((await (await created).json()).id);
    await test.waitForFunction(() => webTerminalViews.get("1")?.terminal?.peer.isPrimary &&
      /[❯$#%>]$/.test(webTerminalViews.get("1").terminal.screenText.trimEnd()), null, { timeout: 30000 });
    await test.evaluate(() => webTerminalViews.get("1").terminal.setSizing({ mode: "fixed", columns: 120, rows: 40 }));
    await test.waitForFunction(() => webTerminalViews.get("1").stats.columns === 120 && webTerminalViews.get("1").stats.rows === 40);
    const first = test.locator('.terminal-window[data-view="1"]');
    await first.locator("canvas").click({ position: { x: 100, y: 100 } });
    await test.keyboard.type("printf '__SHELL_READY__\\n'");
    await test.keyboard.press("Enter");
    await marker("__SHELL_READY__");

    stage = "tabbed output after widening";
    await test.evaluate(() => webTerminalViews.get("1").terminal.setSizing({ mode: "fixed", columns: 154, rows: 34 }));
    await test.waitForFunction(() => webTerminalViews.get("1").stats.columns === 154 && webTerminalViews.get("1").stats.rows === 34);
    await test.keyboard.type("printf '\\033[2J\\033[H\\033[1;73HMouseTest\\t\\tRescueDemo\\r\\n__TABS_OK__\\r\\n'");
    await test.keyboard.press("Enter");
    await marker("__TABS_OK__");
    check(await test.evaluate(() => {
      const lines = webTerminalViews.get("1").terminal.screenText.split("\n");
      return lines[0].slice(72, 81) === "MouseTest" && lines[0].slice(96, 106) === "RescueDemo" &&
        lines[1].trim() === "__TABS_OK__";
    }), "A tab beyond the initial width misplaced the leading character and wrapped the column");
    await test.evaluate(() => webTerminalViews.get("1").terminal.setSizing({ mode: "fixed", columns: 120, rows: 40 }));
    await test.waitForFunction(() => webTerminalViews.get("1").stats.columns === 120 && webTerminalViews.get("1").stats.rows === 40);

    await test.keyboard.type("printf '__KEYBOARD_OK__\\n'X");
    await test.keyboard.press("Backspace");
    await test.keyboard.press("Enter");
    await marker("__KEYBOARD_OK__");
    check(await test.evaluate(() => !webTerminalViews.get("1").terminal.screenText.includes("\\n'X")), "Backspace left stale shell text");
    await test.keyboard.press("ArrowUp");
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => webTerminalViews.get("1").terminal.screenText.split("\n").filter(line => line.trim() === "__KEYBOARD_OK__").length >= 2 &&
      /[❯$#%>]$/.test(webTerminalViews.get("1").terminal.screenText.trimEnd()));
    await test.evaluate(() => {
      const input = webTerminalViews.get("1").terminal.element.shadowRoot.querySelector("textarea");
      const clipboardData = new DataTransfer();
      clipboardData.setData("text/plain", "printf '__PASTE_OK__\\n'");
      input.dispatchEvent(new ClipboardEvent("paste", { clipboardData, bubbles: true, cancelable: true }));
    });
    await test.keyboard.press("Enter");
    await marker("__PASTE_OK__");

    stage = "MouseTest startup";
    await test.keyboard.type("dotnet ../MouseTest/bin/Release/net10.0/MouseTest.dll");
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => webTerminalViews.get("1").terminal.screenText.includes("Press any key to start"));
    await test.keyboard.press("Space");
    await test.waitForFunction(() => webTerminalViews.get("1").terminal.screenText.includes("Button 1 (0)") &&
      webTerminalViews.get("1").stats.mouseTracking === 1003);
    const position = await test.evaluate(() => {
      const lines = webTerminalViews.get("1").terminal.screenText.split("\n");
      const row = lines.findIndex(line => line.includes("Button 1"));
      return { row, column: lines[row].indexOf("Button 1") };
    });
    let box = await first.locator("canvas").boundingBox();
    await test.mouse.click(box.x + (position.column + 3.5) * box.width / 120, box.y + (position.row + .5) * box.height / 40);
    await test.waitForFunction(() => webTerminalViews.get("1").terminal.screenText.includes("Button 1 (1)"));
    await test.mouse.move(box.x + 21.5 * box.width / 120, box.y + 12.5 * box.height / 40);
    await test.mouse.down();
    await test.mouse.move(box.x + 31.5 * box.width / 120, box.y + 12.5 * box.height / 40, { steps: 12 });
    await test.mouse.up();
    await test.waitForFunction(column => webTerminalViews.get("1").terminal.screenText.split("\n")
      .find(line => line.includes("Button 1")).indexOf("Button 1") === column + 10, position.column);

    await first.locator(".thumbnail").click();
    stage = "thumbnail mouse";
    const second = test.locator('.terminal-window[data-view="2"]');
    await test.waitForFunction(() => webTerminalViews.get("2")?.terminal?.connected &&
      webTerminalViews.get("2").terminal.screenText.includes("Button 1 (1)") && webTerminalViews.get("2").stats.mouseTracking === 1003);
    box = await second.locator("canvas").boundingBox();
    await test.mouse.click(box.x + (position.column + 13.5) * box.width / 120, box.y + (position.row + .5) * box.height / 40);
    await test.waitForFunction(() => webTerminalViews.get("1").terminal.screenText.includes("Button 1 (2)") &&
      webTerminalViews.get("2").terminal.screenText.includes("Button 1 (2)"));
    check(await test.evaluate(() => !webTerminalViews.get("2").terminal.peer.isPrimary), "Thumbnail input claimed primary");

    // Selecting a different window's chrome must move input focus too.
    const createdSecond = creationResponse();
    stage = "second shell";
    await test.locator("#scene").selectOption("shell");
    await test.locator("#create").click();
    instances.push((await (await createdSecond).json()).id);
    await test.waitForFunction(() => webTerminalViews.get("3")?.terminal?.peer.isPrimary &&
      /[❯$#%>]$/.test(webTerminalViews.get("3").terminal.screenText.trimEnd()));
    await test.locator('.terminal-window[data-view="3"] canvas').click({ position: { x: 100, y: 100 } });
    await test.keyboard.type("printf '__SECOND_READY__\\n'");
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => webTerminalViews.get("3").terminal.screenText.split("\n").some(line => line.trim() === "__SECOND_READY__") &&
      /[❯$#%>]$/.test(webTerminalViews.get("3").terminal.screenText.trimEnd()));
    stage = "title-bar focus";
    await first.locator(".view-title").click();
    await test.keyboard.press("Control+c");
    await test.waitForFunction(() => webTerminalViews.get("1").stats.mouseTracking === 0 &&
      /[❯$#%>]$/.test(webTerminalViews.get("1").terminal.screenText.trimEnd()));
    await test.keyboard.type("printf '__FIRST_ONLY__\\n'");
    await test.keyboard.press("Enter");
    await marker("__FIRST_ONLY__");
    check(await test.evaluate(() => !webTerminalViews.get("3").terminal.screenText.includes("__FIRST_ONLY__")), "Keyboard crossed independent terminal instances");
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, covered: ["tabbed shell columns after widening", "real shell Backspace", "history arrow encoding", "paste", "DPR2 MouseTest click", "ten-column splitter drag", "scaled secondary mouse input", "title-bar focus", "independent instance input"], browserErrors: errors };
  } catch (error) {
    const state = await test.evaluate(() => [...(window.webTerminalViews?.values() || [])].map(view => ({
      id: view.id, status: view.element.querySelector(".view-status").textContent,
      peer: view.terminal?.peer, geometry: view.terminal?.geometry, text: view.terminal?.screenText.slice(-4000)
    })));
    throw new Error(`${stage}: ${error.message}; ${JSON.stringify(state)}`);
  } finally {
    try {
      for (const id of instances) await test.request.delete(`${origin}/api/terminals/${id}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
