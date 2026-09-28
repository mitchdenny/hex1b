async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5397";
  const renderer = page.url().includes("renderer=webgpu") ? "webgpu" : "webgl2";
  const context = await page.context().browser().newContext({
    viewport: { width: 1440, height: 1000 }, reducedMotion: "reduce"
  });
  const test = await context.newPage();
  const errors = [], frames = [], ids = [];
  let stage = "startup";
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  test.on("pageerror", error => errors.push(error.message));
  test.on("websocket", socket => socket.on("framereceived", ({ payload }) => {
    if (typeof payload === "string" || payload.length < 8 || payload.readUInt32LE(0) !== 0x31545748) return;
    frames.push(JSON.parse(payload.subarray(8, 8 + payload.readUInt32LE(4)).toString("utf8")));
  }));
  try {
    const created = test.waitForResponse(response =>
      response.url() === `${origin}/api/terminals` && response.request().method() === "POST");
    await test.goto(`${origin}/?scene=shell&renderer=${renderer}`);
    const instance = await (await created).json();
    ids.push(instance.id);
    check(instance.tapes.some(tape => tape.id === "double-height"), "DEC tape is not registered");
    await test.waitForFunction(() => webTerminalViews.get("1")?.terminal?.connected &&
      /([#$%>]|[^\x00-\x7f])$/.test(webTerminalViews.get("1").terminal.screenText.trimEnd()));
    await test.evaluate(() => webTerminalViews.get("1").terminal.setSizing({ mode: "fixed", columns: 100, rows: 26 }));
    await test.waitForFunction(() => webTerminalViews.get("1").stats.columns === 100);
    await test.locator("#toggle-terminal-controls").click();
    await test.locator("#tapes").selectOption("double-height");
    stage = "tape playback";
    await test.locator("#play-tape").click();
    await test.waitForFunction(() => ["completed", "failed"].includes(document.querySelector("#tape-status").dataset.state));
    check(await test.locator("#tape-status").getAttribute("data-state") === "completed",
      await test.locator("#tape-status").innerText());
    await test.waitForFunction(() => webTerminalViews.get("1").terminal.screenText.includes("DEC_DOUBLE_HEIGHT_READY"));
    const state = frames.findLast(frame => frame.lineRenditions?.[4] === 2 && frame.lineRenditions?.[5] === 3);
    check(state && state.lineRenditions[3] === 1 && state.lineRenditions[12] === 0, "Expected independent row modes missing");
    const text = await test.evaluate(() => webTerminalViews.get("1").terminal.screenText);
    check(text.split("\n").filter(line => line.trim() === "Hello Hex1b 123").length === 2, "Both physical text halves must exist");
    check(text.includes("Unicode: café 界"), "Unicode did not survive tape playback");
    await test.locator("#close-terminal-controls").click();
    await test.screenshot({ path: `.playwright-cli/double-height-${renderer}.png` });

    // Attach after output has finished: the baseline must include row metadata.
    const baselineStart = frames.length;
    stage = "late attachment";
    await test.locator("#toggle-terminal-controls").click();
    await test.locator("#attach").click();
    await test.waitForFunction(() => webTerminalViews.get("2")?.terminal?.screenText.includes("DEC_DOUBLE_HEIGHT_READY"));
    check(frames.slice(baselineStart).some(frame => frame.full && frame.lineRenditions[4] === 2 &&
      frame.lineRenditions[5] === 3), "Late attachment lost line rendition");

    // Resize crops fixed rows without treating the two halves as a paragraph.
    stage = "resize";
    await test.evaluate(() => webTerminalViews.get("1").terminal.setSizing({ mode: "fixed", columns: 20, rows: 26 }));
    await test.waitForFunction(() => webTerminalViews.get("1").stats.columns === 20);
    let found = false;
    for (let attempt = 0; attempt < 10 && !found; attempt++) {
      found = await test.evaluate(() => webTerminalViews.get("1").terminal.screenText.split("\n")
        .filter(line => line.trim() === "Hello Hex1").length === 2);
      if (!found) {
        await test.evaluate(() => webTerminalViews.get("1").terminal.scrollLines(-10));
        await test.waitForFunction(() => !webTerminalViews.get("1").terminal.viewport.pending);
      }
    }
    check(found, "Truncated halves are missing from resized screen/history");
    check(errors.length === 0, errors.join("; "));
    return { renderer, tape: "completed", independentHalves: true,
      unicode: true, lateAttachment: true, resize: true, screenshot: `.playwright-cli/double-height-${renderer}.png` };
  } catch (error) {
    throw new Error(`${stage}: ${error.message}\n${await test.evaluate(() =>
      webTerminalViews.get("1")?.terminal?.screenText)}`);
  } finally {
    for (const id of ids)
      await test.request.delete(`${origin}/api/terminals/${id}`, { headers: { Origin: origin } });
    await context.close();
  }
}
