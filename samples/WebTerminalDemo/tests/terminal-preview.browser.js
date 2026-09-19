async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0];
  if (!origin) throw new Error("Open the running WebTerminalDemo before executing this fixture");
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1100 } });
  const test = await context.newPage();
  const errors = [], reports = [], connections = [];
  const check = (value, message) => { if (!value) throw new Error(message); };
  let instanceId, stage = "setup", mode = "normal", signalHeld;
  test.on("pageerror", error => errors.push(error.message));

  // Observe real HWT frames, including cell deltas, rather than requesting a preview-only test hook.
  const readFrame = (payload, connection) => {
    if (typeof payload === "string" || payload.length < 8 || payload.readUInt32LE(0) !== 0x31545748) return null;
    const length = payload.readUInt32LE(4);
    const metadata = JSON.parse(payload.subarray(8, 8 + length).toString("utf8"));
    if (!connection.preview) return { metadata };
    let offset = 8 + length;
    const count = payload.readUInt32LE(offset);
    offset += 4;
    if (metadata.full) connection.cells = new Array(metadata.columns * metadata.rows);
    for (let index = 0; index < count; index++) {
      const cell = payload.readUInt32LE(offset), attributes = payload.readUInt16LE(offset + 16);
      const width = payload[offset + 18], textLength = payload.readUInt16LE(offset + 20);
      offset += 22;
      const text = payload.subarray(offset, offset + textLength).toString("utf8");
      offset += textLength;
      connection.cells[cell] = width === 0 ? "" : attributes & 64 ? " ".repeat(width) : text || " ";
    }
    const rows = Array.from({ length: metadata.rows }, (_, row) =>
      connection.cells.slice(row * metadata.columns, (row + 1) * metadata.columns).join("").trimEnd());
    return { metadata, text: rows.join("\n") };
  };
  // Gate native worker notifications without replacing real networking, rendering, or terminal APIs.
  await test.addInitScript(() => {
    const NativeWorker = window.Worker;
    const wire = window.__previewFixtureWire = { mode: "normal", workers: [] };
    window.Worker = class extends NativeWorker {
      constructor(url, options) {
        super(url, options);
        const record = this.fixtureRecord = { url: null, preview: false, held: [], replaying: false };
        wire.workers.push(record);
        this.addEventListener("message", event => {
          if (record.replaying) return;
          const history = event.data.history ?? event.data.stats?.history;
          const hold = wire.mode === "hold-details" && !record.preview && history?.markerResult?.details ||
            wire.mode === "hold-jump" && record.preview && history && history.top < history.liveTop;
          if (!hold) return;
          event.stopImmediatePropagation();
          record.held.push(() => this.dispatchEvent(new MessageEvent("message", { data: event.data })));
        });
      }
      postMessage(message, ...args) {
        if (message.type === "init") {
          const target = new URL(message.url);
          this.fixtureRecord.preview = target.searchParams.get("preview") === "true";
          if (this.fixtureRecord.preview && wire.mode === "reject") {
            target.searchParams.set("failure", "reject-upgrade");
            message = { ...message, url: target.href };
          }
          this.fixtureRecord.url = target.href;
        }
        return super.postMessage(message, ...args);
      }
      terminate() {
        this.fixtureRecord.terminated = true;
        this.fixtureRecord.held.length = 0;
        return super.terminate();
      }
    };
  });
  test.on("websocket", socket => {
    const url = socket.url();
    const connection = {
      url, preview: /[?&]preview=true(?:&|$)/.test(url), probe: /[?&]name=preview-probe(?:&|$)/.test(url),
      sent: [], delivered: [], cells: [], closed: false
    };
    connections.push(connection);
    socket.on("framesent", ({ payload }) => {
      if (typeof payload === "string") connection.sent.push(JSON.parse(payload));
    });
    socket.on("framereceived", ({ payload }) => {
      const frame = readFrame(payload, connection);
      const history = frame?.metadata.history;
      const hold = mode === "hold-details" && !connection.preview && history?.markerResult?.details ||
        mode === "hold-jump" && connection.preview && !connection.probe && history &&
        history.top < history.liveTop;
      if (frame) connection.delivered.push(frame);
      if (hold) signalHeld?.(connection);
    });
    socket.on("close", () => { connection.closed = true; });
  });
  const previews = () => connections.filter(connection => connection.preview && !connection.probe);
  const setMode = async value => {
    mode = value;
    await test.evaluate(value => { window.__previewFixtureWire.mode = value; }, value);
  };
  const waitHeld = async promise => {
    const connection = await Promise.race([
      promise, test.waitForTimeout(15000).then(() => { throw new Error("Expected HWT response was not intercepted"); })
    ]);
    await test.waitForFunction(url =>
      window.__previewFixtureWire.workers.some(worker => worker.url === url && worker.held.length > 0), connection.url);
    return connection;
  };
  const release = async connection => test.evaluate(url => {
    const worker = window.__previewFixtureWire.workers.find(worker => worker.url === url);
    worker.replaying = true;
    try { for (const deliver of worker.held.splice(0)) deliver(); }
    finally { worker.replaying = false; }
  }, connection.url);
  const waitPeers = async count => test.waitForFunction(async ({ id, count }) => {
    const instances = await (await fetch("/api/terminals")).json();
    return instances.find(instance => instance.id === id)?.peerCount === count;
  }, { id: instanceId, count });
  const card = () => test.locator(".demo-terminal-preview");
  const point = async id => test.evaluate(id => {
    const terminal = previewSource, marker = terminal.markers.find(mark => mark.id === id);
    const layout = terminal.layout, track = layout.scrollbar, bounds = terminal.element.getBoundingClientRect();
    return {
      x: bounds.left + (track.left + 1) * bounds.width / layout.width,
      y: bounds.top + (track.top + (track.height - 3) * marker.row /
        Math.max(1, terminal.viewport.totalRows - 1) + 1.5) * bounds.height / layout.height
    };
  }, id);
  const hover = async id => {
    const target = await point(id);
    await test.mouse.move(target.x, target.y);
  };
  const sourceState = () => test.evaluate(() => {
    const { revision, ...selection } = previewSource.selection;
    return {
      top: previewSource.viewport.top, rowIds: previewSource.viewport.rowIds,
      following: previewSource.viewport.following, selection,
      columns: previewSource.geometry.columns, rows: previewSource.geometry.rows,
      peer: previewSource.peer, text: previewSource.screenText,
      sameTerminal: previewSource === [...webTerminalViews.values()][0].terminal,
      focused: document.activeElement === window.previewFocus,
      views: webTerminalViews.size
    };
  });
  const invariant = async before => {
    const after = await sourceState();
    const changes = Object.keys(before).filter(key => JSON.stringify(before[key]) !== JSON.stringify(after[key]))
      .map(key => ({ key, before: before[key], after: after[key] }));
    check(changes.length === 0, `Preview changed source state: ${JSON.stringify(changes)}`);
  };
  const assertDisposed = async message => {
    const terminated = await test.evaluate(() => window.__previewFixtureWire.workers
      .filter(worker => worker.terminated).map(worker => worker.url));
    for (const connection of previews()) if (terminated.includes(connection.url)) connection.disposed = true;
    check(previews().every(connection => connection.closed || connection.disposed), message);
  };
  const leave = async () => {
    await test.mouse.move(1, 1);
    await card().waitFor({ state: "detached" });
    await waitPeers(1);
    await assertDisposed("A preview worker survived pointer leave");
  };
  const assertReady = async (mark, text, transport) => {
    await test.locator(".demo-terminal-preview[data-state=ready]").waitFor({ state: "visible" });
    check(await card().getAttribute("data-marker") === mark.id, "Preview is attached to the wrong command");
    check(await card().getAttribute("role") === "tooltip", "Preview is not an HTML tooltip");
    check(await card().evaluate(element => {
      const bounds = element.getBoundingClientRect();
      const mount = element.querySelector(".demo-terminal-preview-mount").getBoundingClientRect();
      return element.parentElement === document.body && bounds.width <= 640 &&
        bounds.left >= 0 && bounds.right <= innerWidth && bounds.top >= 0 && bounds.bottom <= innerHeight &&
        mount.height > 0 && mount.height <= 280;
    }), "Body-portal preview is clipped, oversized, or not fitted to the viewport");
    const connection = previews().filter(connection => !connection.closed && !connection.disposed).at(-1);
    check(connection && connection.url.includes(`transport=${transport}`), "Child did not retain the parent transport");
    const frame = connection.delivered.at(-1);
    check(frame?.metadata.history.top === mark.row && frame.metadata.history.following === false &&
      frame.text.split("\n").some(row => row.trim() === text), "Real child HWT frame did not contain targeted command output");
    check((await card().locator(".demo-terminal-preview-status").textContent()).includes(`row ${mark.row}`),
      "Ready preview did not report the confirmed retained row");
    check((await card().locator(".demo-terminal-preview-command").textContent()).includes(text),
      "Preview omitted decoded command details");
    check(connection.delivered.every(frame => !frame.metadata.peer.isPrimary), "Preview claimed the primary role");
    check(connection.sent.some(command => command.type === "marker" && command.action === "jump" && command.id === mark.id),
      "Preview never requested producer-backed marker navigation");
    check(!connection.sent.some(command => ["input", "paste", "key", "mouse", "resize", "requestPrimary"].includes(command.type)),
      "Read-only preview sent producer-mutating input");
    const mount = card().locator(".demo-terminal-preview-mount");
    check(await mount.evaluate(element => element.inert), "Embedded terminal is not inert");
    const canvas = mount.locator(".hex1b-terminal canvas").first();
    check(await canvas.isVisible(), "Real child canvas is not visible after the jump");
    check(await canvas.evaluate(element => {
      const shadow = element.getRootNode();
      const scrollbar = shadow.querySelector(".scrollbar-canvas");
      const pixels = scrollbar.getContext("2d").getImageData(0, 0, scrollbar.width, scrollbar.height).data;
      return shadow.querySelector('[role="scrollbar"]').hidden &&
        !pixels.some((value, index) => index % 4 === 3 && value !== 0);
    }), "Embedded preview exposes its own scrollbar");
    check((await canvas.screenshot()).byteLength > 500, "Embedded terminal canvas did not produce a rendered image");
    check(await test.evaluate(() => webTerminalViews.size === 1), "Preview created a synthetic playground view");
    return { top: frame.metadata.history.top, transport, text };
  };
  try {
    for (const transport of ["direct", "hmp1"]) {
      mode = "normal";
      stage = `${transport}: source shell and distinguishable command output`;
      await test.goto(`${origin}/?scene=shell&renderer=webgl2&transport=${transport}`);
      await test.waitForFunction(() => [...webTerminalViews.values()][0]?.instance.id);
      instanceId = await test.evaluate(() => [...webTerminalViews.values()][0].instance.id);
      await test.waitForFunction(() => [...webTerminalViews.values()][0]?.terminal?.peer.isPrimary &&
        /([#$%>]|[^\x00-\x7f])$/.test([...webTerminalViews.values()][0].terminal.screenText.trimEnd()));
      await test.evaluate(() => { window.previewSource = [...webTerminalViews.values()][0].terminal; });
      for (const label of ["ALPHA", "BETA"]) {
        const body = `printf 'PREVIEW-${label}-output\\n'; i=1; while [ "$i" -le 45 ]; do printf '${label}-%03d\\n' "$i"; i=$((i+1)); done`;
        const encoded = encodeURIComponent(body).replace(/'/g, "%27");
        await test.evaluate(() => previewSource.focus());
        await test.keyboard.type(`printf '\\033]133;C;cmdline_url=%s\\007' '${encoded}'; ${body}; printf '\\033]133;D;%s\\007__PREVIEW_${label}_DONE__\\n' $?`);
        await test.keyboard.press("Enter");
        await test.waitForFunction(label => previewSource.screenText.split("\n").some(
          row => row.trim() === `__PREVIEW_${label}_DONE__`) &&
          /([#$%>]|[^\x00-\x7f])$/.test(previewSource.screenText.trimEnd()), label);
      }
      const marks = await test.evaluate(() => previewSource.markers.filter(mark => mark.phase === "executing")
        .sort((a, b) => a.row - b.row));
      check(marks.length === 2, "Seeded command markers were not retained");
      const picker = test.locator(".terminal-window .view-scrollbar-tooltip");
      check(await picker.locator('option[value="terminal"]').textContent() === "Terminal preview",
        "Terminal preview option is not discoverable");
      await picker.selectOption("terminal");
      const selectionPoints = await test.evaluate(() => {
        const row = previewSource.screenText.split("\n").findIndex(row => row.startsWith("BETA-"));
        const box = previewSource.element.shadowRoot.querySelector(".surface").getBoundingClientRect();
        return { x: box.x + previewSource.layout.cellWidth / 2, y: box.y + (row + .5) * previewSource.layout.cellHeight,
          end: box.x + 7.5 * previewSource.layout.cellWidth };
      });
      await test.mouse.move(selectionPoints.x, selectionPoints.y);
      await test.mouse.down();
      await test.mouse.move(selectionPoints.end, selectionPoints.y);
      await test.mouse.up();
      await test.waitForFunction(() => previewSource.selection.active && !previewSource.selection.pending &&
        previewSource.selection.text?.startsWith("BETA-"));
      await test.evaluate(() => { previewSource.focus(); window.previewFocus = document.activeElement; });
      const before = await sourceState();

      stage = `${transport}: no child during details loading and no flash of the live frame`;
      const countBefore = previews().length;
      await setMode("hold-details");
      const detailsHeld = new Promise(resolve => { signalHeld = resolve; });
      await hover(marks[0].id);
      const parentConnection = await waitHeld(detailsHeld);
      await test.waitForTimeout(300);
      check(previews().length === countBefore, "Preview connected before command details were ready");
      check(await card().getAttribute("data-state") === "loading", "Details-loading preview did not remain loading");
      stage = `${transport}: initial live child frame stays hidden until marker jump confirmation`;
      await setMode("hold-jump");
      const jumpHeld = new Promise(resolve => { signalHeld = resolve; });
      await release(parentConnection);
      const previewConnection = await waitHeld(jumpHeld);
      check(previewConnection.delivered.some(frame => frame.metadata.history.following), "No initial live frame was exercised");
      check(await card().getAttribute("data-state") === "loading", "Preview became ready before marker navigation completed");
      check(!await card().locator(".hex1b-terminal").isVisible(), "Initial live frame flashed in the preview");
      await setMode("normal");
      signalHeld = undefined;
      await release(previewConnection);
      const first = await assertReady(marks[0], "PREVIEW-ALPHA-output", transport);
      await invariant(before);
      const screenshot = `.playwright-cli/terminal-preview-ready-${transport}.png`;
      await test.screenshot({ path: screenshot });
      await leave();

      stage = `${transport}: independent second command and preview disposal`;
      await hover(marks[1].id);
      const second = await assertReady(marks[1], "PREVIEW-BETA-output", transport);
      await invariant(before);
      await picker.selectOption("default");
      await card().waitFor({ state: "detached" });
      await waitPeers(1);
      await assertDisposed("Switching to an ordinary tooltip leaked the preview");
      await picker.selectOption("terminal");
      await test.mouse.move(1, 1);
      await hover(marks[1].id);
      await assertReady(marks[1], "PREVIEW-BETA-output", transport);
      await picker.selectOption("off");
      await card().waitFor({ state: "detached" });
      await waitPeers(1);
      const disabledCount = previews().length;
      await hover(marks[0].id);
      await test.waitForTimeout(350);
      check(previews().length === disabledCount, "Disabled tooltip mode opened a child");
      await picker.selectOption("terminal");
      await test.mouse.move(1, 1);
      const rapidCount = previews().length;
      await hover(marks[0].id);
      await hover(marks[1].id);
      await test.mouse.move(1, 1);
      await test.waitForTimeout(350);
      check(previews().length === rapidCount && await card().count() === 0, "Rapid hover cancellation leaked a preview");

      stage = `${transport}: bookmarks use ordinary HTML, not another terminal`;
      const bookmark = await test.evaluate(() => previewSource.addMarker({
        position: { generation: previewSource.viewport.generation, rowId: previewSource.viewport.rowIds[4], column: 0 },
        label: "Preview bookmark"
      }));
      await hover(bookmark.id);
      await test.getByRole("tooltip").filter({ hasText: "Preview bookmark" }).waitFor({ state: "visible" });
      await test.waitForTimeout(300);
      check(await card().count() === 0 && previews().length === rapidCount, "Bookmark opened a terminal preview");
      await test.mouse.move(1, 1);

      stage = `${transport}: isolated preview error`;
      await setMode("reject");
      await hover(marks[0].id);
      await test.locator(".demo-terminal-preview[data-state=error]").waitFor({ state: "visible" });
      check((await card().textContent()).includes("Preview unavailable"), "Failed preview lacks an actionable error");
      await invariant(before);
      await leave();
      await setMode("normal");

      stage = `${transport}: server rejects handcrafted preview input, resizing and primary requests`;
      const probe = await test.evaluate(async ({ id, transport, markerId }) => {
        const url = new URL("/ws", location.href);
        url.protocol = location.protocol === "https:" ? "wss:" : "ws:";
        url.search = new URLSearchParams({ instance: id, transport, preview: "true", name: "preview-probe" });
        return await new Promise((resolve, reject) => {
          const socket = new WebSocket(url);
          socket.binaryType = "arraybuffer";
          let sent = false;
          const timer = setTimeout(() => { socket.close(); reject(new Error("Read-only probe timed out")); }, 15000);
          socket.onerror = () => { clearTimeout(timer); socket.close(); reject(new Error("Read-only probe failed")); };
          socket.onmessage = event => {
            const bytes = new DataView(event.data);
            const metadata = JSON.parse(new TextDecoder().decode(new Uint8Array(event.data, 8, bytes.getUint32(4, true))));
            socket.send(JSON.stringify({ type: "ack", revision: metadata.revision }));
            if (!sent) {
              sent = true;
              for (const command of [
                { type: "requestPrimary", columns: 40, rows: 5 }, { type: "resize", columns: 40, rows: 5 },
                { type: "input", text: "printf '__PREVIEW_FORBIDDEN__\\n'\r" },
                { type: "marker", action: "details", requestId: 741, id: markerId }
              ]) socket.send(JSON.stringify(command));
            } else if (metadata.history?.markerResult?.requestId === 741) {
              clearTimeout(timer);
              socket.close();
              resolve({ peer: metadata.peer, columns: metadata.columns, rows: metadata.rows });
            }
          };
        });
      }, { id: instanceId, transport, markerId: marks[0].id });
      await waitPeers(1);
      check(!probe.peer.isPrimary && probe.columns === before.columns && probe.rows === before.rows,
        "Server let a preview connection take primary ownership or resize the producer");
      await invariant(before);
      check(!await test.evaluate(() => previewSource.screenText.includes("__PREVIEW_FORBIDDEN__")),
        "Server accepted handcrafted preview input");

      stage = `${transport}: closing the parent disposes the embedded terminal`;
      await hover(marks[1].id);
      await assertReady(marks[1], "PREVIEW-BETA-output", transport);
      await test.locator(".terminal-window .close-view").focus();
      await test.keyboard.press("Enter");
      await test.waitForFunction(() => webTerminalViews.size === 0 && !document.querySelector(".demo-terminal-preview"));
      await waitPeers(0);
      await assertDisposed("Closing the parent leaked a preview connection");
      reports.push({ transport, first, second, screenshot, serverReadOnly: true, previewConnections: previews().length - countBefore });
      await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
      instanceId = undefined;
    }
    check(errors.length === 0, errors.join("; "));
    return { passed: true, reports, errors };
  } catch (error) {
    const current = await test.evaluate(() => [...(window.webTerminalViews?.values() || [])].map(view => ({
      phase: view.phase, status: view.element.querySelector(".view-status")?.textContent,
      text: view.terminal?.screenText.slice(-600)
    })));
    throw new Error(`${stage}: ${error.message}; previews=${JSON.stringify(previews().map(connection => ({
      closed: connection.closed, disposed: connection.disposed, sent: connection.sent, frames: connection.delivered.map(frame => ({
        top: frame.metadata.history?.top, primary: frame.metadata.peer.isPrimary
      }))
    })))}; current=${JSON.stringify(current)}; errors=${JSON.stringify(errors)}`);
  } finally {
    mode = "normal";
    signalHeld = undefined;
    try {
      if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    } finally {
      await context.close();
    }
  }
}
