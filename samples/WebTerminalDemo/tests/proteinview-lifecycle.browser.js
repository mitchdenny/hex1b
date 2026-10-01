async page => {
  // Run after proteinview-live.browser.js. Optional screenshots go to the caller's private directory.
  const directory = await page.evaluate(() => window.proteinViewEvidenceDirectory);
  const backend = await page.evaluate(() => window.proteinViewAcceptance.terminal.stats.renderer);
  await page.evaluate(() => window.proteinViewAcceptance.terminal.focus());
  await page.keyboard.press("M");
  await page.waitForFunction(() => {
    const terminal = window.proteinViewAcceptance.terminal;
    return terminal.stats.imageCount === 0 && terminal.stats.textureBytes === 0
      && terminal.screenText.includes("\u2502 HD");
  }, null, { timeout: 10000 });
  await page.keyboard.press("m");
  await page.waitForFunction(() => {
    const terminal = window.proteinViewAcceptance.terminal;
    return terminal.stats.imageCount === 0 && terminal.screenText.includes("Braille")
      && /[\u2801-\u28ff]/u.test(terminal.screenText);
  }, null, { timeout: 10000 });
  if (directory)
    await page.screenshot({ path: `${directory}/live-${backend}-braille.png` });
  await page.keyboard.press("M");
  await page.waitForFunction(() => {
    const terminal = window.proteinViewAcceptance.terminal;
    return terminal.stats.imageCount === 1 && terminal.screenText.includes("FullHD");
  }, null, { timeout: 10000 });

  const cycles = [];
  for (let cycle = 0; cycle < 20; cycle++) {
    await page.evaluate(() => window.proteinViewAcceptance.terminal.dispose());
    await page.waitForFunction(async () => {
      const response = await fetch("/api/terminals");
      if (!response.ok) throw new Error(`Instance status failed: ${response.status}`);
      const instances = await response.json();
      const instance = instances.find(value => value.id === window.proteinViewAcceptance.instanceId);
      if (!instance) throw new Error("Closing a view unexpectedly ended the shared workload");
      return instance.peerCount === 0;
    }, null, { timeout: 10000 });
    const transport = cycle % 2 === 0 ? "direct" : "hmp1";
    await page.evaluate(async transport => {
      const { WebTerminal } = await import("/web-terminal/index.js");
      const state = window.proteinViewAcceptance;
      state.reconnectErrors = [];
      state.terminal = await WebTerminal.mount(document.querySelector("#terminal"), {
        url: `/ws?instance=${encodeURIComponent(state.instanceId)}&transport=${transport}`,
        renderer: new URL(location.href).searchParams.get("backend") || "webgpu",
        onStatus: (message, level) => { if (level === "error") state.reconnectErrors.push(message); }
      });
    }, transport);
    await page.waitForFunction(() => {
      const state = window.proteinViewAcceptance;
      if (state.reconnectErrors.length) throw new Error(state.reconnectErrors.join("; "));
      return state.terminal.stats.imageCount > 0;
    }, null, { timeout: 10000 });
    cycles.push(await page.evaluate(({ cycle, transport }) => {
      const terminal = window.proteinViewAcceptance.terminal;
      const stats = terminal.stats;
      if (stats.imageCount !== 1 || stats.textureBytes !== 1280000)
        throw new Error(`Reconnect retained incorrect graphics: ${JSON.stringify(stats)}`);
      if (!terminal.screenText.includes("FullHD"))
        throw new Error("Reconnect lost application text/state");
      return { cycle, transport, imageCount: stats.imageCount, textureBytes: stats.textureBytes,
        initialImagePayloadBytes: stats.imagePayloadBytes, frames: stats.frames };
    }, { cycle, transport }));
  }
  if (directory)
    await page.screenshot({ path: `${directory}/live-${backend}-reconnected.png` });
  await page.evaluate(() => {
    const terminal = window.proteinViewAcceptance.terminal;
    terminal.requestPrimary();
    terminal.focus();
  });
  return { modes: ["FullHD", "HalfBlock", "Braille", "FullHD"],
    reconnectCycles: cycles, note: "Application remains owned and paused; caller must end the terminal." };
}
