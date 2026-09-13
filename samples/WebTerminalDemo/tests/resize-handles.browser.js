async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0];
  if (!origin) throw new Error("Navigate to the running WebTerminalDemo before invoking this fixture.");
  const context = await page.context().browser().newContext({ viewport: { width: 1600, height: 1200 } });
  const test = await context.newPage();
  const instances = new Set();
  const errors = [];
  const commands = [];
  const results = [];
  const screenshots = [];
  let stage = "starting";
  test.on("pageerror", error => errors.push(error.message));
  test.on("websocket", socket => socket.on("framesent", frame => {
    if (typeof frame.payload === "string") {
      const command = JSON.parse(frame.payload);
      if (command.type === "resize") commands.push({ url: socket.url(), ...command });
    }
  }));
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const near = (actual, expected) => Math.abs(actual - expected) < 1;
  const windowFor = id => test.locator(`.terminal-window[data-view="${id}"]`);
  const handleFor = (id, edge) => windowFor(id).locator(`.resize-handle[data-edge="${edge}"]`);
  const box = id => windowFor(id).evaluate(element => ({
    left: element.offsetLeft, top: element.offsetTop, width: element.offsetWidth, height: element.offsetHeight
  }));
  const point = async (id, edge) => {
    await test.evaluate(id => webTerminalViews.get(id).terminal.focus(), id);
    const bounds = await windowFor(id).boundingBox();
    return {
      x: edge.includes("w") ? bounds.x - 3 : edge.includes("e") ? bounds.x + bounds.width + 3 : bounds.x + bounds.width / 2,
      y: edge.includes("n") ? bounds.y - 3 : edge.includes("s") ? bounds.y + bounds.height + 3 : bounds.y + bounds.height / 2
    };
  };
  const drag = async (id, edge, dx, dy) => {
    const start = await point(id, edge);
    await test.mouse.move(start.x, start.y);
    await test.mouse.down();
    await test.mouse.move(start.x + dx, start.y + dy, { steps: 5 });
    check(await handleFor(id, edge).getAttribute("data-active") === "true", `${edge} did not capture the resize gesture`);
    await test.mouse.up();
    check(await handleFor(id, edge).getAttribute("data-active") === null, `${edge} stayed active after release`);
  };
  const setSize = async (id, width, height) => {
    const before = await box(id);
    await drag(id, "se", width - before.width, height - before.height);
    const after = await box(id);
    check(near(after.width, width) && near(after.height, height), `Expected ${width}x${height}: ${JSON.stringify(after)}`);
  };
  const waitAuto = async () => test.waitForFunction(() => {
    const primary = [...webTerminalViews.values()].find(view => view.terminal?.peer.isPrimary);
    if (!primary || primary.terminal.sizing.mode !== "auto") return false;
    const mount = primary.element.querySelector(".terminal-mount").getBoundingClientRect();
    const scale = primary.terminal.sizing.fontSize / 16;
    const columns = Math.max(20, Math.min(300, Math.floor(mount.width / (10 * scale))));
    const rows = Math.max(10, Math.min(100, Math.floor(mount.height / (20 * scale))));
    return [...webTerminalViews.values()].every(view => view.terminal.geometry.columns === columns &&
      view.terminal.geometry.rows === rows && view.stats.columns === columns && view.stats.rows === rows);
  });
  const metadata = async id => (await (await test.request.get(`${origin}/api/terminals`)).json())
    .find(instance => instance.id === id);
  const highlight = async (id, edge, opacity) => test.waitForFunction(({ id, edge, opacity }) => {
    const element = webTerminalViews.get(id).element.querySelector(`.resize-handle[data-edge="${edge}"]`);
    return Math.abs(Number(getComputedStyle(element, "::after").opacity) - opacity) < .01;
  }, { id, edge, opacity });
  const clearPointer = async () => { await test.mouse.move(5, 5); };
  try {
    for (const transport of ["direct", "hmp1"]) {
      stage = `${transport}: creating primary`;
      await test.goto(`${origin}/?empty=1&scene=text&renderer=webgl2&transport=${transport}`);
      const created = test.waitForResponse(response => response.url() === `${origin}/api/terminals` &&
        response.request().method() === "POST" && response.status() === 201);
      await test.locator("#create").click();
      const instanceId = (await (await created).json()).id;
      instances.add(instanceId);
      await test.waitForFunction(() => webTerminalViews.get("1")?.terminal?.peer.isPrimary &&
        webTerminalViews.get("1").stats.gpu === "ready");
      await test.request.post(`${origin}/api/terminals/${instanceId}/controls`, {
        headers: { Origin: origin }, data: { paused: true }
      });
      await setSize("1", 600, 400);
      const title = await windowFor("1").locator(".view-title").boundingBox();
      const initial = await box("1");
      await test.mouse.move(title.x + 40, title.y + title.height / 2);
      await test.mouse.down();
      await test.mouse.move(title.x + 40 + 120 - initial.left, title.y + title.height / 2 + 100 - initial.top, { steps: 5 });
      await test.mouse.up();
      const positioned = await box("1");
      check(near(positioned.left, 120) && near(positioned.top, 100) &&
        positioned.width === 600 && positioned.height === 400, "Titlebar drag changed size or failed to move");
      await waitAuto();
      const originalProducer = await metadata(instanceId);
      const edges = { n: "ns-resize", ne: "nesw-resize", e: "ew-resize", se: "nwse-resize",
        s: "ns-resize", sw: "nesw-resize", w: "ew-resize", nw: "nwse-resize" };
      check(await windowFor("1").locator(".resize-handle").count() === 8, "Expected exactly eight resize handles");
      for (const [edge, cursor] of Object.entries(edges)) {
        stage = `${transport}: hover and physical ${edge} drag`;
        await clearPointer();
        await highlight("1", edge, .45);
        const restingColor = await handleFor("1", edge).evaluate(element => getComputedStyle(element, "::after").borderColor);
        const start = await point("1", edge);
        await test.mouse.move(start.x, start.y);
        await highlight("1", edge, 1);
        check(await handleFor("1", edge).evaluate(element => getComputedStyle(element).cursor) === cursor,
          `${edge} cursor was not ${cursor}`);
        check(await handleFor("1", edge).evaluate(element => getComputedStyle(element, "::after").borderColor) !== restingColor,
          `${edge} hover did not use accent color`);
        check(await test.evaluate(({ x, y, edge }) => document.elementFromPoint(x, y)?.getAttribute("data-edge") === edge,
          { ...start, edge }), `${edge} outer hitbox is clipped or obscured`);
        if (transport === "direct" && ["n", "ne"].includes(edge)) {
          const path = `.playwright-cli/resize-handles-hover-${edge}.png`;
          await test.screenshot({ path });
          screenshots.push(path);
        }
        const before = await box("1");
        const dx = edge.includes("w") ? -25 : edge.includes("e") ? 25 : 0;
        const dy = edge.includes("n") ? -20 : edge.includes("s") ? 20 : 0;
        await test.mouse.down();
        await test.mouse.move(start.x + dx, start.y + dy, { steps: 5 });
        await highlight("1", edge, 1);
        check(await handleFor("1", edge).getAttribute("data-active") === "true", `${edge} lost active highlight`);
        check(await windowFor("1").locator(".view-titlebar").getAttribute("data-active") === null,
          `${edge} resize was stolen by titlebar dragging`);
        const after = await box("1");
        check(near(after.width, before.width + Math.abs(dx)) && near(after.height, before.height + Math.abs(dy)),
          `${edge} resized wrong axes: ${JSON.stringify({ before, after })}`);
        check(near(after.left + (edge.includes("w") ? after.width : 0), before.left + (edge.includes("w") ? before.width : 0)) &&
          near(after.top + (edge.includes("n") ? after.height : 0), before.top + (edge.includes("n") ? before.height : 0)),
        `${edge} did not preserve opposite bounds`);
        await test.mouse.up();
        await clearPointer();
        await highlight("1", edge, .45);
        await drag("1", edge, -dx, -dy);
      }

      stage = `${transport}: captured pointer outside handle and maximum/minimum constraints`;
      let start = await point("1", "se");
      await test.mouse.move(start.x, start.y);
      await test.mouse.down();
      await test.mouse.move(start.x + 4000, start.y + 3000, { steps: 5 });
      let constrained = await box("1");
      check(constrained.width === 3200 && constrained.height === 2200, "Maximum window size not enforced");
      await test.mouse.move(start.x - 1000, start.y - 800, { steps: 5 });
      constrained = await box("1");
      check(constrained.width === 240 && constrained.height === 180, "Minimum window size not enforced");
      check(await handleFor("1", "se").getAttribute("data-active") === "true", "Captured pointer outside handle lost activation");
      await highlight("1", "se", 1);
      await test.mouse.move(start.x, start.y, { steps: 5 });
      await test.mouse.up();
      check((await box("1")).width === 600 && (await box("1")).height === 400, "Captured drag failed to restore dimensions");
      await clearPointer();
      await highlight("1", "se", .45);

      stage = `${transport}: north/west workspace origin constraints`;
      const beforeOrigin = await box("1");
      await drag("1", "nw", -1000, -1000);
      const atOrigin = await box("1");
      check(atOrigin.left === 0 && atOrigin.top === 0 &&
        atOrigin.left + atOrigin.width === beforeOrigin.left + beforeOrigin.width &&
        atOrigin.top + atOrigin.height === beforeOrigin.top + beforeOrigin.height,
      "North/west failed to clamp origin while preserving opposite bounds");
      const topTitle = await windowFor("1").locator(".view-title").boundingBox();
      await test.mouse.move(topTitle.x + 40, topTitle.y + topTitle.height / 2);
      await test.mouse.down();
      await test.mouse.move(topTitle.x + 160, topTitle.y + topTitle.height / 2 + 100, { steps: 5 });
      await test.mouse.up();
      await setSize("1", 600, 400);

      for (const ending of ["pointercancel", "lostpointercapture"]) {
        stage = `${transport}: ${ending} clears active gesture`;
        const handle = handleFor("1", "e");
        await handle.evaluate(element => element.addEventListener("gotpointercapture",
          event => { element.dataset.testPointerId = String(event.pointerId); }, { once: true }));
        start = await point("1", "e");
        await test.mouse.move(start.x, start.y);
        await test.mouse.down();
        await test.mouse.move(start.x + 20, start.y + 10);
        await handle.evaluate((element, ending) => {
          const pointerId = Number(element.dataset.testPointerId);
          if (!element.hasPointerCapture(pointerId)) throw new Error("Real pointer was not captured");
          if (ending === "pointercancel")
            element.dispatchEvent(new PointerEvent("pointercancel", { pointerId, bubbles: true }));
          else element.releasePointerCapture(pointerId);
        }, ending);
        await test.mouse.move(start.x - 50, start.y + 100);
        check(await handle.getAttribute("data-active") === null, `${ending} left stale active state`);
        check((await box("1")).width === 620, `${ending} did not stop geometry updates`);
        await test.mouse.up();
        await clearPointer();
        await highlight("1", "e", .45);
        await setSize("1", 600, 400);
      }

      stage = `${transport}: primary Auto controls and resize authority`;
      const fontBefore = await test.evaluate(() => webTerminalViews.get("1").terminal.sizing.fontSize);
      const chromeBefore = await box("1");
      await windowFor("1").locator(".font-smaller").click();
      await test.waitForFunction(size => webTerminalViews.get("1").terminal.sizing.fontSize === size - 1, fontBefore);
      check(JSON.stringify(await box("1")) === JSON.stringify(chromeBefore), "Border control click moved/resized window");
      await drag("1", "e", 100, 0);
      await waitAuto();
      const afterAuto = await metadata(instanceId);
      check(afterAuto.columns !== originalProducer.columns && commands.some(command => command.url.includes(`transport=${transport}`)),
        "Primary Auto resize did not reach producer through selected transport");

      stage = `${transport}: fixed grid changes view size only`;
      await windowFor("1").locator(".view-resolution").selectOption("80x24");
      await test.waitForFunction(() => webTerminalViews.get("1").stats.columns === 80 && webTerminalViews.get("1").stats.rows === 24);
      const fixedCount = commands.length;
      await drag("1", "se", -60, -40);
      await test.waitForTimeout(250);
      const afterFixed = await metadata(instanceId);
      check(afterFixed.columns === 80 && afterFixed.rows === 24 && commands.length === fixedCount,
        "Fixed grid resize changed the producer");

      stage = `${transport}: secondary resize and close control`;
      await windowFor("1").locator(".thumbnail").click();
      await test.waitForFunction(() => webTerminalViews.get("2")?.terminal?.connected && webTerminalViews.get("2").stats.gpu === "ready");
      const secondaryCount = commands.length;
      const secondaryBefore = await box("2");
      await drag("2", "sw", -50, 40);
      const secondaryAfter = await box("2");
      check(secondaryAfter.width === secondaryBefore.width + 50 && secondaryAfter.height === secondaryBefore.height + 40,
        "Secondary view did not physically resize");
      await test.waitForTimeout(250);
      const afterSecondary = await metadata(instanceId);
      check(afterSecondary.columns === 80 && afterSecondary.rows === 24 && commands.length === secondaryCount,
        "Secondary view resize emitted a producer resize");
      await windowFor("2").locator(".close-view").click();
      await test.waitForFunction(() => webTerminalViews.size === 1);
      check(await windowFor("1").locator("[data-active=true]").count() === 0, "Controls left active drag handles");

      stage = `${transport}: workspace scrolling during captured resize`;
      await setSize("1", 1800, 1000);
      await test.locator("#workspace").evaluate(element => element.scrollTo(0, 0));
      const beforeScroll = await box("1");
      start = await point("1", "nw");
      await test.mouse.move(start.x, start.y);
      await test.mouse.down();
      await test.locator("#workspace").evaluate(element => element.scrollTo(40, 30));
      await test.waitForFunction(() => {
        const workspace = document.getElementById("workspace");
        return workspace.scrollLeft === 40 && workspace.scrollTop === 30;
      });
      await test.mouse.move(start.x + 1, start.y + 1);
      const afterScroll = await box("1");
      check(afterScroll.left === beforeScroll.left + 41 && afterScroll.top === beforeScroll.top + 31 &&
        afterScroll.width === beforeScroll.width - 41 && afterScroll.height === beforeScroll.height - 31,
      `Captured resize ignored workspace scroll delta: ${JSON.stringify({ beforeScroll, afterScroll })}`);
      await test.mouse.up();
      check(await handleFor("1", "nw").getAttribute("data-active") === null, "Scrolled gesture did not clean up");
      check(await test.evaluate(() => webTerminalViews.get("1").stats.gpu === "ready" &&
        webTerminalViews.get("1").stats.renderer === "webgl2" && webTerminalViews.get("1").stats.warnings.length === 0),
      "GPU worker diagnostics failed");
      results.push({ transport, edges: Object.keys(edges), constraints: "240x180..3200x2200",
        pointerCancel: true, lostCapture: true, workspaceScroll: true,
        primaryAuto: true, fixedAndSecondaryViewOnly: true });
      await test.goto(`${origin}/health`);
      const deleted = await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
      check(deleted.status() === 204, "Could not delete fixture producer");
      instances.delete(instanceId);
    }
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, results, screenshots, browserErrors: errors };
  } catch (error) {
    const state = await test.evaluate(() => [...(window.webTerminalViews?.values() ?? [])].map(view => ({
      id: view.id, bounds: view.element.getBoundingClientRect().toJSON(), style: view.element.style.cssText,
      active: [...view.element.querySelectorAll("[data-active=true]")].map(element => element.getAttribute("data-edge")),
      geometry: view.terminal?.geometry, peer: view.terminal?.peer
    })));
    throw new Error(`${stage}: ${error.message}; ${JSON.stringify(state)}`);
  } finally {
    try {
      for (const id of instances)
        await test.request.delete(`${origin}/api/terminals/${id}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
