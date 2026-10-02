import assert from "node:assert/strict";
import { test } from "node:test";
import { WebTerminal } from "../.build/web-terminal.js";
import { browser, Element, frame, mounting, present } from "./fixtures/browser.mjs";

const closures = [
  { code: 1000, reason: "", wasClean: true },
  { code: 1001, reason: "server restarting", wasClean: true },
  { code: 1006, reason: "", wasClean: false },
  { code: 4000, reason: "<host-defined result>", wasClean: true }
];

for (const stage of ["decoding", "scheduled", "drawing"]) {
  test(`Preserved close at ${stage} presents every final cell before notifying the host`, async t => {
    const workers = browser(t);
    const finalText = "Final output: e\u0301\u754c!";
    const cells = Array.from(finalText).map((text, index) => ({ index, text, width: 1 }));
    const notices = [];
    const view = await mounting(t, workers, {
      preserveOnDisconnect: true,
      onClose(details) {
        assert.equal(view.handle.connected, false);
        assert.equal(view.handle.screenText, finalText);
        assert.equal(view.handle.title, "final title");
        notices.push(details);
      }
    });
    await view.worker.request("open");
    await present(view, { columns: cells.length });
    await view.promise;
    if (stage === "decoding") await view.worker.request("holdImages");
    if (stage === "drawing") await view.worker.request("hold");
    await view.worker.request("frame", { buffer: frame({
      columns: cells.length, revision: 2, title: "final title", cells
    }) });
    if (stage === "drawing") await view.worker.request("draw");
    await view.worker.request("disconnect", { details: closures[0] });
    await view.worker.request("input", { message: {
      type: "command", command: { type: "input", text: "input racing disconnect" }
    } });
    if (stage !== "scheduled") {
      assert.deepEqual(notices, [], "close must wait for accepted content to finish presenting");
      await view.worker.request(stage === "decoding" ? "releaseImages" : "release");
    }
    await view.worker.request("flush");
    assert.deepEqual(notices, [closures[0]]);
    assert.equal(view.handle.screenText, finalText);
    assert.deepEqual(await view.worker.request("rendererState"), { disposed: false, text: finalText });
    assert.equal(view.handle.element.shadowRoot.querySelector("textarea").disabled, true);
    assert.throws(() => view.handle.paste("no input"), /does not accept input/);
    await view.worker.request("input", { message: { type: "viewport", width: 200, height: 100 } });
    await view.worker.request("draw");
    assert.deepEqual(await view.worker.request("rendererState"), { disposed: false, text: finalText });
    assert.equal(view.worker.commands.filter(command => command.type === "ack" && command.revision === 2).length, 0,
      "never send an acknowledgement on a closed transport");
    view.handle.dispose();
    assert.equal(view.container.children.length, 0);
  });
}

for (const stage of ["idle", "drawing"]) {
  test(`Preserved close with blinking text ${stage} freezes its visible phase`, async t => {
    const workers = browser(t);
    const notices = [];
    const view = await mounting(t, workers, { preserveOnDisconnect: true, onClose: details => notices.push(details) });
    await view.worker.request("clock", { message: { now: 0 } });
    await view.worker.request("open");
    await present(view, { cells: [{ index: 0, text: "X", width: 1, attributes: 16 }] });
    await view.promise;
    await view.worker.request("clock", { message: { now: 600 } });
    await view.worker.request("pulse");
    if (stage === "drawing") await view.worker.request("hold");
    await view.worker.request("draw");
    assert.equal(await view.worker.request("renderedBlink"), false);
    await view.worker.request("disconnect");
    if (stage === "drawing") {
      assert.deepEqual(notices, []);
      await view.worker.request("release");
    }
    await view.worker.request("flush");
    assert.equal(notices.length, 1);
    assert.equal(await view.worker.request("renderedBlink"), true);
    assert.deepEqual(await view.worker.request("rendererState"), { disposed: false, text: "X" });
  });
}

test("Default close releases the renderer; preservation is opt-in", async t => {
  const workers = browser(t);
  const view = await mounting(t, workers);
  await view.worker.request("resourceMetrics", { message: {
    imageCount: 2, textureBytes: 32, atlasGlyphs: 3, atlasBytes: 64, instanceBufferBytes: 128,
    imageUploadBytes: 32
  } });
  await view.worker.request("open");
  await present(view, {});
  await view.promise;
  assert.equal(view.handle.stats.imageCount, 2);
  assert.equal(view.handle.stats.textureBytes, 32);
  await view.worker.request("disconnect");
  await view.worker.request("flush");
  assert.equal((await view.worker.request("rendererState")).disposed, true);
  for (const resource of ["imageCount", "textureBytes", "atlasGlyphs", "atlasBytes", "instanceBufferBytes"])
    assert.equal(view.handle.stats[resource], 0, `${resource} must describe released resources`);
  assert.equal(view.handle.stats.imageUploadBytes, 32, "cumulative counters survive disposal");
});

