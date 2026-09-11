async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({ viewport: { width: 1200, height: 1000 } });
  const test = await context.newPage();
  const errors = [];
  let instanceId;
  test.on("pageerror", error => errors.push(error.message));
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const emit = async (sequence, title, marker) => {
    await test.evaluate(command => {
      titleViews.direct.terminal.focus();
      titleViews.direct.terminal.paste(command);
    }, `printf '${sequence}\\r\\n${marker}\\r\\n'`);
    await test.keyboard.press("Enter");
    await test.waitForFunction(({ title, marker }) => Object.values(titleViews)
      .filter(view => !view.disposed).every(view =>
        view.terminal.title === title && view.terminal.screenText.split("\n").some(line => line.trim() === marker)),
    { title, marker }, { timeout: 30000 });
  };
  try {
    await test.goto(`${origin}/health`);
    const created = await test.request.post(`${origin}/api/terminals`, {
      headers: { Origin: origin }, data: { scene: "shell", columns: 80, rows: 24 }
    });
    check(created.status() === 201, `Shell creation failed: ${created.status()}`);
    instanceId = (await created.json()).id;
    await test.evaluate(async id => {
      const { WebTerminal } = await import("/web-terminal/index.js");
      document.title = "Host-owned title";
      document.body.replaceChildren();
      window.titleViews = {};
      window.mountTitleView = async (name, transport) => {
        const header = document.createElement("h2");
        const container = document.createElement("div");
        container.style.cssText = "width:800px;height:240px";
        document.body.append(header, container);
        const view = { events: [], header, container, terminal: null, disposed: false };
        titleViews[name] = view;
        view.terminal = await WebTerminal.mount(container, {
          url: `/ws?instance=${id}&transport=${transport}`, renderer: "webgl2",
          sizing: { mode: "fixed", columns: 80, rows: 24 },
          onTitleChange(title) {
            if (view.terminal && view.terminal.title !== title) throw new Error("Getter lags callback");
            view.events.push(title);
            header.textContent = title || "Resource fallback";
          }
        });
        if (view.events.length !== 1 || view.events[0] !== view.terminal.title)
          throw new Error("Mount did not deliver exactly one authoritative initial title");
      };
      await mountTitleView("direct", "direct");
      titleViews.direct.terminal.requestPrimary();
      await mountTitleView("relay", "hmp1");
    }, instanceId);
    await test.waitForFunction(() => titleViews.direct.terminal.peer.isPrimary, null, { timeout: 30000 });
    // Avoid user-shell prompt hooks changing the title under test.
    await test.evaluate(() => {
      titleViews.direct.terminal.focus();
      titleViews.direct.terminal.paste("exec /bin/sh");
    });
    await test.keyboard.press("Enter");
    await emit("\\033]0;\\007", "", "__TITLE_CLEAR__");
    await emit("\\033]2;first;title\\033\\\\", "first;title", "__TITLE_FIRST__");
    await test.evaluate(() => mountTitleView("late", "hmp1"));
    check(await test.evaluate(() => titleViews.late.events[0] === "first;title"),
      "Late relay lost the existing title");

    const before = await test.evaluate(() => Object.fromEntries(Object.entries(titleViews)
      .map(([name, view]) => [name, view.events.length])));
    await emit("\\033]2;first;title\\007\\033]1;icon-only\\007", "first;title", "__TITLE_DUPLICATE__");
    await emit("\\033c", "first;title", "__TITLE_RESET__");
    check(await test.evaluate(before => Object.entries(titleViews).every(([name, view]) =>
      view.events.length === before[name]), before), "Duplicate, icon-only, or reset output notified a title change");
    const revisions = await test.evaluate(() => Object.fromEntries(Object.entries(titleViews).map(([name, view]) => {
      const revision = view.terminal.stats.revision;
      view.terminal.resync();
      return [name, revision];
    })));
    await test.waitForFunction(revisions => Object.entries(titleViews).every(([name, view]) =>
      view.terminal.stats.revision > revisions[name]), revisions, { timeout: 30000 });
    check(await test.evaluate(before => Object.entries(titleViews).every(([name, view]) =>
      view.events.length === before[name]), before), "Same-title resync notified again");

    await emit("\\033]0;<b>plain;title</b>\\007", "<b>plain;title</b>", "__TITLE_TEXT__");
    check(await test.evaluate(() => document.title === "Host-owned title" &&
      Object.values(titleViews).every(view => view.header.childElementCount === 0 &&
        view.header.textContent === "<b>plain;title</b>")), "Title was treated as markup or changed host chrome");
    await test.evaluate(() => {
      titleViews.relay.terminal.dispose();
      titleViews.relay.disposed = true;
      titleViews.relay.countAtDisposal = titleViews.relay.events.length;
    });
    await emit("\\033]2;\\007", "", "__TITLE_EMPTY__");
    check(await test.evaluate(() => titleViews.relay.events.length === titleViews.relay.countAtDisposal &&
      titleViews.relay.terminal.title === "<b>plain;title</b>" &&
      titleViews.direct.header.textContent === "Resource fallback"), "Disposal or fallback contract failed");
    await test.evaluate(() => {
      titleViews.late.terminal.dispose();
      titleViews.late.disposed = true;
    });
    await test.evaluate(() => mountTitleView("reconnected", "hmp1"));
    check(await test.evaluate(() => titleViews.reconnected.events.length === 1 &&
      titleViews.reconnected.events[0] === ""), "Reconnected relay did not replay an explicit empty title");
    await emit("\\033]2;after-reconnect\\007", "after-reconnect", "__TITLE_RECONNECTED__");
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, views: await test.evaluate(() => Object.fromEntries(
      Object.entries(titleViews).map(([name, view]) => [name, view.events]))) };
  } finally {
    try {
      if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
