async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({
    viewport: { width: 1440, height: 1000 }, deviceScaleFactor: 2
  });
  const test = await context.newPage();
  const errors = [];
  let instanceId;
  let stage = "mounting real shell and worker";
  test.on("pageerror", error => errors.push(error.message));
  const check = (value, message) => { if (!value) throw new Error(message); };
  const shell = async (command, marker) => {
    await test.evaluate(() => primary.focus());
    await test.keyboard.press("Control+u");
    await test.evaluate(command => primary.paste(command), command);
    await test.keyboard.press("Enter");
    await test.waitForFunction(marker => primary.screenText.split("\n").some(line => line.trim() === marker) &&
      /[\u276f$#%>]$/.test(primary.screenText.trimEnd()), marker, { timeout: 30000 });
  };
  const cell = (name, x, y) => test.evaluate(({ name, x, y }) => {
    const terminal = window[name];
    const box = terminal.element.shadowRoot.querySelector("canvas").getBoundingClientRect();
    return { x: box.x + (x + .5) * box.width / terminal.geometry.columns,
      y: box.y + (y + .5) * box.height / terminal.geometry.rows };
  }, { name, x, y });
  const click = async (name, x, y, modifier = "Control") => {
    const position = await cell(name, x, y);
    await test.mouse.move(position.x, position.y);
    if (modifier) await test.keyboard.down(modifier);
    await test.mouse.click(position.x, position.y);
    if (modifier) await test.keyboard.up(modifier);
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
      document.body.innerHTML = '<div id="a" style="width:800px;height:480px"></div><div id="b" style="width:400px;height:240px"></div>';
      window.commands = [];
      const RealWorker = window.Worker;
      window.Worker = class extends RealWorker {
        postMessage(message, ...args) {
          if (message.type === "command") commands.push(message.command);
          super.postMessage(message, ...args);
        }
      };
      window.primary = await WebTerminal.mount(document.getElementById("a"), {
        url: `/ws?instance=${id}`, sizing: { mode: "fixed", columns: 80, rows: 24 }
      });
      primary.requestPrimary();
      window.secondary = await WebTerminal.mount(document.getElementById("b"), {
        url: `/ws?instance=${id}`, readOnly: true
      });
    }, instanceId);
    await test.waitForFunction(() => primary.peer.isPrimary && primary.geometry.columns === 80 &&
      /[\u276f$#%>]$/.test(primary.screenText.trimEnd()), null, { timeout: 30000 });
    const target = `${origin}/health?hyperlink=first`;
    const osc = (uri, text) => `\\033]8;;${uri}\\033\\\\${text}\\033]8;;\\033\\\\`;
    await shell(`printf '\\033[2J\\033[H${osc(target, "LINK")}\\r\\n${osc("javascript:alert(1)", "BLOCKED")}\\r\\n__LINKS_READY__\\r\\n'`,
      "__LINKS_READY__");

    stage = "real Cmd-click navigation";
    const position = await cell("primary", 1, 0);
    await test.mouse.move(position.x, position.y);
    const canvas = test.locator("#a canvas");
    check((await canvas.getAttribute("title")).includes(target), "Presented OSC 8 destination was lost");
    await test.keyboard.down("Meta");
    check(await canvas.evaluate(element => element.style.cursor === "pointer"), "Link affordance missing");
    const nextPage = context.waitForEvent("page");
    const request = context.waitForEvent("request", request => request.url() === target);
    await test.mouse.click(position.x, position.y);
    await test.keyboard.up("Meta");
    const popup = await nextPage;
    await popup.waitForURL(target);
    await popup.waitForLoadState("domcontentloaded");
    check(await popup.evaluate(() => window.opener === null), "Opened link retained an opener");
    check((await request).headers().referer === undefined, "Opened link leaked a referrer");
    await popup.close();

    await test.evaluate(() => {
      window.opened = [];
      window.open = (...args) => { opened.push(args); return null; };
    });
    stage = "selection, unsafe URLs, and read-only thumbnails";
    await click("primary", 1, 1);
    check(await test.evaluate(() => opened.length === 0), "Script URI activated");
    await click("primary", 1, 0, null);
    await test.waitForFunction(() => primary.selection.active && !primary.selection.pending);
    const selected = await test.evaluate(() => primary.selection.text);
    await click("primary", 1, 0);
    check(await test.evaluate(text => opened.length === 1 && primary.selection.text === text, selected),
      "Ctrl-click changed the retained selection");
    await click("secondary", 1, 0);
    check(await test.evaluate(() => opened.length === 2 && opened.every(args =>
      args[1] === "_blank" && args[2] === "noopener,noreferrer")), "Read-only thumbnail link did not activate safely");

    stage = "application capture and drag cancellation";
    await shell("printf '\\033[?1003;1006h__CAPTURE_READY__\\r\\n'", "__CAPTURE_READY__");
    await test.waitForFunction(() => primary.geometry.mouseTracking === 1003);
    const captured = await cell("primary", 1, 0);
    await test.mouse.move(captured.x, captured.y);
    await test.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    await test.evaluate(() => { commands.length = 0; opened.length = 0; });
    await test.keyboard.down("Control");
    await test.mouse.down();
    await test.mouse.up();
    await test.keyboard.up("Control");
    check(await test.evaluate(() => opened.length === 1 && !commands.some(command =>
      command.type === "mouse" || command.type === "selection")), "Link gesture leaked into terminal input");
    await test.keyboard.down("Control");
    await test.mouse.down();
    await test.mouse.move(captured.x + 20, captured.y);
    await test.mouse.move(captured.x, captured.y);
    await test.mouse.up();
    await test.keyboard.up("Control");
    check(await test.evaluate(() => opened.length === 1), "Dragged link activated");

    stage = "destination-only updates";
    const replacement = `${origin}/health?hyperlink=replaced`;
    await shell(`printf '\\033[?1003l\\033[s\\033[H${osc(replacement, "LINK")}\\033[u__REPLACED__\\r\\n'`, "__REPLACED__");
    await click("primary", 1, 0);
    check(await test.evaluate(uri => opened.at(-1)[0] === uri, replacement), "Destination-only change left stale hit testing");

    stage = "scrollback destinations";
    await shell("i=0; while [ \"$i\" -lt 40 ]; do printf 'history row\\r\\n'; i=$((i+1)); done; printf '__SCROLLED__\\r\\n'", "__SCROLLED__");
    await test.evaluate(() => secondary.scrollLines(-10000));
    await test.waitForFunction(() => !secondary.viewport.pending && secondary.screenText.split("\n").some(line => line === "LINK"));
    const row = await test.evaluate(() => secondary.screenText.split("\n").findIndex(line => line === "LINK"));
    await click("secondary", 1, row);
    check(await test.evaluate(uri => opened.at(-1)[0] === uri, replacement), "Historical link used live viewport coordinates");
    await test.evaluate(() => secondary.scrollToLive());
    await test.waitForFunction(() => secondary.viewport.following && !secondary.viewport.pending);
    const before = await test.evaluate(() => opened.length);
    await click("secondary", 1, 0);
    check(await test.evaluate(before => opened.length === before, before), "Return to live retained old links");
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, covered: ["real OSC 8/worker delivery", "Cmd/Ctrl activation", "noopener/noreferrer",
      "unsafe schemes", "selection preservation", "read-only thumbnail", "mouse capture", "drag cancellation",
      "destination-only updates", "scrollback and return to live"] };
  } catch (error) {
    const text = await test.evaluate(() => window.primary?.screenText);
    throw new Error(`${stage}: ${error.message}; ${text}`);
  } finally {
    try {
      if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
