async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)[0];
  const results = [];
  for (const transport of ["direct", "hmp1"]) {
    for (const preserve of [true, false]) {
      for (const overlay of [true, false]) {
        for (const exitCode of [0, 7]) {
          const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1100 } });
          const test = await context.newPage();
          const errors = [];
          test.on("pageerror", error => errors.push(error.message));
          let instance;
          try {
            await test.goto(`${origin}/?empty=1&renderer=webgl2&transport=${transport}`);
            await test.locator("#scene").selectOption("shell");
            await test.locator("#preserve-on-disconnect").setChecked(preserve);
            await test.locator("#show-exit-overlay").setChecked(overlay);
            const created = test.waitForResponse(response => response.url() === `${origin}/api/terminals` &&
              response.request().method() === "POST" && response.status() === 201);
            await test.locator("#create").click();
            instance = (await (await created).json()).id;
            await test.waitForFunction(() => webTerminalViews.get("1")?.phase === "connected");
            await test.locator("#tapes").selectOption(exitCode === 0 ? "exit-success" : "exit-error");
            const played = test.waitForResponse(response => response.url().endsWith("/tape") &&
              response.request().method() === "POST");
            await test.locator("#play-tape").click();
            if ((await played).status() !== 202) throw new Error("Exit tape was rejected");
            await test.locator("#close-terminal-controls").click();
            await test.waitForFunction(() => webTerminalViews.get("1")?.phase === "closed");
            const tile = test.locator('.terminal-window[data-view="1"]');
            const state = await test.evaluate(() => {
              const view = webTerminalViews.get("1");
              return { close: view.closure.close, exitCode: view.closure.exitCode,
                text: view.terminal?.screenText, cachedText: view.text,
                connected: view.terminal?.connected, preserved: view.element.dataset.preserved,
                preserveOption: view.preserveOnDisconnect };
            });
            if (state.close.code !== 4000 || state.exitCode !== exitCode ||
                state.preserveOption !== preserve || state.preserved !== String(preserve))
              throw new Error(`Wrong exit callback result: ${JSON.stringify(state)}`);
            if (preserve) {
              if (state.connected || !state.text.split("\n").some(line => line.trim() === `FINAL-OUTPUT-EXIT-${exitCode}`))
                throw new Error(`Final output was lost: ${JSON.stringify(state)}`);
              if (!await tile.locator(".terminal-mount").evaluate(mount =>
                mount.inert && mount.querySelector(".hex1b-terminal").shadowRoot.querySelector("textarea").disabled))
                throw new Error("Retained terminal still accepts input");
            } else if (await tile.locator(".terminal-mount").evaluate(mount => mount.childElementCount) ||
                state.text !== undefined || state.cachedText !== "") {
              throw new Error("Preservation opt-out retained terminal content or resources");
            }
            const message = tile.locator(".closed-overlay");
            if (await message.isVisible() !== overlay)
              throw new Error("The app-owned overlay ignored its opt-in setting");
            const title = await tile.locator(".closed-title").textContent();
            if (title !== (exitCode === 0 ? "Finished successfully" : "The program exited with an error") ||
                await tile.locator(".closed-exit-code").textContent() !== `Process exit code: ${exitCode}` ||
                !await tile.locator(".reconnect-view").isDisabled())
              throw new Error("Wrong friendly message, process exit code, or reconnect state");
            if (!overlay) await tile.locator(".show-exit-details").click();
            if (preserve) {
              await tile.locator(".view-final-output").click();
              if (await message.isVisible()) throw new Error("View final output did not dismiss the overlay");
              const canvas = tile.locator(".terminal-mount canvas:not(.scrollbar-canvas)");
              const before = await canvas.screenshot();
              await test.waitForTimeout(750);
              if (!before.equals(await canvas.screenshot()))
                throw new Error("Dismissed exit overlay changed the retained framebuffer");
              await tile.locator(".show-exit-details").click();
              if (!await message.isVisible()) throw new Error("Exit details could not reopen the overlay");
            } else if (!await tile.locator(".view-final-output").isDisabled()) {
              throw new Error("A cleared terminal offered nonexistent final output");
            }
            await tile.locator(".dismiss-view").click();
            await test.waitForFunction(() => webTerminalViews.size === 0);
            if (errors.length) throw new Error(errors.join("\n"));
            results.push({ transport, preserve, overlay, exitCode, passed: true });
          } finally {
            if (instance) await test.request.delete(`${origin}/api/terminals/${instance}`, { headers: { Origin: origin } });
            await context.close();
          }
        }
      }
    }
    for (const mode of ["close", "abort"]) {
      const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1100 } });
      const test = await context.newPage();
      let instance;
      try {
        await test.goto(`${origin}/?empty=1&renderer=webgl2&transport=${transport}`);
        await test.locator("#scene").selectOption("activity");
        const created = test.waitForResponse(response => response.url() === `${origin}/api/terminals` &&
          response.request().method() === "POST" && response.status() === 201);
        await test.locator("#create").click();
        instance = (await (await created).json()).id;
        await test.waitForFunction(() => webTerminalViews.get("1")?.phase === "connected");
        await test.locator("#close-terminal-controls").click();
        const tile = test.locator('.terminal-window[data-view="1"]');
        await tile.locator(".view-failure").selectOption(mode);
        await tile.locator(".trigger-failure").click();
        await test.waitForFunction(() => webTerminalViews.get("1")?.phase === "closed");
        if (!await test.evaluate(() => webTerminalViews.get("1").closure.exitCode === undefined) ||
            await tile.locator(".closed-exit-code").textContent() !== "Process exit code: unavailable")
          throw new Error(`${transport}/${mode}: a view disconnect fabricated a process exit code`);
        results.push({ transport, mode, exitCode: null, passed: true });
      } finally {
        if (instance) await test.request.delete(`${origin}/api/terminals/${instance}`, { headers: { Origin: origin } });
        await context.close();
      }
    }
  }
  return { passed: true, cases: results.length, results };
}
