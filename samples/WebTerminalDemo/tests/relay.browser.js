async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1100 } });
  const test = await context.newPage();
  const errors = [];
  const instances = [];
  const results = [];
  const urls = [];
  let stage = "";
  test.on("pageerror", error => errors.push(error.message));
  test.on("websocket", socket => urls.push(socket.url()));
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const ready = id => test.waitForFunction(id => {
    const view = webTerminalViews.get(id);
    return view?.terminal?.connected && view.stats.frames >= 10 && view.stats.imageCount > 0;
  }, id, { timeout: 30000 });
  const advance = async id => {
    const before = await test.evaluate(id => webTerminalViews.get(id).terminal.stats, id);
    await test.waitForFunction(({ id, frames }) => webTerminalViews.get(id).stats.frames >= frames + 10,
      { id, frames: before.frames }, { timeout: 30000 });
    const after = await test.evaluate(id => webTerminalViews.get(id).terminal.stats, id);
    check(after.imageCount > 0 && after.gpu === "ready" && after.warnings.length === 0 &&
      after.discardedFrames === 0, `Bad relay graphics state: ${JSON.stringify(after)}`);
    return { before, after };
  };
  try {
    stage = "invalid-transport";
    const invalid = await test.request.get(`${origin}/ws?instance=missing&transport=invalid`, {
      headers: {
        Origin: origin, Connection: "Upgrade", Upgrade: "websocket",
        "Sec-WebSocket-Version": "13", "Sec-WebSocket-Key": "dGhlIHNhbXBsZSBub25jZQ=="
      }
    });
    check(invalid.status() === 400 && (await invalid.json()).error === "transport must be direct or hmp1.",
      "Invalid transport must be rejected before opening a view");
    for (const scene of ["kgp", "animation"]) {
      stage = `${scene}/direct`;
      await test.goto(`${origin}/?empty=1&renderer=webgl2`);
      check(await test.locator("#transport").inputValue() === "direct", "Direct HWT1 must remain the default");
      await test.locator("#scene").selectOption(scene);
      const created = test.waitForResponse(response => response.url() === `${origin}/api/terminals` &&
        response.request().method() === "POST" && response.status() === 201);
      await test.locator("#create").click();
      const instance = await (await created).json();
      instances.push(instance.id);
      await ready("1");
      await test.waitForFunction(() => webTerminalViews.get("1").terminal.peer.isPrimary);

      stage = `${scene}/mixed-relay`;
      await test.locator("#transport").selectOption("hmp1");
      await test.locator("#attach").click();
      await ready("2");
      const peers = await test.evaluate(() => [...webTerminalViews.values()].map(view => ({
        transport: view.transport, peer: view.terminal.peer
      })));
      check(peers[0].transport === "direct" && peers[1].transport === "hmp1" &&
        peers[0].peer.id !== peers[1].peer.id && !peers[1].peer.isPrimary, "Views must have independent peers and transports");
      check((await test.locator("#metric-transport").textContent()).includes("HMP1 relay"), "Transport diagnostic is missing");
      const firstReplay = await advance("2");
      if (scene === "kgp")
        check(firstReplay.after.imageUploadBytes === firstReplay.before.imageUploadBytes, "Placement-only motion re-uploaded pixels");
      else
        check(firstReplay.after.workloadBytes === firstReplay.before.workloadBytes, "Silent animation required fresh HMP1 traffic");
      await test.evaluate(() => webTerminalViews.get("2").terminal.focus());
      await test.keyboard.type("relay-input");
      await test.waitForFunction(() => [...webTerminalViews.values()].every(view => view.text.includes("relay-input")));

      stage = `${scene}/primary`;
      await test.locator('[data-view="2"] .take-primary').click();
      await test.waitForFunction(() => webTerminalViews.get("2").terminal.peer.isPrimary &&
        !webTerminalViews.get("1").terminal.peer.isPrimary);
      await test.locator('[data-view="2"] .view-resolution').selectOption("80x24");
      await test.waitForFunction(() => [...webTerminalViews.values()].every(view =>
        view.terminal.geometry.columns === 80 && view.terminal.geometry.rows === 24));
      await test.locator('[data-view="2"] .close-view').click();
      await test.waitForFunction(() => webTerminalViews.size === 1 &&
        webTerminalViews.get("1").terminal.peer.primaryId === null);

      stage = `${scene}/reconnect`;
      await test.locator("#attach").click();
      await ready("3");
      const reconnect = await test.evaluate(() => webTerminalViews.get("3").terminal.peer);
      check(reconnect.id !== peers[1].peer.id && !reconnect.isPrimary, "Reattachment must create a fresh secondary peer");
      await advance("3");

      stage = `${scene}/navigation-return`;
      await test.goto(`${origin}/?empty=1&renderer=webgl2&transport=hmp1`);
      await test.waitForFunction(async id => (await (await fetch("/api/terminals")).json())
        .find(instance => instance.id === id)?.peerCount === 0, instance.id);
      check(await test.locator("#transport").inputValue() === "hmp1", "Transport query was not applied");
      await test.locator("#instances").selectOption(instance.id);
      await test.locator("#attach").click();
      await ready("1");
      const returned = await advance("1");
      check(await test.evaluate(previous => webTerminalViews.get("1").terminal.peer.id !== previous, reconnect.id),
        "Navigation-return reused an old relay peer");
      results.push({ scene, frames: returned.after.frames, images: returned.after.imageCount,
        transport: "hmp1", discardedFrames: returned.after.discardedFrames });
      await test.request.delete(`${origin}/api/terminals/${instance.id}`, { headers: { Origin: origin } });
      instances.pop();
    }
    check(urls.some(url => /[?&]transport=direct(?:&|$)/.test(url)) &&
      urls.some(url => /[?&]transport=hmp1(?:&|$)/.test(url)), "Transport selector did not reach the WebSocket route");
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, results };
  } catch (error) {
    const state = await test.evaluate(() => [...(window.webTerminalViews?.values() || [])].map(view => ({
      transport: view.transport, status: view.element.querySelector(".view-status").textContent,
      peer: view.terminal?.peer, stats: view.stats
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
