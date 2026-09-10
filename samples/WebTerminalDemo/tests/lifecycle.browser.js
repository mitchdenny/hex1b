async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1100 } });
  const test = await context.newPage();
  const errors = [];
  const instances = new Set();
  const results = [];
  const sockets = [];
  test.on("pageerror", error => errors.push(error.message));
  test.on("websocket", socket => sockets.push(socket.url()));
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const tile = id => test.locator(`.terminal-window[data-view="${id}"]`);
  const waitConnected = id => test.waitForFunction(id => {
    const view = webTerminalViews.get(id);
    return view?.phase === "connected" && view.terminal?.connected && view.stats.revision > 0;
  }, id);
  const waitClosed = id => test.waitForFunction(id => {
    const view = webTerminalViews.get(id);
    return view?.phase === "closed" && view.closure?.close;
  }, id);
  const lastId = () => test.evaluate(() => [...webTerminalViews.keys()].at(-1));
  const closure = id => test.evaluate(id => webTerminalViews.get(id).closure, id);
  const reconnect = async id => {
    const oldConnection = await test.evaluate(id => webTerminalViews.get(id).connectionId, id);
    await tile(id).locator(".reconnect-view").click();
    await waitConnected(id);
    check(await tile(id).locator(".closed-overlay").isHidden(), "Reconnect retained the closed overlay");
    check(await test.evaluate(({ id, oldConnection }) => {
      const view = webTerminalViews.get(id);
      return view.connectionId !== oldConnection && !view.element.querySelector(".terminal-mount").inert &&
        !view.terminal.peer.isPrimary;
    }, { id, oldConnection }), "Reconnect reused a lease, disabled input, or claimed primary");
  };
  try {
    for (const transport of ["direct", "hmp1"]) {
      await test.goto(`${origin}/?empty=1&renderer=webgl2&transport=${transport}`);
      await test.locator("#scene").selectOption("text");
      const created = test.waitForResponse(response => response.url() === `${origin}/api/terminals` &&
        response.request().method() === "POST" && response.status() === 201);
      await test.locator("#create").click();
      const instanceId = (await (await created).json()).id;
      instances.add(instanceId);
      await waitConnected("1");
      await test.locator("#attach").click();
      await waitConnected("2");
      const target = tile("2");
      const viewId = await test.evaluate(() => webTerminalViews.get("2").connectionId);
      const controlUrl = `${origin}/api/terminals/${instanceId}/views/${viewId}/failure`;
      check((await test.request.post(controlUrl, { headers: { Origin: origin }, data: { mode: "unknown" } })).status() === 400,
        "Unknown failure mode was not rejected");
      check((await test.request.post(controlUrl, { headers: { Origin: "https://example.invalid" }, data: { mode: "close" } })).status() === 403,
        "Cross-origin failure injection was not rejected");

      for (const [mode, code, reason] of [
        ["close", 1000, "Demo: graceful view closure"],
        ["abort", 1006, ""],
        ["policy", 1008, "Demo: policy violation"],
        ["server-error", 1011, "Demo: server failure"]
      ]) {
        const frames = await test.evaluate(() => webTerminalViews.get("1").stats.frames);
        const connections = sockets.length;
        await target.locator(".view-failure").selectOption(mode);
        await target.locator(".trigger-failure").click();
        await waitClosed("2");
        const state = await closure("2");
        check(state.close.code === code && state.close.reason === reason && state.close.wasClean === (code !== 1006),
          `${transport}/${mode}: wrong close details ${JSON.stringify(state)}`);
        check(state.reconnect && await target.locator(".closed-overlay").isVisible(), "Missing persistent reconnectable overlay");
        check(await target.locator(".take-primary").isDisabled() &&
          await target.locator(".trigger-failure").isDisabled() && await target.locator(".resync").isDisabled(),
        "Closed view controls remained enabled");
        check(await test.evaluate(() => {
          const view = webTerminalViews.get("2");
          const mount = view.element.querySelector(".terminal-mount");
          const overlay = view.element.querySelector(".closed-overlay");
          const a = mount.getBoundingClientRect(), b = overlay.getBoundingClientRect();
          return mount.inert && mount.childElementCount === 0 && !view.terminal.connected &&
            Math.abs(a.x - b.x) < 1 && Math.abs(a.y - b.y) < 1 &&
            Math.abs(a.width - b.width) < 1 && Math.abs(a.height - b.height) < 1;
        }), "Closed overlay did not cover, disable, and dispose the terminal");
        await test.waitForFunction(frames => {
          const peer = webTerminalViews.get("1");
          return peer.terminal.connected && peer.stats.frames > frames;
        }, frames);
        check(sockets.length === connections, "A closed view automatically reconnected");
        const registry = await (await test.request.get(`${origin}/api/terminals`)).json();
        check(registry.some(instance => instance.id === instanceId), "View failure killed producer");
        results.push({ transport, mode, ...state.close });
        await reconnect("2");
      }

      for (const [mode, code, reason] of [
        ["before-frame-close", 1000, "Demo: closed before first frame"],
        ["before-frame-abort", 1006, ""],
        ["reject-upgrade", 1006, ""]
      ]) {
        await test.locator("#failure").selectOption(mode);
        await test.locator("#attach").click();
        const id = await lastId();
        await waitClosed(id);
        const state = await closure(id);
        check(state.close.code === code && state.close.reason === reason && state.close.wasClean === (code === 1000),
          `${transport}/${mode}: wrong pre-mount close ${JSON.stringify(state)}`);
        check(state.detail.startsWith("Before mounting completed"), "Pre-mount closure was mislabeled");
        check(await test.evaluate(id => !webTerminalViews.get(id).terminal, id), "Pre-frame failure unexpectedly mounted");
        results.push({ transport, mode, ...state.close });
        // Retry intentionally ignores the new-view failure selector.
        await reconnect(id);
        await tile(id).locator(".close-view").click();
        await test.waitForFunction(id => !webTerminalViews.has(id), id);
      }
      await test.locator("#failure").selectOption("");

      // Local initialization failure has no fabricated native WebSocket details.
      await test.route("**/web-terminal/terminal-worker.js", route => route.abort());
      await test.locator("#attach").click();
      const failedMount = await lastId();
      await test.waitForFunction(id => webTerminalViews.get(id)?.phase === "closed", failedMount);
      const localFailure = await closure(failedMount);
      check(!localFailure.close && localFailure.reconnect && await tile(failedMount).locator(".closed-overlay").isVisible(),
        "Local mount failure fabricated a native close or failed to show its overlay");
      await test.unroute("**/web-terminal/terminal-worker.js");
      await reconnect(failedMount);
      await tile(failedMount).locator(".close-view").click();

      // Owner completion is terminal-wide, unlike any injected transport fault.
      test.once("dialog", dialog => dialog.accept());
      await test.locator("#terminate").click();
      await waitClosed("1");
      await waitClosed("2");
      for (const id of ["1", "2"]) {
        const state = await closure(id);
        check(state.close.code === 4000 && state.close.wasClean && /owner/i.test(state.close.reason) && !state.reconnect,
          `Owner termination was not authoritative: ${JSON.stringify(state)}`);
        check(await tile(id).locator(".closed-title").innerText() === "Terminal ended" &&
          await tile(id).locator(".reconnect-view").isDisabled(), "Ended tile was removed or allowed reconnect");
        await tile(id).locator(".view-title").click();
        await tile(id).locator(".dismiss-view").click();
      }
      await test.waitForFunction(() => webTerminalViews.size === 0);
      check(!(await (await test.request.get(`${origin}/api/terminals`)).json()).some(item => item.id === instanceId),
        "Ended producer retained its leases");
      instances.delete(instanceId);
    }

    await test.locator("#scene").selectOption("shell");
    const shellCreated = test.waitForResponse(response => response.url() === `${origin}/api/terminals` &&
      response.request().method() === "POST" && response.status() === 201);
    await test.locator("#create").click();
    const shellId = (await (await shellCreated).json()).id;
    instances.add(shellId);
    const shellView = await lastId();
    await waitConnected(shellView);
    await test.locator("#transport").selectOption("direct");
    await test.locator("#attach").click();
    const shellPeer = await lastId();
    await waitConnected(shellPeer);
    await test.evaluate(id => {
      const terminal = webTerminalViews.get(id).terminal;
      terminal.focus();
      terminal.paste("echo __LIFECYCLE_READY__");
    }, shellView);
    await test.keyboard.press("Enter");
    await test.waitForFunction(id => webTerminalViews.get(id).text.split("\n")
      .some(line => line.trim() === "__LIFECYCLE_READY__"), shellView);
    await test.evaluate(id => {
      const terminal = webTerminalViews.get(id).terminal;
      terminal.focus();
      terminal.paste("exit 7");
    }, shellView);
    await test.keyboard.press("Enter");
    for (const id of [shellView, shellPeer]) {
      await waitClosed(id);
      const state = await closure(id);
      check(state.close.code === 4000 && state.close.wasClean && /code 7/.test(state.close.reason) && !state.reconnect,
        `Natural workload exit was misreported: ${JSON.stringify(state)}`);
      results.push({ mode: "workload-exit", ...state.close });
      await tile(id).locator(".view-title").click();
      await tile(id).locator(".dismiss-view").click();
    }
    await test.waitForFunction(() => webTerminalViews.size === 0);
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, results, browserErrors: errors };
  } catch (error) {
    const state = await test.evaluate(() => [...webTerminalViews.values()].map(view => ({
      id: view.id, phase: view.phase, closure: view.closure,
      revision: view.stats.revision, frames: view.stats.frames, text: view.text.slice(-500),
      status: view.element.querySelector(".view-status").textContent
    })));
    throw new Error(`${error.message}\nCompleted: ${JSON.stringify(results)}\nViews: ${JSON.stringify(state)}\nBrowser errors: ${errors.join("; ")}`);
  } finally {
    try {
      for (const id of instances) await test.request.delete(`${origin}/api/terminals/${id}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
