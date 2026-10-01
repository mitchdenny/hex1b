async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({
    viewport: { width: 1500, height: 1100 }, deviceScaleFactor: 2
  });
  const test = await context.newPage();
  const errors = [];
  let instanceId;
  let stage = "initializing";
  const reports = [];
  await test.addInitScript(() => {
    window.scrollbarWire = [];
    window.heldAcknowledgements = 0;
    window.ackControl = false;
    window.ackChannel = new BroadcastChannel("scrollbar-test-acks");
    ackChannel.onmessage = ({ data }) => {
      if (data.kind === "wire") scrollbarWire.push(data.command);
      if (data.kind === "held") heldAcknowledgements++;
      if (data.kind === "control") ackControl = data.value;
    };
  });
  // The transport lives in a worker, outside page-level WebSocket routing.
  // Delay only real outgoing acknowledgements; all producer frames remain intact.
  const workerSetups = [];
  test.on("worker", worker => workerSetups.push(worker.evaluate(() => {
    const channel = new BroadcastChannel("scrollbar-test-acks");
    const send = WebSocket.prototype.send;
    const held = [];
    let hold = false;
    WebSocket.prototype.send = function(data) {
      const command = typeof data === "string" ? JSON.parse(data) : null;
      if (command && command.type !== "ack") channel.postMessage({ kind: "wire", command });
      if (hold && command?.type === "ack") {
        held.push(() => send.call(this, data));
        channel.postMessage({ kind: "held" });
      } else send.call(this, data);
    };
    channel.onmessage = ({ data }) => {
      if (typeof data !== "boolean") return;
      hold = data;
      if (!hold) for (const release of held.splice(0)) release();
      channel.postMessage({ kind: "control", value: hold });
    };
  })));
  test.on("pageerror", error => errors.push(error.message));
  test.on("console", message => {
    if (message.type() === "error") errors.push(message.text());
  });
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const settled = () => test.waitForFunction(() => {
    const terminal = [...webTerminalViews.values()][0]?.terminal;
    return terminal?.viewport.available && !terminal.viewport.pending;
  });
  try {
    for (const renderer of ["webgpu", "webgl2"]) {
      stage = `${renderer}: mounting`;
      await test.goto(`${origin}/?empty=1`);
      await test.selectOption("#renderer", renderer);
      await test.selectOption("#scene", "shell");
      await test.click("#create");
      await test.waitForFunction(() => [...webTerminalViews.values()][0]?.instance.id);
      instanceId = await test.evaluate(() => [...webTerminalViews.values()][0].instance.id);
      await test.waitForFunction(() => [...webTerminalViews.values()][0]?.phase === "connected");
      await Promise.all(workerSetups);
      const view = test.locator(".terminal-window").first();
      await view.locator(".view-resolution").selectOption("80x24");
      await test.waitForFunction(() => {
        const t = [...webTerminalViews.values()][0].terminal;
        return t.peer.isPrimary && t.geometry.columns === 80 &&
          /[\u276f$#%>]$/.test(t.screenText.trimEnd());
      });
      await test.evaluate(() => {
        window.scrollbarView = [...webTerminalViews.values()][0];
        window.originalTerminal = scrollbarView.terminal;
        window.originalConnection = scrollbarView.connectionId;
        window.originalPeer = originalTerminal.peer.id;
        originalTerminal.focus();
      });
      await test.keyboard.type("printf '\\033]133;C;fixture-start\\007'; i=1; while [ \"$i\" -le 120 ]; do printf 'ROW-%03d alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron\\n' \"$i\"; i=$((i+1)); done; printf '\\033]133;D;0;fixture-end\\007__SCROLL_READY__\\n'");
      await test.keyboard.press("Enter");
      await test.waitForFunction(() => originalTerminal.screenText.split("\n").some(line =>
        line.trim() === "__SCROLL_READY__") && originalTerminal.viewport.liveTop > 100);
      await test.waitForFunction(() => originalTerminal.markers.some(marker =>
        marker.source === "command" && marker.phase === "executing" && marker.row !== null));
      check(await test.evaluate(async () => {
        const marker = originalTerminal.markers.find(marker => marker.phase === "executing");
        const details = await originalTerminal.getCommandMarkDetails(marker.id);
        return details.rawParameters.includes("fixture-start");
      }), "Retained command details were unavailable");

      stage = `${renderer}: modes and asymmetric padding`;
      for (const [edge, value] of [["top", 7], ["right", 19], ["bottom", 11], ["left", 23]]) {
        await view.locator(`.padding-${edge}`).fill(String(value));
        await view.locator(`.padding-${edge}`).dispatchEvent("change");
      }
      for (const mode of ["overlay", "beside", "native", "disabled", "overlay"]) {
        await view.locator(".view-scrollbar").selectOption(mode);
        await test.waitForFunction(mode => {
          const t = originalTerminal;
          return t.padding.left === 23 && t.padding.top === 7 &&
            (mode === "native" || mode === "disabled" ? t.scrollbar === false : t.scrollbar?.placement === mode);
        }, mode);
        const geometry = await test.evaluate(() => {
          const t = originalTerminal, l = t.layout;
          return { stable: t === scrollbarView.terminal && originalConnection === scrollbarView.connectionId &&
              originalPeer === t.peer.id && t.connected && t.peer.isPrimary,
            columns: t.geometry.columns, content: l.content, layout: l };
        });
        check(geometry.stable && geometry.columns === 80, "Changing local chrome remounted/resized a fixed grid");
        check(geometry.content.left >= 23 && geometry.content.top >= 7 &&
          geometry.content.left + geometry.content.width <= geometry.layout.width - 19 + .1 &&
          geometry.content.top + geometry.content.height <= geometry.layout.height - 11 + .1,
        "Content escaped asymmetric padding");
        if (mode === "beside") check(geometry.layout.scrollbar &&
          geometry.content.left + geometry.content.width <= geometry.layout.scrollbar.left + .1,
        "Beside gutter overlaps terminal cells");
      }

      stage = `${renderer}: actual default painter pixels and fade`;
      await test.evaluate(() => {
        window.scrollbarPixels = () => {
          const canvas = originalTerminal.element.shadowRoot.querySelector("canvas.scrollbar-canvas");
          const context = canvas?.getContext("2d");
          if (!context) throw new Error("No actual Canvas2D scrollbar layer");
          const bytes = context.getImageData(0, 0, canvas.width, canvas.height).data;
          return bytes.some((value, index) => index % 4 === 3 && value > 0);
        };
        originalTerminal.setScrollbar({ placement: "overlay", hideDelay: 100, fadeDuration: 100 });
      });
      const edge = await test.evaluate(() => {
        const l = originalTerminal.layout, b = originalTerminal.element.getBoundingClientRect();
        return { x: b.left + l.scrollbar.left + l.scrollbar.width / 2,
          y: b.top + l.scrollbar.top + l.scrollbar.height / 2 };
      });
      await test.mouse.move(edge.x, edge.y);
      await test.waitForFunction(() => scrollbarPixels());
      await test.mouse.move(1, 1);
      await test.locator("#scene").focus();
      await test.waitForFunction(() => !scrollbarPixels(), null, { timeout: 5000 });

      stage = `${renderer}: synchronous custom painter`;
      await view.locator(".view-scrollbar-fade").selectOption("custom");
      await test.evaluate(async () => {
        const { softFadeScrollbar } = await import("/playground/scrollbar-renderer.js");
        window.customPaints = 0;
        originalTerminal.setScrollbar({ placement: "beside", render(frame) {
          customPaints++;
          if (frame.context.canvas !== frame.canvas || !Number.isFinite(frame.now))
            throw new Error("Invalid public painter frame");
          return softFadeScrollbar(frame);
        } });
        originalTerminal.refreshScrollbar();
      });
      await test.waitForFunction(() => customPaints > 0 && scrollbarPixels());

      stage = `${renderer}: captured dragging with delayed acknowledgements and live output`;
      await test.evaluate(async () => {
        const { renderDefaultScrollbar } = await import("/web-terminal/index.js");
        originalTerminal.setScrollbar({ render(frame) {
          window.dragFrame = frame;
          renderDefaultScrollbar(frame);
        } });
        originalTerminal.scrollToLive();
        originalTerminal.focus();
      });
      await settled();
      await test.keyboard.type("printf '\\033[?1003h'; i=1; while [ \"$i\" -le 100 ]; do printf 'DRAG-%03d\\n' \"$i\"; i=$((i+1)); sleep .02; done; printf '\\033[?1003l\\033]133;D;0;drag-end\\007__DRAG_DONE__\\n'");
      await test.keyboard.press("Enter");
      await test.waitForFunction(() => originalTerminal.stats.mouseTracking === 1003 &&
        originalTerminal.screenText.includes("DRAG-001") && dragFrame);
      const drag = await test.evaluate(() => {
        const b = originalTerminal.element.getBoundingClientRect(), f = dragFrame;
        return { x: b.left + f.thumb.left + f.thumb.width / 2,
          y: b.top + f.thumb.top + f.thumb.height / 2, top: b.top + f.track.top,
          height: f.track.height, batches: originalTerminal.stats.outputBatches };
      });
      const inputStart = await test.evaluate(() => {
        ackChannel.postMessage(true);
        return scrollbarWire.length;
      });
      await test.waitForFunction(() => ackControl);
      await test.mouse.move(drag.x, drag.y);
      await test.mouse.down();
      for (const fraction of [.3, .8, .2, .6])
        await test.mouse.move(drag.x, drag.top + drag.height * fraction, { steps: 3 });
      await test.mouse.up();
      await test.waitForFunction(() => dragFrame.pendingTarget !== null && originalTerminal.viewport.pending);
      const dragTarget = await test.evaluate(() => dragFrame.pendingTarget);
      await test.waitForFunction(() => heldAcknowledgements > 0);
      check(await test.evaluate(start => !scrollbarWire.slice(start).some(command => command.type === "input"), inputStart),
        "Scrollbar gestures leaked application input under mouse tracking");
      await test.evaluate(() => ackChannel.postMessage(false));
      await test.waitForFunction(target => !originalTerminal.viewport.pending &&
        originalTerminal.viewport.top === target, dragTarget);
      await test.waitForFunction(batches => originalTerminal.stats.mouseTracking === 0 &&
        originalTerminal.stats.outputBatches > batches, drag.batches);

      stage = `${renderer}: native scroll metrics and fixed canvas`;
      await view.locator(".view-scrollbar").selectOption("native");
      await test.waitForFunction(() => scrollbarView.nativeScrollbar?.rail.scrollHeight >
        scrollbarView.nativeScrollbar?.rail.clientHeight &&
        Math.abs(originalTerminal.layout.width - originalTerminal.element.clientWidth) < .1 &&
        Math.abs(originalTerminal.layout.height - originalTerminal.element.clientHeight) < .1);
      const native = await test.evaluate(() => {
        const rail = scrollbarView.nativeScrollbar.rail;
        const canvas = originalTerminal.element.shadowRoot.querySelector("canvas:not(.scrollbar-canvas)");
        const before = canvas.getBoundingClientRect().toJSON();
        const range = rail.scrollHeight - rail.clientHeight;
        rail.scrollTop = range * .25;
        return { before, target: Math.round(rail.scrollTop / range * originalTerminal.viewport.liveTop) };
      });
      await test.waitForFunction(target => !originalTerminal.viewport.pending &&
        Math.abs(originalTerminal.viewport.top - target) <= 1, native.target);
      check(await test.evaluate(before => {
        const after = originalTerminal.element.shadowRoot.querySelector("canvas:not(.scrollbar-canvas)").getBoundingClientRect();
        return after.x === before.x && after.y === before.y && after.width === before.width;
      }, native.before), "The native spacer scrolled the terminal canvas");
      await test.evaluate(() => {
        const rail = scrollbarView.nativeScrollbar.rail, range = rail.scrollHeight - rail.clientHeight;
        for (const fraction of [.7, .1, .8, .4]) {
          rail.scrollTop = range * fraction;
          rail.dispatchEvent(new Event("scroll"));
        }
        window.nativeTarget = Math.round(rail.scrollTop / range * originalTerminal.viewport.liveTop);
      });
      await test.waitForFunction(() => !originalTerminal.viewport.pending &&
        Math.abs(originalTerminal.viewport.top - nativeTarget) <= 1);

      stage = `${renderer}: authoritative bookmarks and reflow`;
      await view.locator(".add-bookmark").click();
      await test.waitForFunction(() => originalTerminal.markers.some(marker => marker.source === "custom"));
      await view.locator(".remove-bookmark").click();
      await test.waitForFunction(() => !originalTerminal.markers.some(marker => marker.source === "custom"));
      await test.evaluate(async () => {
        const t = originalTerminal, v = t.viewport;
        const row = t.screenText.split("\n").findIndex(line => /^ROW-\d+/.test(line));
        if (row < 0) throw new Error("No retained output row to anchor");
        window.anchoredText = t.screenText.split("\n")[row].slice(0, 7);
        window.scrollbarBookmark = await t.addMarker({
          position: { generation: v.generation, rowId: v.rowIds[row], column: 0 },
          label: "<bookmark> retained across reflow"
        });
        t.setSizing({ mode: "fixed", columns: 40, rows: 24 });
      });
      await test.waitForFunction(() => originalTerminal.geometry.columns === 40 &&
        originalTerminal.markers.some(marker => marker.id === scrollbarBookmark.id && marker.row !== null));
      await test.evaluate(() => originalTerminal.scrollToLive());
      await test.waitForFunction(() => originalTerminal.viewport.following && !originalTerminal.viewport.pending);
      await view.getByRole("button", { name: "<bookmark> retained across reflow", exact: true }).click();
      await test.waitForFunction(() => !originalTerminal.viewport.pending &&
        originalTerminal.screenText.startsWith(anchoredText));
      await view.locator(".view-markers").uncheck();
      check(await view.locator(".native-marker").count() === 0, "Marker visibility toggle left external ticks");
      check(await test.evaluate(() => originalTerminal.markers.some(marker => marker.id === scrollbarBookmark.id)),
        "Hiding markers deleted the retained inventory");
      await view.locator(".view-markers").check();
      await test.evaluate(() => originalTerminal.removeMarker(scrollbarBookmark.id));
      await test.waitForFunction(() => !originalTerminal.markers.some(marker => marker.id === scrollbarBookmark.id));

      stage = `${renderer}: secondary and read-only local chrome`;
      await view.locator(".thumbnail").click();
      await test.waitForFunction(() => webTerminalViews.size === 2 &&
        [...webTerminalViews.values()].every(view => view.phase === "connected"));
      const secondary = test.locator(".terminal-window").nth(1);
      await test.evaluate(() => {
        const view = [...webTerminalViews.values()][1];
        window.secondaryConnection = view.connectionId;
        view.terminal.setReadOnly(true);
      });
      await secondary.locator(".view-scrollbar").selectOption("native");
      await secondary.locator(".padding-left").fill("31");
      await secondary.locator(".padding-left").dispatchEvent("change");
      await test.waitForFunction(() => [...webTerminalViews.values()][1].terminal.padding.left === 31);
      check(await test.evaluate(() => {
        const second = [...webTerminalViews.values()][1];
        return !second.terminal.peer.isPrimary && second.terminal.geometry.columns === 40 &&
          originalTerminal.geometry.columns === 40 && originalTerminal.peer.isPrimary &&
          second.connectionId === secondaryConnection;
      }), "Secondary/read-only chrome changed producer sizing or authority");
      await secondary.locator(".close-view").click();

      stage = `${renderer}: minimal chrome and native teardown`;
      await view.locator(".minimal-chrome-toggle").click();
      check(await view.locator(".native-scrollbar").isVisible(), "Minimal chrome removed the chosen scrolling mechanism");
      await test.click("#restore-chrome");
      await test.evaluate(() => { window.detachedNative = scrollbarView.nativeScrollbar; });
      await view.locator(".view-scrollbar").selectOption("overlay");
      check(await test.evaluate(() => !detachedNative.element.isConnected &&
        scrollbarView.terminal === originalTerminal && scrollbarView.connectionId === originalConnection),
      "Switching away leaked native chrome or replaced the connection");
      await settled();
      reports.push({ renderer, markers: await test.evaluate(() => originalTerminal.markers.length) });

      stage = `${renderer}: closure and reconnect cleanup`;
      await view.locator(".view-scrollbar").selectOption("native");
      await test.evaluate(() => { window.closedNative = scrollbarView.nativeScrollbar; });
      await view.locator(".view-failure").selectOption("close");
      await view.locator(".trigger-failure").click();
      await test.waitForFunction(() => scrollbarView.phase === "closed");
      check(await test.evaluate(() => !closedNative.element.isConnected), "Closed view retained native listeners/chrome");
      await view.locator(".reconnect-view").click();
      await test.waitForFunction(() => scrollbarView.phase === "connected" && scrollbarView.nativeScrollbar);
      check(await test.evaluate(() => scrollbarView.terminal !== originalTerminal &&
        scrollbarView.connectionId !== originalConnection && scrollbarView.terminal.padding.left === 23 &&
        scrollbarView.terminal.scrollbar === false),
      "Explicit reconnect did not preserve presentation controls with a fresh view");
      await view.locator(".close-view").click();
      await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
      instanceId = undefined;
    }
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { reports, errors };
  } catch (error) {
    const status = await test.locator(".terminal-window").first().innerText().catch(() => "");
    throw new Error(`${stage}: ${error.message}\n${errors.join("; ")}\n${status}`);
  } finally {
    if (instanceId) await test.request.delete(`${origin}/api/terminals/${instanceId}`, { headers: { Origin: origin } });
    await context.close();
  }
}