test("Preserved final DEC row modes survive disconnect and local redraw", async t => {
  const workers = browser(t);
  const closed = Promise.withResolvers();
  const view = await mounting(t, workers, { preserveOnDisconnect: true, onClose: closed.resolve });
  await view.worker.request("open");
  await present(view, { columns: 8, rows: 4 });
  await view.promise;
  const lineRenditions = [0, 1, 2, 3];
  await view.worker.request("frame", { buffer: frame({
    columns: 8, rows: 4, revision: 2, lineRenditions,
    cells: [
      { index: 0, text: "N", width: 1 },
      { index: 8, text: "W", width: 1 },
      { index: 16, text: "\u754c", width: 2 },
      { index: 17, text: "", width: 0 },
      { index: 24, text: "\u754c", width: 2 },
      { index: 25, text: "", width: 0 }
    ]
  }) });
  await view.worker.request("disconnect");
  await closed.promise;
  assert.deepEqual(await view.worker.request("renderedRenditions"), lineRenditions);
  await view.worker.request("input", { message: { type: "viewport", width: 320, height: 160 } });
  await view.worker.request("draw");
  assert.deepEqual(await view.worker.request("renderedRenditions"), lineRenditions);
  assert.equal((await view.worker.request("rendererState")).disposed, false);
  assert.equal(view.handle.screenText.split("\n").filter(line => line.includes("\u754c")).length, 2);
});

for (const lateMessage of [
  { type: "linkDetection", enabled: false, generation: 2 },
  { type: "linkDecorations", revision: 1, generation: 1, serial: 2, ranges: [] }
]) {
  test(`Preserved close freezes link decorations despite late ${lateMessage.type}`, async t => {
    const workers = browser(t);
    const view = await mounting(t, workers, { preserveOnDisconnect: true });
    await view.worker.request("open");
    await present(view, { cells: [{ index: 0, text: "X", width: 1 }] });
    await view.promise;
    await view.worker.request("input", { message: { type: "linkDetection", enabled: true, generation: 1 } });
    await view.worker.request("draw");
    await view.worker.request("input", { message: {
      type: "linkDecorations", revision: 1, generation: 1, serial: 1,
      ranges: [{ row: 0, startColumn: 0, endColumn: 1 }]
    } });
    await view.worker.request("draw");
    assert.deepEqual(await view.worker.request("renderedDecorations"), [1]);
    await view.worker.request("disconnect");
    await view.worker.request("flush");
    await view.worker.request("input", { message: lateMessage });
    await view.worker.request("input", { message: { type: "viewport", width: 200, height: 100 } });
    await view.worker.request("draw");
    assert.deepEqual(await view.worker.request("renderedDecorations"), [1]);
  });
}

test("Invalid preservation option fails before opening a transport", async t => {
  const workers = browser(t);
  await assert.rejects(WebTerminal.mount(new Element(), {
    url: "/ws", preserveOnDisconnect: "yes"
  }), /preserveOnDisconnect must be a boolean/);
  assert.equal(workers.length, 0);
});

for (const details of closures) {
  for (const stage of ["connecting", "socket-open", "awaiting-peer", "mounted"]) {
    test(`Native close ${details.code} at ${stage} notifies once without inferring completion or retrying`, async t => {
      const workers = browser(t);
      const notices = [];
      const view = await mounting(t, workers, {
        onClose(close) {
          assert.equal(view.handle.connected, false);
          assert.equal(view.settled, stage === "mounted");
          assert.equal(view.handle.element.shadowRoot.querySelector("textarea").disabled, true);
          assert.ok(Object.isFrozen(close));
          notices.push(close);
        }
      });
      if (stage !== "connecting") await view.worker.request("open");
      if (stage === "awaiting-peer") {
        await present(view, { peer: { id: null, primaryId: null, isPrimary: false } });
      } else if (stage === "mounted") {
        await present(view, {});
        await view.promise;
      }
      const closed = new Promise(resolve => view.worker.addEventListener("message", event => {
        if (event.data.type === "closed") resolve();
      }));
      // Pre-mount close terminates the worker as part of mount cleanup.
      const closing = view.worker.request("disconnect", { details }).catch(error => {
        assert.match(error.message, /Worker terminated/);
      });
      await closed;
      if (stage !== "mounted") {
        await assert.rejects(view.promise, new RegExp(`WebSocket closed \\(${details.code}`));
        assert.equal(view.container.children.length, 0);
      } else {
        assert.equal(view.handle.connected, false);
        assert.throws(() => view.handle.paste("no input"), /does not accept input/);
        assert.equal(view.container.children.length, 1, "disconnect does not remove the host's mounted view");
      }
      await closing;
      view.worker.deliver({ type: "closed", details });
      assert.deepEqual(notices, [details]);
      assert.equal(workers.length, 1, "reconnection is a host decision");
      assert.deepEqual(view.worker.errors, []);
    });
  }
}

