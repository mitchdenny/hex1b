async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5291";
  const context = await page.context().browser().newContext({ viewport: { width: 1200, height: 800 }, deviceScaleFactor: 2 });
  const test = await context.newPage();
  const errors = [];
  let stage = "mounting selection UI";
  test.on("pageerror", error => errors.push(error.message));
  const check = (value, message) => { if (!value) throw new Error(message); };
  try {
    await test.goto(`${origin}/health`);
    await test.evaluate(async () => {
      const { WebTerminal, TerminalAction } = await import("/web-terminal/index.js");
      window.uiFixture = { workers: [], commands: [], events: [], writes: [], statuses: [], mounts: 0,
        cleanup: 0, replace: true, fail: false, hostActions: 0, revision: 0 };
      const css = document.createElement("style");
      css.textContent = `
        .host-toolbar { position:absolute; display:flex; gap:4px; pointer-events:auto; }
        .host-toolbar[hidden] { display:none; }
        .host-toolbar button, .host-toolbar input {
          font:inherit; background:var(--cp-view-surface); color:var(--cp-view-text);
          border:2px dashed var(--cp-view-accent); border-radius:.625rem;
        }
        .host-toolbar input { width:90px; }
        .hex1b-terminal::part(selection-highlight) { opacity:.65; }
      `;
      document.head.append(css);
      document.body.replaceChildren();
      for (const id of ["a", "b"]) {
        const host = document.createElement("div");
        host.id = id;
        host.style.cssText = "display:inline-block;vertical-align:top;width:500px;height:300px";
        document.body.append(host);
      }
      Object.defineProperty(navigator, "clipboard", { configurable: true, value: {
        write: async items => { uiFixture.writes.push(await (await items[0].getType("text/plain")).text()); }
      } });
      window.Worker = class extends EventTarget {
        constructor() {
          super();
          this.id = String(uiFixture.workers.length + 1);
          this.selection = { requestId: 0, status: "none", mode: "character", ranges: [], text: null };
          this.copy = null;
          this.top = 20;
          this.viewportRequest = 0;
          uiFixture.workers.push(this);
        }
        frame() {
          if (this.stopped) return;
          const revision = ++uiFixture.revision;
          this.dispatchEvent(new MessageEvent("message", { data: {
            type: "geometry", columns: 40, rows: 10, cellWidth: 10, cellHeight: 20, mouseTracking: 1003, hyperlinks: [], title: "",
            progress: { state: "none", percentage: null }, shellIntegration: { phase: "unknown", lastExitCode: null },
            peer: { id: this.id, primaryId: "native", isPrimary: false }, revision, text: "HELLO",
            history: { generation: "1", buffer: "main", totalRows: 30, top: this.top, liveTop: 20,
              following: this.top === 20, requestId: this.viewportRequest,
              rowIds: Array.from({ length: 10 }, (_, index) => String(this.top + index + 1)),
              selection: { ...this.selection, ranges: this.selection.ranges.map(range => ({ ...range })) }, copy: this.copy }
          } }));
          this.dispatchEvent(new MessageEvent("message", { data: { type: "stats", stats: { revision, connected: true } } }));
        }
        postMessage(message) {
          if (message.type === "init") queueMicrotask(() => {
            this.dispatchEvent(new MessageEvent("message", { data: { type: "connected" } }));
            this.frame();
          });
          if (message.type !== "command") return;
          const command = message.command;
          uiFixture.commands.push({ view: this.id, ...command });
          if (command.type === "copy") {
            this.copy = { requestId: command.requestId, status: "valid", text: this.selection.text };
            queueMicrotask(() => this.frame());
          } else if (command.type === "selection") {
            this.selection.requestId = command.requestId;
            if (command.action === "clear") this.selection = {
              ...this.selection, status: "none", ranges: [], text: null
            };
            queueMicrotask(() => this.frame());
          } else if (command.type === "viewport") {
            this.viewportRequest = command.requestId;
            this.top = command.live ? 20 : Math.max(0, Math.min(20, this.top + command.delta));
            queueMicrotask(() => this.frame());
          }
        }
        terminate() { this.stopped = true; }
      };
      const select = worker => {
        worker.selection = { requestId: worker.selection.requestId, status: "valid", mode: "word",
          ranges: [{ row: 2, startColumn: 3, endColumn: 8 }], text: "HELLO" };
        worker.frame();
      };
      uiFixture.select = select;
      window.uiDefault = await WebTerminal.mount(document.getElementById("a"), { url: "/ws" });
      let toolbar;
      window.uiCustom = await WebTerminal.mount(document.getElementById("b"), {
        url: "/ws",
        onInput: () => { uiFixture.hostActions++; return "continue"; },
        onStatus: message => uiFixture.statuses.push(message),
        onSelectionUI(event) {
          const detail = event.detail;
          if (uiFixture.fail) throw new Error("host overlay failed");
          if (uiFixture.replace) event.preventDefault();
          if (!toolbar) {
            uiFixture.mounts++;
            uiFixture.overlay = detail.overlay;
            uiFixture.signal = detail.signal;
            toolbar = document.createElement("div");
            toolbar.className = "host-toolbar";
            const button = document.createElement("button");
            button.className = "host-copy";
            button.textContent = "My copy";
            const input = document.createElement("input");
            input.className = "host-field";
            input.setAttribute("aria-label", "Overlay notes");
            toolbar.append(button, input);
            detail.overlay.append(toolbar);
            button.addEventListener("click", () => {
              detail.runAction(TerminalAction.CopySelection).catch(error => uiFixture.statuses.push(error.message));
            }, { signal: detail.signal });
            detail.signal.addEventListener("abort", () => { uiFixture.cleanup++; toolbar.remove(); }, { once: true });
          }
          toolbar.hidden = !detail.selection.active;
          toolbar.querySelector("button").disabled = !detail.connected || detail.selection.copying;
          const rect = detail.rects[0] ?? { left: 0, top: 0 };
          toolbar.style.left = `${rect.left}px`;
          toolbar.style.top = `${rect.top}px`;
          uiFixture.events.push({ selection: detail.selection, viewport: detail.viewport, size: detail.canvasSize,
            rects: detail.rects, connected: detail.connected });
        }
      });
      select(uiFixture.workers[0]);
      select(uiFixture.workers[1]);
    });
    await test.waitForFunction(() => uiCustom.selection.active && uiFixture.events.at(-1).selection.active);
    check(await test.evaluate(() => !uiDefault.element.shadowRoot.querySelector(".copy-selection").hidden &&
      uiCustom.element.shadowRoot.querySelector(".copy-selection").hidden), "Replacement affected another view or did not hide the default");
    check(await test.evaluate(() => getComputedStyle(document.querySelector(".host-copy")).borderTopStyle === "dashed"),
      "Host stylesheet did not reach custom light-DOM controls");
    check(await test.evaluate(() => getComputedStyle(uiCustom.element.shadowRoot.querySelector(".highlight")).opacity === "0.65"),
      "Selection highlighting was not externally styleable");
    const aligned = () => test.evaluate(() => {
      const canvas = uiCustom.element.shadowRoot.querySelector("canvas").getBoundingClientRect();
      const overlay = uiFixture.overlay.getBoundingClientRect();
      const highlight = uiCustom.element.shadowRoot.querySelector(".highlight").getBoundingClientRect();
      const rect = uiFixture.events.at(-1).rects[0];
      const probe = document.createElement("div");
      probe.style.cssText = `position:absolute;pointer-events:none;left:${rect.left}px;top:${rect.top}px;width:${rect.width}px;height:${rect.height}px`;
      uiFixture.overlay.append(probe);
      const positioned = probe.getBoundingClientRect();
      probe.remove();
      return ["left", "top", "width", "height"].every(key => Math.abs(canvas[key] - overlay[key]) < .1) &&
        ["left", "top", "width", "height"].every(key => Math.abs(positioned[key] - highlight[key]) < .1);
    });
    check(await aligned(), "Overlay coordinates did not match the displayed canvas at DPR2");

    stage = "stable rendering and custom input isolation";
    const before = await test.evaluate(() => ({ events: uiFixture.events.length, actions: uiFixture.hostActions,
      commands: uiFixture.commands.length }));
    await test.evaluate(() => { for (let index = 0; index < 20; index++) uiFixture.workers[1].frame(); });
    await test.evaluate(() => Promise.resolve());
    check(await test.evaluate(count => uiFixture.events.length === count && uiFixture.mounts === 1, before.events),
      "Unchanged terminal frames rebuilt/notified selection controls");
    await test.locator(".host-field").click();
    await test.keyboard.type("notes");
    await test.keyboard.press("ControlOrMeta+a");
    await test.keyboard.press("Backspace");
    await test.keyboard.type("kept");
    await test.keyboard.press("Enter");
    await test.locator(".host-field").dispatchEvent("wheel", { deltaY: 100, bubbles: true });
    const isolation = await test.evaluate(before => ({
      focused: document.activeElement === document.querySelector(".host-field"),
      value: document.querySelector(".host-field").value,
      commands: uiFixture.commands.slice(before.commands), actions: uiFixture.hostActions - before.actions
    }), before);
    check(isolation.focused && isolation.value === "kept" && isolation.commands.length === 0 && isolation.actions === 0,
      `Custom controls lost focus or leaked terminal input/bindings: ${JSON.stringify(isolation)}`);
    await test.locator(".host-copy").click();
    await test.waitForFunction(() => uiFixture.writes.at(-1) === "HELLO" && !uiCustom.selection.copying);
    check(await test.evaluate(() => uiFixture.commands.filter(command => command.type === "copy").length === 1 &&
      uiFixture.events.some(event => event.selection.copying) && uiCustom.selection.active),
    "Custom Copy did not use the shared guarded action/progress state");

    stage = "resize, scrolling, and default augmentation";
    await test.evaluate(() => { document.getElementById("b").style.width = "280px"; document.getElementById("b").style.height = "180px"; });
    await test.waitForFunction(() => uiFixture.events.at(-1).size.width <= 280 && uiFixture.events.at(-1).size.width > 0);
    check(await aligned(), "Overlay did not follow resized thumbnail geometry");
    await test.evaluate(() => {
      document.getElementById("b").style.transform = "scale(.75)";
      document.getElementById("b").style.width = "240px";
    });
    await test.waitForFunction(() => uiFixture.events.at(-1).size.width <= 240);
    check(await aligned(), "Overlay-local coordinates applied ancestor scaling twice");
    await test.evaluate(() => {
      document.getElementById("b").style.transform = "";
      document.getElementById("b").style.width = "280px";
    });
    await test.waitForFunction(() => uiFixture.events.at(-1).size.width === 280);
    await test.evaluate(() => {
      uiFixture.workers[1].top = 15;
      uiFixture.workers[1].selection.ranges = [{ row: 7, startColumn: 3, endColumn: 8 }];
      uiFixture.workers[1].frame();
    });
    await test.waitForFunction(() => uiFixture.events.at(-1).viewport.top === 15);
    check(await aligned(), "Selection overlay did not follow viewport scrolling");
    check(await test.evaluate(() => !uiCustom.element.shadowRoot.querySelector(".return-live").hidden),
      "Replacing Copy also removed Return to live");
    await test.evaluate(() => { uiFixture.replace = false; uiCustom.refreshSelectionUI(); });
    await test.waitForFunction(() => !uiCustom.element.shadowRoot.querySelector(".copy-selection").hidden);
    check(await test.evaluate(() => uiFixture.mounts === 1 && uiFixture.overlay === document.querySelector("#b .hex1b-selection-overlay")),
      "Forced refresh replaced the host's DOM layer");
    await test.locator("#b .copy-selection").focus();
    await test.keyboard.press("Enter");
    await test.waitForFunction(() => uiFixture.writes.length === 2 && !uiCustom.selection.copying);

    stage = "UI error visibility and lifecycle";
    await test.evaluate(() => { uiFixture.fail = true; uiCustom.refreshSelectionUI(); });
    await test.waitForFunction(() => uiCustom.element.shadowRoot.querySelector(".inspection-message").textContent.includes("host overlay failed"));
    check(await test.evaluate(() => uiFixture.overlay.hidden &&
      uiCustom.element.shadowRoot.querySelector(".copy-selection").hidden &&
      !uiCustom.element.shadowRoot.querySelector(".return-live").hidden && uiCustom.element.shadowRoot.querySelector(".highlight")),
    "A UI failure hid independent feedback/highlights or silently fell back to default Copy");
    await test.evaluate(() => { uiFixture.fail = false; uiCustom.refreshSelectionUI(); });
    await test.waitForFunction(() => !uiFixture.overlay.hidden);
    await test.evaluate(() => {
      uiCustom.refreshSelectionUI();
      uiCustom.dispose();
      uiDefault.dispose();
    });
    check(await test.evaluate(() => uiFixture.signal.aborted && uiFixture.cleanup === 1 &&
      uiFixture.workers.every(worker => worker.stopped) && !document.querySelector(".hex1b-terminal")),
    "Disposal left host components, listeners, or workers alive");
    check(errors.length === 0, `Browser errors: ${errors.join("; ")}`);
    return { passed: true, covered: ["default and replaced UI", "host CSS", "highlight CSS parts",
      "DPR2 canvas coordinates", "stable DOM and frame deduplication", "custom input/focus isolation",
      "shared copy actions", "resize and scrolling", "ancestor transforms", "augmentation", "independent feedback",
      "UI errors", "abort cleanup"], browserErrors: errors };
  } catch (error) {
    throw new Error(`${stage}: ${error.message}`);
  } finally {
    await context.close();
  }
}
