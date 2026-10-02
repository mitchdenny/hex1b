async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)[0];
  const results = [];
  for (const transport of ["direct", "hmp1"]) {
    const context = await page.context().browser().newContext({ viewport: { width: 1000, height: 800 } });
    const test = await context.newPage();
    const frames = [];
    test.on("websocket", socket => socket.on("framereceived", ({ payload }) => {
      if (typeof payload === "string" || payload.length < 8 || payload.readUInt32LE(0) !== 0x31545748) return;
      frames.push(JSON.parse(payload.subarray(8, 8 + payload.readUInt32LE(4)).toString("utf8")));
    }));
    let instance;
    try {
      await test.goto(`${origin}/?empty=1&renderer=webgl2`);
      const response = await test.request.post(`${origin}/api/terminals`, {
        headers: { Origin: origin }, data: { scene: "shell", columns: 80, rows: 24, reflowStrategy: "none" }
      });
      if (response.status() !== 201) throw new Error(`Could not create terminal: ${response.status()}`);
      instance = (await response.json()).id;
      await test.evaluate(async ({ instance, transport }) => {
        const { WebTerminal } = await import("/web-terminal/index.js");
        const host = document.createElement("div");
        host.id = "final-output-terminal";
        host.style.cssText = "position:fixed;inset:0 auto auto 0;width:800px;height:480px;z-index:10000";
        document.body.append(host);
        window.finalOutputTerminal = await WebTerminal.mount(host, {
          url: `/ws?instance=${instance}&transport=${transport}`,
          renderer: "webgl2", preserveOnDisconnect: true,
          sizing: { mode: "fixed", columns: 80, rows: 24 },
          onClose: details => {
            window.finalOutputClose = { details, text: window.finalOutputTerminal.screenText };
          }
        });
      }, { instance, transport });
      await test.waitForFunction(() => /[#$%>\u276f]\s*$/.test(finalOutputTerminal.screenText.trimEnd()));
      await test.evaluate(() => {
        finalOutputTerminal.focus();
        finalOutputTerminal.paste(
          "exec /bin/sh -c \"printf '\\033[2J\\033[HFIRST-OUTPUT\\nFINAL-STDOUT\\n'; printf 'FINAL-STDERR\\n' >&2; " +
          "printf '\\033#6DOUBLE-WIDTH e\u0301\u754c\\n\\033#3DOUBLE-HEIGHT e\u0301\u754c\\n\\033#4DOUBLE-HEIGHT e\u0301\u754c\\n\\033#5FINAL-UNTERMINATED'; exit 7\"");
      });
      await test.keyboard.press("Enter");
      await test.waitForFunction(() => window.finalOutputClose !== undefined);
      const state = await test.evaluate(() => ({
        close: finalOutputClose, text: finalOutputTerminal.screenText,
        connected: finalOutputTerminal.connected,
        disabled: finalOutputTerminal.element.shadowRoot.querySelector("textarea").disabled
      }));
      const expected = ["FIRST-OUTPUT", "FINAL-STDOUT", "FINAL-STDERR",
        "DOUBLE-WIDTH e\u0301\u754c", "DOUBLE-HEIGHT e\u0301\u754c", "DOUBLE-HEIGHT e\u0301\u754c", "FINAL-UNTERMINATED"];
      const lines = state.text.split("\n").slice(0, expected.length);
      if (JSON.stringify(lines) !== JSON.stringify(expected) || state.close.text !== state.text ||
          state.close.details.code !== 4000 || !state.close.details.reason.endsWith("code 7") ||
          state.connected || !state.disabled)
        throw new Error(`${transport}: lost final output or incorrect close state: ${JSON.stringify(state)}`);
      const lineRenditions = frames.at(-1)?.lineRenditions?.slice(0, expected.length);
      if (JSON.stringify(lineRenditions) !== JSON.stringify([0, 0, 0, 1, 2, 3, 0]))
        throw new Error(`${transport}: final DEC row modes were lost: ${JSON.stringify(lineRenditions)}`);
      const canvas = test.locator("#final-output-terminal canvas:not(.scrollbar-canvas)");
      const before = await canvas.screenshot();
      // Cross a full blinking period: the disconnected framebuffer must remain unchanged.
      await test.waitForTimeout(750);
      const after = await canvas.screenshot();
      if (!before.equals(after)) throw new Error(`${transport}: the final framebuffer changed after disconnect`);
      await test.evaluate(() => finalOutputTerminal.dispose());
      if (await test.locator("#final-output-terminal .hex1b-terminal").count())
        throw new Error(`${transport}: disposal did not remove the retained view`);
      results.push({ transport, finalLines: lines, lineRenditions, stableFramebuffer: true });
    } finally {
      if (instance) await test.request.delete(`${origin}/api/terminals/${instance}`, { headers: { Origin: origin } });
      await context.close();
    }
  }
  return { passed: true, results };
}