test("WebSocket error waits for actual close details before rejecting mount", async t => {
  const workers = browser(t);
  const notices = [];
  const view = await mounting(t, workers, { onClose: details => notices.push(details) });
  await view.worker.request("socketError");
  assert.equal(view.settled, false);
  assert.deepEqual(notices, []);
  const details = { code: 1006, reason: "", wasClean: false };
  const closing = view.worker.request("disconnect", { details }).catch(error => {
    assert.match(error.message, /Worker terminated/);
  });
  await assert.rejects(view.promise, /WebSocket closed \(1006/);
  await closing;
  assert.deepEqual(notices, [details]);
});

test("Close callback errors remain visible but cannot strand mount", async t => {
  const workers = browser(t);
  const failure = new Error("Host callback failed");
  const view = await mounting(t, workers, { onClose() { throw failure; } });
  const closing = view.worker.request("disconnect").catch(error => {
    assert.match(error.message, /Worker terminated/);
  });
  await assert.rejects(view.promise, /WebSocket closed/);
  await closing;
  assert.deepEqual(view.worker.errors, [failure]);
  assert.equal(view.container.children.length, 0);
});

test("Host can remount after a transport close without stale close notifications", async t => {
  const workers = browser(t);
  const notices = [];
  for (let attempt = 0; attempt < 2; attempt++) {
    const view = await mounting(t, workers, { onClose: details => notices.push({ attempt, details }) });
    await view.worker.request("open");
    await present(view, {});
    await view.promise;
    await view.worker.request("disconnect", { details: closures[attempt] });
    view.handle.dispose();
    view.worker.deliver({ type: "closed", details: closures[2] });
  }
  assert.deepEqual(notices, closures.slice(0, 2).map((details, attempt) => ({ attempt, details })));
  assert.equal(workers.length, 2);
});

for (const mounted of [false, true]) {
  for (const abort of [false, true]) {
    test(`${abort ? "Abort" : "Dispose"} ${mounted ? "after" : "before"} mount never manufactures a close`, async t => {
      const workers = browser(t);
      const controller = new AbortController();
      const notices = [];
      const view = await mounting(t, workers, {
        signal: controller.signal, onClose: details => notices.push(details)
      });
      await view.worker.request("open");
      if (mounted) {
        await present(view, {});
        await view.promise;
      } else {
        await view.worker.request("frame", { buffer: frame() });
      }
      if (abort) controller.abort();
      else view.handle.dispose();
      if (!mounted) await assert.rejects(view.promise, { name: "AbortError" });
      view.worker.deliver({ type: "closed", details: closures[0] });
      view.handle.dispose();
      assert.deepEqual(notices, []);
      assert.equal(view.container.children.length, 0);
    });
  }
}

test("Initialization errors do not manufacture native close details", async t => {
  const workers = browser(t);
  const notices = [];
  await assert.rejects(WebTerminal.mount(new Element(), {
    url: "file:///not-a-socket", renderer: "webgl2", onClose: details => notices.push(details)
  }), /ws: or wss:/);
  assert.equal(workers.length, 0);
  assert.deepEqual(notices, []);
});

test("First-frame timeout rejects and disposes without manufacturing close details", async t => {
  const workers = browser(t);
  t.mock.timers.enable({ apis: ["setTimeout"] });
  const notices = [];
  const view = await mounting(t, workers, { onClose: details => notices.push(details) });
  await view.worker.request("open");
  t.mock.timers.tick(30000);
  await assert.rejects(view.promise, /Timed out waiting/);
  view.worker.deliver({ type: "closed", details: closures[0] });
  assert.deepEqual(notices, []);
  assert.equal(view.container.children.length, 0);
});

test("Closing during the first GPU presentation rejects instead of resolving a disconnected mount", async t => {
  const workers = browser(t);
  const notices = [];
  const view = await mounting(t, workers, { onClose: details => notices.push(details) });
  await view.worker.request("open");
  await view.worker.request("hold");
  await view.worker.request("frame", { buffer: frame({ title: "in flight" }) });
  await view.worker.request("draw");
  assert.equal(view.settled, false);
  const closing = view.worker.request("disconnect", { details: closures[3] }).catch(error => {
    assert.match(error.message, /Worker terminated/);
  });
  await assert.rejects(view.promise, /WebSocket closed \(4000/);
  await closing;
  assert.deepEqual(notices, [closures[3]]);
  assert.deepEqual(view.notices, []);
});

for (const mounted of [false, true]) {
  test(`Disposing reentrantly while disconnecting ${mounted ? "a mounted" : "a pending"} view suppresses onClose`, async t => {
    const workers = browser(t);
    const notices = [];
    let disposeOnDisconnect = false;
    const view = await mounting(t, workers, {
      onSelectionChange() {
        if (disposeOnDisconnect) view.handle.dispose();
      },
      onClose: details => notices.push(details)
    });
    await view.worker.request("open");
    if (mounted) {
      await present(view, {});
      await view.promise;
    }
    disposeOnDisconnect = true;
    view.worker.deliver({ type: "closed", details: closures[0] });
    if (!mounted) await assert.rejects(view.promise, { name: "AbortError" });
    assert.deepEqual(notices, []);
    assert.equal(view.container.children.length, 0);
  });
}
