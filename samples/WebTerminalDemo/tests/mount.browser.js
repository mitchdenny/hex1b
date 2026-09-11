async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5290";
  const context = await page.context().browser().newContext({ viewport: { width: 1440, height: 1100 }, deviceScaleFactor: 2 });
  const test = await context.newPage();
  const errors = [];
  test.on("pageerror", error => errors.push(error.message));
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  try {
    await test.goto(`${origin}/health`);
    await test.setContent('<div id="a" style="width:600px;height:400px;padding:12px;border:1px solid;box-sizing:content-box"><span id="keep" style="position:absolute">Keep</span></div><div id="b" style="width:200px;height:200px"></div><div id="c" style="width:300px;height:200px"></div>');
    await test.evaluate(async () => {
      window.workers = [];
      window.commands = [];
      window.grid = { columns: 80, rows: 24 };
      window.primaryId = "1";
      window.broadcast = () => {
        for (const worker of workers) {
          if (worker.stopped) continue;
          worker.dispatchEvent(new MessageEvent("message", { data: {
            type: "geometry", ...grid, cellWidth: 10, cellHeight: 20, mouseTracking: 1003, hyperlinks: [], title: "",
            progress: { state: "none", percentage: null }, shellIntegration: { phase: "unknown", lastExitCode: null },
            peer: { id: worker.id, primaryId, isPrimary: worker.id === primaryId }
          } }));
          worker.dispatchEvent(new MessageEvent("message", { data: {
            type: "stats", stats: { revision: 1, connected: true }, text: worker.id
          } }));
        }
      };
      window.Worker = class extends EventTarget {
        constructor() { super(); this.id = String(workers.length + 1); workers.push(this); }
        postMessage(message) {
          if (message.type === "init") {
            this.renderer = message.renderer;
            if (window.holdWorker) {
              queueMicrotask(() => {
                this.dispatchEvent(new MessageEvent("message", { data: { type: "connected" } }));
                this.dispatchEvent(new MessageEvent("message", { data: {
                  type: "geometry", ...grid, cellWidth: 10, cellHeight: 20, mouseTracking: 0, hyperlinks: [], title: "",
                  progress: { state: "none", percentage: null }, shellIntegration: { phase: "unknown", lastExitCode: null },
                  peer: { id: null, primaryId: null, isPrimary: false }
                } }));
                this.dispatchEvent(new MessageEvent("message", { data: { type: "stats", stats: { revision: 1 } } }));
              });
              return;
            }
            queueMicrotask(() => {
              this.dispatchEvent(new MessageEvent("message", { data: { type: "connected" } }));
              broadcast();
            });
          } else if (message.type === "command") {
            commands.push({ peer: this.id, ...message.command });
            if (message.command.type === "requestPrimary") primaryId = this.id;
            if (["requestPrimary", "resize"].includes(message.command.type) && primaryId === this.id) {
              grid = { columns: message.command.columns, rows: message.command.rows };
              broadcast();
            }
          }
        }
        terminate() { this.stopped = true; }
      };
      const { WebTerminal } = await import("/web-terminal/index.js");
      window.WebTerminal = WebTerminal;
      window.a = await WebTerminal.mount(document.getElementById("a"), { url: "/ws" });
      window.b = await WebTerminal.mount(document.getElementById("b"), { url: "/ws", renderer: "webgl2" });
      window.c = await WebTerminal.mount(document.getElementById("c"), { url: "/ws", readOnly: true });
    });
    await test.waitForFunction(() => a.geometry.columns === 60 && a.geometry.rows === 20);
    check(await test.evaluate(() => workers[0].renderer === "auto" && workers[1].renderer === "webgl2"),
      "Mount did not forward default and explicit renderer preferences to the worker");
    check(await test.evaluate(() => !!document.getElementById("keep")), "Mount replaced caller-owned children");
    check(await test.evaluate(() => b.geometry.columns === 60 && b.geometry.rows === 20), "Secondary missed initial authoritative geometry");
    const firstFit = await test.evaluate(() => {
      const bounds = b.element.shadowRoot.querySelector("canvas").getBoundingClientRect();
      return { width: bounds.width, height: bounds.height };
    });
    check(Math.abs(firstFit.width / firstFit.height - 1.5) < .001, "Secondary did not aspect-fit");

    await test.evaluate(() => { document.getElementById("a").style.width = "780px"; document.getElementById("a").style.height = "450px"; });
    await test.waitForFunction(() => a.geometry.columns === 78 && a.geometry.rows === 22 && b.geometry.columns === 78);
    const beforeSecondary = await test.evaluate(() => commands.filter(item => item.type === "resize").length);
    await test.evaluate(() => { document.getElementById("b").style.width = "160px"; document.getElementById("b").style.height = "320px"; });
    await test.waitForTimeout(150);
    check(await test.evaluate(before => commands.filter(item => item.type === "resize").length === before, beforeSecondary), "Secondary sent a resize");
    const secondFit = await test.evaluate(() => {
      const bounds = b.element.shadowRoot.querySelector("canvas").getBoundingClientRect();
      return { width: bounds.width, height: bounds.height };
    });
    check(Math.abs(secondFit.width / secondFit.height - 780 / 440) < .001 && secondFit.width <= 160.1, "Thumbnail geometry was distorted");

    await test.evaluate(() => { primaryId = "native"; grid = { columns: 100, rows: 50 }; broadcast(); });
    await test.waitForTimeout(150);
    check(await test.evaluate(before => commands.filter(item => item.type === "resize").length === before, beforeSecondary), "Remote role/resize caused an echo");
    check(await test.evaluate(() => !a.peer.isPrimary && !b.peer.isPrimary && a.geometry.rows === 50), "Native primary was not reflected");
    const remoteFit = await test.evaluate(() => {
      const bounds = a.element.shadowRoot.querySelector("canvas").getBoundingClientRect();
      return { width: bounds.width, height: bounds.height };
    });
    check(Math.abs(remoteFit.width - remoteFit.height) < .1 && remoteFit.height <= 450.1, "Remote aspect ratio did not fit primary's old container");
    check(await test.evaluate(() => {
      try { a.resize(90, 30); return false; } catch (error) { return error.message.includes("primary"); }
    }), "Public resize accepted a secondary request");
    await test.evaluate(() => b.requestPrimary());
    await test.waitForFunction(() => b.peer.isPrimary && b.geometry.columns === 20 && b.geometry.rows === 16);
    const beforeHide = await test.evaluate(() => commands.length);
    await test.evaluate(() => document.getElementById("b").style.display = "none");
    await test.waitForTimeout(150);
    check(await test.evaluate(before => commands.length === before, beforeHide), "Hidden mount requested minimum geometry");
    await test.evaluate(() => document.getElementById("b").style.display = "block");
    await test.waitForTimeout(150);
    check(await test.evaluate(before => commands.length === before, beforeHide), "Restoring matching geometry sent an echo");

    await test.locator("#a canvas").click({ position: { x: 50, y: 50 } });
    await test.keyboard.type("alpha");
    check(await test.evaluate(() => commands.filter(item => item.type === "input" && item.peer === "1").map(item => item.text).join("") === "alpha"), "Keyboard was not scoped to first mount");
    check(await test.evaluate(() => !commands.some(item => item.type === "input" && item.peer === "2")), "Keyboard leaked to second mount");
    check(await test.evaluate(() => c.element.shadowRoot.querySelector("textarea").disabled), "Read-only mount enabled keyboard");
    await test.locator("#b canvas").click({ position: { x: 50, y: 50 } });
    await test.keyboard.type("beta");
    check(await test.evaluate(() => commands.filter(item => item.type === "input" && item.peer === "2").map(item => item.text).join("") === "beta"), "Secondary input focus was not isolated");
    check(await test.evaluate(() => commands.some(item => item.type === "mouse" && item.peer === "2" && item.x >= 0 && item.x < 20)), "Scaled mouse did not reach correct mount");

    await test.evaluate(() => {
      window.holdWorker = true;
      const host = document.createElement("div");
      host.id = "d";
      host.style.cssText = "width:200px;height:200px";
      document.body.append(host);
      window.pendingController = new AbortController();
      window.pendingResolved = false;
      window.pendingMount = WebTerminal.mount(host, { url: "/ws", signal: pendingController.signal })
        .then(() => { window.pendingResolved = true; })
        .catch(error => { window.pendingError = error.name; });
    });
    await test.locator("#d canvas").click({ position: { x: 50, y: 50 } });
    const beforePendingInput = await test.evaluate(() => commands.length);
    await test.keyboard.type("ignored");
    check(await test.evaluate(before => commands.length === before, beforePendingInput), "Clicking an unready mount left keyboard focus in a different terminal");
    check(await test.evaluate(() => !pendingResolved), "Mount resolved before HMP1 connection readiness");
    await test.evaluate(async () => { pendingController.abort(); await pendingMount; });
    check(await test.evaluate(() => pendingError === "AbortError" && !document.querySelector("#d .hex1b-terminal")), "Pending mount did not abort cleanly");

    await test.evaluate(() => { a.dispose(); a.dispose(); b.dispose(); c.dispose(); });
    check(await test.evaluate(() => workers.every(worker => worker.stopped) && document.querySelectorAll(".hex1b-terminal").length === 0 && !!document.getElementById("keep")), "Dispose leaked or removed caller DOM");
    check(await test.evaluate(async () => {
      try { await WebTerminal.mount(document.getElementById("c"), { url: "/ws", scale: 99 }); return false; }
      catch (error) { return error instanceof RangeError && !document.querySelector(".hex1b-terminal"); }
    }), "Failed mount was not cleaned up");

    await test.evaluate(async () => {
      holdWorker = false;
      primaryId = String(workers.length + 1);
      grid = { columns: 80, rows: 24 };
      window.fixedView = await WebTerminal.mount(document.getElementById("c"), {
        url: "/ws", sizing: { mode: "fixed", columns: 100, rows: 30 }
      });
    });
    await test.waitForFunction(() => fixedView.geometry.columns === 100 && fixedView.geometry.rows === 30);
    const beforeRoleLoss = await test.evaluate(() => commands.length);
    await test.evaluate(() => {
      fixedView.setSizing({ mode: "auto", fontSize: 12 });
      primaryId = "native";
      grid = { columns: 110, rows: 40 };
      broadcast();
    });
    await test.waitForTimeout(150);
    check(await test.evaluate(before => commands.length === before && fixedView.geometry.columns === 110, beforeRoleLoss),
      "Losing primary failed to cancel a queued font-size resize");
    await test.evaluate(() => fixedView.requestPrimary());
    await test.waitForFunction(() => fixedView.peer.isPrimary && fixedView.geometry.columns === 40 && fixedView.geometry.rows === 13);
    await test.evaluate(() => document.getElementById("c").style.display = "none");
    await test.waitForTimeout(150);
    await test.evaluate(() => fixedView.setSizing({ mode: "fixed", columns: 132, rows: 43 }));
    await test.waitForFunction(() => fixedView.geometry.columns === 132 && fixedView.geometry.rows === 43);
    check(await test.evaluate(() => {
      try { fixedView.requestPrimary(); return false; }
      catch (error) { return error.message.includes("Show the terminal container"); }
    }), "A hidden fixed-grid view bypassed the primary-claim visibility guard");
    const beforeRestore = await test.evaluate(() => commands.length);
    await test.evaluate(() => document.getElementById("c").style.display = "block");
    await test.waitForTimeout(150);
    check(await test.evaluate(before => commands.length === before, beforeRestore), "Restoring a fixed-grid view sent an automatic resize");
    await test.evaluate(() => fixedView.dispose());
    check(await test.evaluate(() => workers.every(worker => worker.stopped)), "Sizing fixture leaked its worker");
    await test.waitForTimeout(100);
    check(errors.length === 0, `Unexpected browser errors: ${errors.join("; ")}`);
    return { passed: true, covered: ["arbitrary padded div", "aspect fit", "local primary resize", "secondary scaling", "remote role and aspect changes", "no feedback", "hidden mount", "keyboard/mouse isolation", "read-only", "pending focus/abort", "disposal", "failed mount cleanup", "initial fixed sizing", "queued resize cancellation on role loss", "hidden fixed sizing"], browserErrors: errors };
  } finally {
    await context.close();
  }
}
