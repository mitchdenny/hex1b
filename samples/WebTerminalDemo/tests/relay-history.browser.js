async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({ viewport: { width: 1700, height: 1100 } });
  const test = await context.newPage();
  const errors = [];
  let instanceId, stage = "creating producer";
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  test.on("pageerror", error => errors.push(error.message));
  try {
    await test.goto(`${origin}/health`);
    const created = await test.request.post(`${origin}/api/terminals`, {
      headers: { Origin: origin }, data: { scene: "shell", columns: 80, rows: 24 }
    });
    check(created.status() === 201, `Create returned ${created.status()}`);
    instanceId = (await created.json()).id;
    await test.evaluate(async id => {
      const { WebTerminal } = await import("/web-terminal/index.js");
      document.body.replaceChildren();
      document.body.style.cssText = "display:grid;grid-template-columns:800px 800px;gap:16px";
      window.mountHistoryView = async (name, transport) => {
        const host = document.createElement("div");
        host.style.cssText = "width:800px;height:480px";
        document.body.append(host);
        return WebTerminal.mount(host, {
          url: `/ws?instance=${id}&name=${name}&transport=${transport}`, renderer: "webgl2",
          sizing: { mode: "fixed", columns: 80, rows: 24 }
        });
      };
      window.directHistory = await mountHistoryView("direct", "direct");
      window.relayHistory = await mountHistoryView("relay", "hmp1");
      directHistory.requestPrimary();
    }, instanceId);
    await test.waitForFunction(() => directHistory.peer.isPrimary &&
      /[\u276f$#%>]$/.test(directHistory.screenText.trimEnd()));
    await test.evaluate(() => directHistory.focus());
    await test.keyboard.type("i=1; while [ \"$i\" -le 140 ]; do printf 'RELAY-HISTORY-%03d\\n' \"$i\"; i=$((i+1)); done; printf '__RELAY_HISTORY_READY__\\n'");
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => [directHistory, relayHistory].every(terminal =>
      terminal.viewport.liveTop > 80 && terminal.screenText.split("\n").some(line =>
        line.trim() === "__RELAY_HISTORY_READY__")));

    stage = "relayed scrolling and independent viewports";
    await test.evaluate(() => {
      relayHistory.setReadOnly(true);
      relayHistory.scrollLines(-80);
    });
    await test.waitForFunction(() => !relayHistory.viewport.pending && !relayHistory.viewport.following &&
      /RELAY-HISTORY-0\d\d/.test(relayHistory.screenText));
    check(await test.evaluate(() => directHistory.viewport.following && !relayHistory.peer.isPrimary),
      "Read-only relay navigation moved the producer or claimed primary");
    const point = await test.evaluate(() => {
      const bounds = relayHistory.element.shadowRoot.querySelector(".surface").getBoundingClientRect();
      return { x: bounds.x + bounds.width / 2, y: bounds.y + bounds.height / 2 };
    });
    const beforeWheel = await test.evaluate(() => relayHistory.viewport.top);
    await test.mouse.move(point.x, point.y);
    await test.mouse.wheel(0, -120);
    await test.waitForFunction(top => !relayHistory.viewport.pending && relayHistory.viewport.top < top, beforeWheel);
    const retained = await test.evaluate(() => relayHistory.viewport.rowIds[0]);
    await test.evaluate(() => directHistory.focus());
    await test.keyboard.type("printf '__RELAY_MORE__\\n'");
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => directHistory.screenText.split("\n").some(line =>
      line.trim() === "__RELAY_MORE__"));
    check(await test.evaluate(id => relayHistory.viewport.rowIds[0] === id, retained),
      "New output moved the retained relay viewport");
    await test.evaluate(() => relayHistory.scrollToLive());
    await test.waitForFunction(() => relayHistory.viewport.following &&
      relayHistory.screenText.split("\n").some(line => line.trim() === "__RELAY_MORE__"));

    stage = "late-attachment history transfer";
    await test.evaluate(async () => {
      window.lateRelayHistory = await mountHistoryView("late-relay", "hmp1");
      window.lateDirectHistory = await mountHistoryView("late-direct", "direct");
    });
    const counts = await test.evaluate(() => ({
      existingRelay: relayHistory.viewport.liveTop,
      lateRelay: lateRelayHistory.viewport.liveTop,
      lateDirect: lateDirectHistory.viewport.liveTop
    }));
    check(counts.existingRelay > 80 && counts.lateDirect > 80, "Retained producer or existing replica history disappeared");
    check(counts.lateRelay === counts.lateDirect, "Late HMP1 relay did not receive retained producer history");
    await test.evaluate(() => lateRelayHistory.scrollToRow(0));
    await test.waitForFunction(() => !lateRelayHistory.viewport.pending &&
      lateRelayHistory.viewport.top === 0 && lateRelayHistory.screenText.includes("RELAY-HISTORY-001"));
    stage = "reconnect history transfer";
    await test.evaluate(async () => {
      await lateRelayHistory.dispose();
      window.lateRelayHistory = await mountHistoryView("reconnected-relay", "hmp1");
    });
    check(await test.evaluate(() => lateRelayHistory.viewport.liveTop === lateDirectHistory.viewport.liveTop),
      "Reconnect lost retained producer history");
    check(errors.length === 0, errors.join("; "));
    return { passed: true, counts, preAttachmentHistoryTransfer: true, errors };
  } catch (error) {
    throw new Error(`${stage}: ${error.message}`);
  } finally {
    if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    await context.close();
  }
}
