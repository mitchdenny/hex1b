async page => {
  const origin = page.url().match(/^https?:\/\/[^/]+/)?.[0] || "http://localhost:5291";
  const context = await page.context().browser().newContext({
    viewport: { width: 1440, height: 1000 }, deviceScaleFactor: 2
  });
  const test = await context.newPage();
  const browserErrors = [];
  const covered = [];
  let stage = "mounting deterministic terminal views";
  test.on("pageerror", error => browserErrors.push(error.message));
  test.setDefaultTimeout(5000);
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const section = async (name, run) => { stage = name; await run(); covered.push(name); };
  const tick = () => test.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
  const snapshot = () => test.evaluate(() => fixture.snapshot());
  const commandsSince = async (before, peer = "a") => test.evaluate(({ before, peer }) =>
    fixture.commands.slice(before.commands.length).filter(command => command.peer === peer), { before, peer });
  const appCommands = commands => commands.filter(command => ["input", "paste", "key", "mouse"].includes(command.type));
  const point = async (name, x = 8, y = 2) => test.evaluate(({ name, x, y }) => {
    const terminal = fixture.views[name];
    const box = terminal.element.shadowRoot.querySelector("canvas").getBoundingClientRect();
    return { x: box.x + (x + .5) * box.width / terminal.geometry.columns,
      y: box.y + (y + .5) * box.height / terminal.geometry.rows };
  }, { name, x, y });
  const right = async (name = "a") => {
    const position = await point(name);
    await test.mouse.click(position.x, position.y, { button: "right" });
    await tick();
  };
  const wheel = async name => {
    const count = await test.evaluate(name => fixture.events.filter(event => event.peer === name && event.type === "wheel").length, name);
    await test.mouse.wheel(0, 40);
    await test.waitForFunction(({ name, count }) =>
      fixture.events.filter(event => event.peer === name && event.type === "wheel").length > count, { name, count });
  };
  const select = async (name = "a", row = 2, pending = false) => {
    const start = await point(name, 7, row);
    const end = await point(name, 11, row);
    await test.mouse.move(start.x, start.y);
    await test.mouse.down();
    await test.mouse.move(end.x, end.y, { steps: 3 });
    await test.mouse.up();
    if (!pending) await test.waitForFunction(name => {
      const selection = fixture.views[name].selection;
      return selection.active && !selection.pending && selection.text === "alpha";
    }, name);
  };
  const clear = async (name = "a") => {
    await test.evaluate(name => fixture.views[name].clearSelection(), name);
    await test.waitForFunction(name => fixture.views[name].selection.status === "none", name);
  };
  const idle = async () => test.waitForFunction(() => fixture.clipboardCalls.every(call => call.settled));
  const worker = async (name, changes) => test.evaluate(({ name, changes }) => fixture.workers[name].configure(changes), { name, changes });
  const configure = async changes => test.evaluate(changes => Object.assign(fixture.config, changes), changes);
  const script = async (kind, mode, text = "clipboard payload") =>
    test.evaluate(({ kind, mode, text }) => fixture.script(kind, mode, text), { kind, mode, text });
  const releaseClipboard = async () => {
    await test.evaluate(() => fixture.releaseClipboard());
    await idle();
    await tick();
  };
  const releaseFrames = async (name = "a") => test.evaluate(name => fixture.workers[name].release(), name);
  const waitForClipboard = async (before, kind) => test.waitForFunction(({ count, kind }) =>
    fixture.clipboardCalls.length === count + 1 && fixture.clipboardCalls.at(-1).kind === kind &&
    fixture.clipboardCalls.at(-1).ready, { count: before.clipboardCalls.length, kind });
  try {
    await test.goto(`${origin}/health`);
    await test.setContent(`<style>body{margin:20px}#views{display:flex;gap:20px}.host{width:400px;height:240px;flex:none}</style>
      <div id="views"><div class="host" id="a"></div><div class="host" id="b"></div><div class="host" id="c"></div></div>
      <button id="outside">Outside terminal focus</button>`);
    await test.evaluate(async () => {
      const { validateHistory } = await import("/web-terminal/protocol.js");
      const { WebTerminal, TerminalAction, InputRoute, defaultInputBindings } = await import("/web-terminal/index.js");
      const fixture = window.fixture = {
        views: {}, workers: {}, commands: [], clipboardCalls: [], clipboardPlans: [], gates: [],
        inputErrors: [], observed: [], events: [], actions: [], config: {
          contextAction: true, browserRight: false, consumeLeft: false, consumeWheel: false,
          intercept: "", failAction: false, failWhen: false
        },
        constants: { TerminalAction, InputRoute, ids: defaultInputBindings().map(binding => binding.id) },
        script(kind, mode, text) { this.clipboardPlans.push({ kind, mode, text }); },
        releaseClipboard() { this.gates.splice(0).forEach(gate => gate()); },
        snapshot() {
          return JSON.parse(JSON.stringify({
            commands: this.commands, clipboardCalls: this.clipboardCalls, inputErrors: this.inputErrors,
            observed: this.observed, events: this.events, actions: this.actions,
            selections: Object.fromEntries(Object.entries(this.views).map(([name, view]) => [name, view.selection])),
            viewports: Object.fromEntries(Object.entries(this.views).map(([name, view]) => [name, view.viewport]))
          }));
        }
      };
      const clipboard = async (kind, data) => {
        const plan = fixture.clipboardPlans.shift() ?? { kind, mode: "ok", text: "clipboard payload" };
        const call = { kind, mode: plan.mode, ready: false, settled: false };
        fixture.clipboardCalls.push(call);
        try {
          if (plan.kind !== kind) throw new Error(`Expected mocked clipboard ${plan.kind}, received ${kind}`);
          if (kind === "write") call.text = await (await data[0].getType("text/plain")).text();
          if (kind === "writeText") call.text = data;
          call.ready = true;
          if (plan.mode === "delay") await new Promise(resolve => fixture.gates.push(resolve));
          if (plan.mode === "deny") throw new DOMException(`Mock ${kind} denied`, "NotAllowedError");
          if (kind === "read") throw new Error("Unexpected clipboard.read; fixture intercepts it without touching the system clipboard");
          return kind === "readText" ? plan.text : undefined;
        } finally { call.settled = true; }
      };
      Object.defineProperty(navigator, "clipboard", { configurable: true, value: {
        readText: () => clipboard("readText"), read: () => clipboard("read"),
        write: items => clipboard("write", items), writeText: text => clipboard("writeText", text)
      } });
      // Observe terminal cancellation first, then block browser/OS clipboard shortcuts.
      // Native paste payloads below are deterministic ClipboardEvents, never system clipboard reads.
      document.addEventListener("keydown", event => {
        if (event.ctrlKey || event.metaKey) event.preventDefault();
      });
      document.addEventListener("copy", event => event.preventDefault(), true);
      document.addEventListener("cut", event => event.preventDefault(), true);

      window.Worker = class extends EventTarget {
        constructor() {
          super();
          this.name = ["a", "b", "c"][Object.keys(fixture.workers).length];
          fixture.workers[this.name] = this;
          this.revision = 0;
          this.tracking = 0;
          this.held = [];
          this.hold = "";
          this.history = {
            generation: "1", buffer: "main", totalRows: 60, liveTop: 48, top: 48, following: true,
            rowIds: [], requestId: 0,
            selection: { requestId: 0, status: "none", mode: "character", ranges: [], text: null },
            copy: null
          };
          this.rows();
        }
        rows() {
          this.history.rowIds = Array.from({ length: 12 }, (_, row) =>
            String(Number(this.history.generation) * 1000 + this.history.top + row + 1));
        }
        line(rowId) { return `row${String(Number(rowId) % 1000).padStart(3, "0")} alpha bravo charlie delta`.padEnd(40); }
        emit(data) { if (!this.stopped) this.dispatchEvent(new MessageEvent("message", { data })); }
        frame(reason = "") {
          this.rows();
          const history = structuredClone(this.history);
          validateHistory(history, 40, 12);
          const message = {
            type: "geometry", columns: 40, rows: 12, cellWidth: 10, cellHeight: 20, hyperlinks: [], title: "",
            progress: { state: "none", percentage: null }, shellIntegration: { phase: "unknown", lastExitCode: null },
            mouseTracking: this.tracking, peer: { id: this.name, primaryId: "native", isPrimary: false },
            revision: ++this.revision, history, text: history.rowIds.map(rowId => this.line(rowId)).join("\n")
          };
          if (this.hold && (this.hold === reason || (this.hold === "selection" && reason.startsWith("selection:"))))
            this.held.push(message);
          else queueMicrotask(() => this.emit(message));
        }
        release() {
          this.hold = "";
          this.held.splice(0).forEach(message => this.emit(message));
        }
        configure(changes) {
          if (changes.hold !== undefined) this.hold = changes.hold;
          if (changes.tracking !== undefined) this.tracking = changes.tracking;
          if (changes.buffer || changes.generation) {
            this.history.generation = String(Number(this.history.generation) + 1);
            this.history.buffer = changes.buffer ?? this.history.buffer;
            this.history.totalRows = this.history.buffer === "main" ? 60 : 12;
            this.history.liveTop = this.history.totalRows - 12;
            this.history.top = this.history.liveTop;
            this.history.following = true;
            this.history.selection = { ...this.history.selection, status: "none", ranges: [], text: null };
            this.history.copy = null;
          }
          if (changes.connected === false) { this.emit({ type: "disconnected" }); return; }
          if (changes.connected === true) this.emit({ type: "connected" });
          this.frame();
        }
        select(command) {
          if (command.action === "clear") {
            this.history.selection = { requestId: command.requestId, status: "none",
              mode: this.history.selection.mode, ranges: [], text: null };
            return;
          }
          if (command.generation !== this.history.generation) throw new Error("Selection used a stale producer generation");
          if (command.action === "start") this.anchor = { rowId: command.rowId, column: command.column };
          const row = this.history.rowIds.indexOf(command.rowId);
          if (row < 0 || this.anchor.rowId !== command.rowId) throw new Error("Fixture selections must stay on one presented row");
          const startColumn = Math.min(this.anchor.column, command.column);
          const endColumn = Math.max(this.anchor.column, command.column) + 1;
          this.history.selection = {
            requestId: command.requestId, status: "valid", mode: command.mode,
            ranges: [{ row, startColumn, endColumn }],
            text: this.line(command.rowId).slice(startColumn, endColumn)
          };
        }
        postMessage(message) {
          if (message.type === "init") {
            queueMicrotask(() => {
              this.emit({ type: "connected" });
              this.frame();
              queueMicrotask(() => this.emit({ type: "stats", stats: { revision: this.revision, connected: true } }));
            });
          } else if (message.type === "command") {
            const command = message.command;
            fixture.commands.push({ peer: this.name, ...structuredClone(command) });
            if (command.type === "selection") {
              this.select(command);
              this.frame(`selection:${command.action}`);
            } else if (command.type === "copy") {
              const selection = this.history.selection;
              if (command.generation !== this.history.generation || command.selectionRequestId !== selection.requestId ||
                  selection.status !== "valid") throw new Error("Copy did not reference the authoritative selection");
              this.history.copy = { requestId: command.requestId, status: "valid", text: selection.text };
              this.frame("copy");
            } else if (command.type === "viewport") {
              this.history.requestId = command.requestId;
              this.history.top = command.live ? this.history.liveTop :
                Math.min(this.history.liveTop, Math.max(0, this.history.top + command.delta));
              this.history.following = this.history.top === this.history.liveTop;
              if (command.extend) throw new Error("Unexpected drag-scroll selection in this fixture");
              this.frame("viewport");
            }
          }
        }
        terminate() { this.stopped = true; }
      };
      const observe = name => (input, context) => {
        fixture.observed.push({ peer: name, input, context: {
          ownsTerminal: context.terminal === fixture.views[name], buffer: context.buffer,
          mouseCaptured: context.mouseCaptured, historical: context.historical, readOnly: context.readOnly,
          connected: context.connected, peer: context.peer.id, selection: context.selection.status,
          viewportAvailable: context.viewport.available
        } });
        if (name !== "b") return;
        if (fixture.config.intercept === "throw" && input.type === "key" && input.key === "q")
          throw new Error("Mock policy callback exploded");
        if (fixture.config.intercept === "consume" && input.type === "key" && input.ctrl && input.key === "l") return "consume";
        if (fixture.config.intercept === "text" && input.type === "text") return "consume";
        if (fixture.config.intercept === "paste" && input.type === "paste") return "consume";
        if (fixture.config.intercept === "browser-printable" && input.type === "key" && input.key === "z") return "browser";
        if (fixture.config.intercept === "action" && input.type === "key" && input.key === "F8")
          return { action: "host.context", args: { source: "interceptor" } };
      };
      const hostAction = (context, args, input) => {
        fixture.actions.push({ peer: context.peer.id, ownsTerminal: context.terminal === fixture.views.b,
          buffer: context.buffer, args, input: input ?? null });
        if (fixture.config.failAction) return Promise.reject(new Error("Mock host action exploded"));
        return "host result";
      };
      for (const name of ["a", "b", "c"]) {
        fixture.views[name] = await WebTerminal.mount(document.getElementById(name), {
          url: "/mock-bindings", readOnly: name === "c",
          sizing: { mode: "fixed", columns: 40, rows: 12 },
          onInput: observe(name),
          onInputError: error => fixture.inputErrors.push({ peer: name, message: error.message }),
          ...(name === "b" ? {
            actions: { "host.context": hostAction },
            inputBindings: [
              { id: "clipboard.context-click", match: input => input.type === "pointer" && input.button === "right",
                when: (context, input) => fixture.config.contextAction && context.connected && input.button === "right",
                action: "host.context", args: { source: "binding" } },
              { id: "clipboard.copy-key", remove: true },
              { id: "host.browser-right", match: input => input.type === "pointer" && input.button === "right",
                when: () => fixture.config.browserRight, route: "browser" },
              { id: "host.control-l", match: input => input.type === "key" && input.ctrl && input.key === "l",
                route: "application" },
              { id: "host.key-consume", match: input => input.type === "key" && input.key === "F2",
                when: () => { if (fixture.config.failWhen) throw new Error("Mock binding predicate exploded"); return true; },
                route: "consume" },
              { id: "host.left-consume", match: input => input.type === "pointer" && input.button === "left",
                when: () => fixture.config.consumeLeft, route: "consume" },
              { id: "host.wheel-consume", match: input => input.type === "wheel",
                when: () => fixture.config.consumeWheel, route: "consume" },
              { id: "host.function-action", match: input => input.type === "key" && input.key === "F9",
                action: hostAction, args: { source: "function" } }
            ]
          } : {})
        });
        const terminal = fixture.views[name];
        const canvas = terminal.element.shadowRoot.querySelector("canvas");
        terminal.element.addEventListener("keydown", event => fixture.events.push({
          peer: name, type: "key", key: event.key, ctrl: event.ctrlKey, meta: event.metaKey,
          defaultPrevented: event.defaultPrevented
        }), true);
        for (const type of ["pointerdown", "contextmenu", "wheel"]) canvas.addEventListener(type, event =>
          fixture.events.push({ peer: name, type, defaultPrevented: event.defaultPrevented,
            ...(type === "wheel" ? { deltaX: event.deltaX, deltaY: event.deltaY, deltaMode: event.deltaMode } : {}) }),
        { passive: true });
      }
      fixture.nativePaste = (name, text) => {
        const input = fixture.views[name].element.shadowRoot.querySelector("textarea");
        const data = new DataTransfer();
        data.setData("text/plain", text);
        const event = new ClipboardEvent("paste", { clipboardData: data, bubbles: true, composed: true, cancelable: true });
        input.dispatchEvent(event);
        input.dispatchEvent(new InputEvent("input", { data: text, inputType: "insertFromPaste", bubbles: true }));
        return event.defaultPrevented;
      };
    });
    await tick();
    check(await test.evaluate(() => Object.values(fixture.views).every(view =>
      view.connected && view.viewport.available && !view.viewport.pending && view.geometry.columns === 40 &&
      view.element.shadowRoot.querySelector("canvas").getBoundingClientRect().width === 400)),
    "Views did not settle with validated history in fixed-size shadow DOM mounts");
    check(await test.evaluate(() => fixture.constants.TerminalAction.CopyOrPaste === "copyOrPaste" &&
      fixture.constants.InputRoute.Browser === "browser" &&
      ["clipboard.copy-key", "browser.shortcuts", "terminal.keys", "clipboard.context-click"]
        .every(id => fixture.constants.ids.includes(id))), "Public input constants/default bindings were not exported");
    covered.push(stage);

    await section("right-click copies, clears after acknowledgment, then pastes in main and alternate buffers", async () => {
      for (const buffer of ["main", "alternate"]) {
        await worker("a", { buffer });
        await select();
        const before = await snapshot();
        await worker("a", { hold: "selection:clear" });
        await right();
        await idle();
        const copied = await snapshot();
        check(copied.clipboardCalls.length === before.clipboardCalls.length + 1 &&
          copied.clipboardCalls.at(-1).kind === "write" && copied.clipboardCalls.at(-1).text === "alpha",
        `${buffer}: first right-click did not write authoritative selected text exactly once`);
        check(copied.selections.a.pending && !copied.selections.a.canExtend && !copied.selections.a.ranges.length,
          `${buffer}: successful copy did not enter pending-clear state`);
        check((await commandsSince(before)).filter(command => command.type === "copy").length === 1 &&
          !appCommands(await commandsSince(before)).length, `${buffer}: copying emitted application input`);
        await script("readText", "ok", `${buffer} paste`);
        await right();
        const pasted = await snapshot();
        check(pasted.clipboardCalls.length === copied.clipboardCalls.length + 1 &&
          pasted.clipboardCalls.at(-1).kind === "readText", `${buffer}: pending clear did not count as cleared`);
        const delivered = appCommands(await commandsSince(before));
        check(delivered.length === 1 && delivered[0].type === "paste" && delivered[0].text === `${buffer} paste`,
          `${buffer}: second right-click did not send one unencoded HWT paste`);
        await releaseFrames();
        await test.waitForFunction(() => fixture.views.a.selection.status === "none");
      }
      await worker("a", { buffer: "main" });
    });

    await section("copy denial retains selection and surfaces an error without falling through", async () => {
      await select();
      await script("write", "deny");
      const before = await snapshot();
      await right();
      await idle();
      const after = await snapshot();
      check(after.selections.a.active && after.selections.a.text === "alpha" &&
        after.clipboardCalls.length === before.clipboardCalls.length + 1, "Failed copy lost selection or read the clipboard");
      check(after.inputErrors.length === before.inputErrors.length + 1 &&
        after.inputErrors.at(-1).message.includes("Mock write denied"), "Clipboard rejection bypassed onInputError");
      check(await test.locator("#a .inspection-message").isVisible(), "Clipboard rejection was not visible");
      check(!appCommands(await commandsSince(before)).length &&
        !(await commandsSince(before)).some(command => command.type === "selection"), "Failed copy fell through or cleared selection");
    });

    await section("slow producer copy, rapid context clicks, and newer selection during slow write", async () => {
      await worker("a", { hold: "copy" });
      const before = await snapshot();
      await right();
      check((await snapshot()).selections.a.copying, "Delayed producer copy was not pending");
      await right();
      const rapid = await snapshot();
      check(rapid.clipboardCalls.length === before.clipboardCalls.length + 1 &&
        (await commandsSince(before)).filter(command => command.type === "copy").length === 1,
      "Rapid right-click started a second clipboard operation");
      check(rapid.selections.a.active && rapid.inputErrors.at(-1).message.includes("still in progress"),
        "Rapid right-click did not preserve selection/report its busy state");
      await releaseFrames();
      await idle();
      await tick();
      await select();
      await script("write", "delay");
      const delayed = await snapshot();
      await right();
      await waitForClipboard(delayed, "write");
      const writing = await snapshot();
      check(writing.selections.a.active && writing.selections.a.copying &&
        !(await commandsSince(delayed)).some(command => command.type === "selection" && command.action === "clear"),
      "Selection cleared before clipboard write succeeded");
      await select("a", 3);
      const newer = (await snapshot()).selections.a.requestId;
      await releaseClipboard();
      const after = await snapshot();
      check(after.selections.a.active && after.selections.a.requestId === newer && !after.selections.a.copying,
        "Completing an older clipboard write cleared a newer selection");
      check(!appCommands(await commandsSince(delayed)).length, "Slow copy leaked application input");
    });

    await section("pending selection cannot become an accidental paste", async () => {
      await clear();
      await worker("a", { hold: "selection" });
      await select("a", 4, true);
      const before = await snapshot();
      check(before.selections.a.pending && before.selections.a.canExtend, "Selection fixture was not pending");
      await right();
      const after = await snapshot();
      check(after.clipboardCalls.length === before.clipboardCalls.length &&
        !appCommands(await commandsSince(before)).length, "Pending selection performed clipboard paste or application input");
      check(after.inputErrors.length === before.inputErrors.length + 1, "Pending selection copy failure was not reported");
      await releaseFrames();
      await test.waitForFunction(() => fixture.views.a.selection.active);
    });

    await section("clipboard read denial and stale paste invalidation", async () => {
      await clear();
      await script("readText", "deny");
      const denied = await snapshot();
      await right();
      check((await snapshot()).inputErrors.at(-1).message.includes("Mock readText denied") &&
        !appCommands(await commandsSince(denied)).length, "Denied clipboard read delivered input or hid its error");
      for (const invalidation of ["input", "disconnect", "buffer", "generation", "focus", "selection"]) {
        await clear();
        await script("readText", "delay", `stale ${invalidation}`);
        const before = await snapshot();
        await right();
        await waitForClipboard(before, "readText");
        if (invalidation === "input") await test.keyboard.type("x");
        if (invalidation === "disconnect") await worker("a", { connected: false });
        if (invalidation === "buffer") await worker("a", { buffer: "alternate" });
        if (invalidation === "generation") await worker("a", { generation: true });
        if (invalidation === "focus") await test.locator("#outside").click();
        if (invalidation === "selection") await select("a", 5);
        await releaseClipboard();
        const after = await snapshot();
        check(!appCommands(await commandsSince(before)).some(command => command.type === "paste"),
          `${invalidation}: stale clipboard content was delivered`);
        check(after.clipboardCalls.length === before.clipboardCalls.length + 1 &&
          after.inputErrors.length === before.inputErrors.length + 1, `${invalidation}: pending paste did not reject once`);
        if (invalidation === "disconnect") await worker("a", { connected: true });
        if (invalidation === "buffer") await worker("a", { buffer: "main" });
      }
    });

    await section("read-only copy remains available without clipboard reads or application input", async () => {
      await worker("c", { tracking: 1002 });
      await select("c");
      const before = await snapshot();
      await right("c");
      await idle();
      await right("c");
      await test.evaluate(() => fixture.views.c.runAction("copyOrPaste"));
      const rejected = await test.evaluate(async () => {
        try { await fixture.views.c.runAction("pasteClipboard"); return false; }
        catch (error) { return error.message.includes("does not accept input"); }
      });
      const after = await snapshot();
      check(rejected && after.clipboardCalls.length === before.clipboardCalls.length + 1 &&
        after.clipboardCalls.at(-1).kind === "write" && after.clipboardCalls.at(-1).text === "alpha",
      "Read-only view failed to copy or attempted to read clipboard");
      check(!appCommands(await commandsSince(before, "c")).length &&
        await test.locator("#c textarea").isDisabled(), "Read-only inspection emitted application input");
      check(after.observed.some(item => item.peer === "c" && item.context.readOnly && item.context.mouseCaptured),
        "Read-only/capture context was not supplied to policy");
    });

    await section("application right-button capture and Shift override retain gesture ownership", async () => {
      await clear();
      await worker("a", { tracking: 1002 });
      const captured = await snapshot();
      await right();
      const app = appCommands(await commandsSince(captured));
      check(app.length === 2 && app[0].type === "mouse" && app[0].button === "right" && app[0].action === "down" &&
        app[1].button === "right" && app[1].action === "up" && app[0].x === 8 && app[0].y === 2,
      `Application capture changed right down/up intent: ${JSON.stringify(app)}`);
      check((await snapshot()).clipboardCalls.length === captured.clipboardCalls.length, "Application mouse capture invoked local clipboard");
      await worker("a", { tracking: 0 });
      await select();
      await worker("a", { tracking: 1002 });
      const local = await snapshot();
      const position = await point("a");
      await test.keyboard.down("Shift");
      await test.mouse.move(position.x, position.y);
      await test.mouse.down({ button: "right" });
      await test.keyboard.up("Shift");
      await wheel("a");
      await test.mouse.up({ button: "right" });
      await idle();
      await tick();
      check(!appCommands(await commandsSince(local)).length &&
        (await snapshot()).clipboardCalls.length === local.clipboardCalls.length + 1,
      "Releasing Shift during local right-click gesture sent application mouse/wheel/up");
      await test.evaluate(() => fixture.views.a.scrollLines(-3));
      const historical = await snapshot();
      await right();
      check((await snapshot()).clipboardCalls.length === historical.clipboardCalls.length + 1 &&
        appCommands(await commandsSince(historical)).every(command => command.type === "paste"),
      "Historical context did not override application capture");
      await worker("a", { tracking: 0 });
    });

    await section("per-view overrides replace defaults, remove copy, and bypass browser shortcuts", async () => {
      await select("b");
      const before = await snapshot();
      await right("b");
      const after = await snapshot();
      check(after.actions.length === before.actions.length + 1 &&
        after.actions.at(-1).args.source === "binding" && after.actions.at(-1).ownsTerminal &&
        after.actions.at(-1).input.button === "right" && after.actions.at(-1).input.point.x === 8,
      "Same-id context-click replacement did not receive its own view/input/args");
      check(after.clipboardCalls.length === before.clipboardCalls.length &&
        !appCommands(await commandsSince(before, "b")).length, "Replaced context click fell through to built-in clipboard/application");
      await test.keyboard.press("Meta+c");
      check((await snapshot()).clipboardCalls.length === before.clipboardCalls.length &&
        (await snapshot()).events.at(-1).defaultPrevented === false, "Removed copy default still consumed the browser shortcut");
      await test.keyboard.press("Control+l");
      const keys = appCommands(await commandsSince(before, "b"));
      check(keys.length === 1 && keys[0].type === "key" && keys[0].key === "l" && keys[0].ctrl,
        `Custom Ctrl+L did not override browser shortcut: ${JSON.stringify(keys)}`);
      await configure({ intercept: "consume" });
      const intercepted = await snapshot();
      await test.keyboard.press("Control+l");
      check(!appCommands(await commandsSince(intercepted, "b")).length, "Per-view onInput did not precede bindings");
      await configure({ intercept: "" });
      await select();
      const isolated = await snapshot();
      await test.keyboard.press("Meta+c");
      await idle();
      check((await snapshot()).clipboardCalls.length === isolated.clipboardCalls.length + 1 &&
        (await snapshot()).selections.a.active, "Custom removal leaked into the other mount or default key copy cleared selection");
      check(!(await commandsSince(isolated, "b")).length, "Default mount copy leaked commands to custom mount");
    });

    await section("custom key, left-pointer, and wheel consume without unmatched application releases", async () => {
      await configure({ consumeLeft: true, consumeWheel: true });
      await worker("b", { tracking: 1002 });
      const position = await point("b");
      await test.mouse.move(position.x, position.y);
      const before = await snapshot();
      await test.mouse.down();
      await configure({ consumeLeft: false, consumeWheel: false });
      await test.keyboard.down("Shift");
      await test.mouse.move(position.x + 20, position.y + 20, { steps: 3 });
      await wheel("b");
      await test.mouse.up();
      await test.keyboard.up("Shift");
      await test.keyboard.down("F2");
      await test.keyboard.down("F2");
      await test.keyboard.up("F2");
      await tick();
      check(!appCommands(await commandsSince(before, "b")).length, "Consume route leaked keys, mouse motion, wheel, or unmatched up");
      const repeated = (await snapshot()).observed.slice(before.observed.length).filter(item => item.input.key === "F2");
      check(repeated.length === 2 && repeated.every(item => item.input.code === "F2") &&
        repeated[0].input.repeat === false && repeated[1].input.repeat === true,
      "Keyboard binding input lost its physical key code or repeat state");
      await configure({ consumeWheel: true });
      const wheelBefore = await snapshot();
      await wheel("b");
      await tick();
      const wheelAfter = await snapshot();
      const nativeWheel = wheelAfter.events.slice(wheelBefore.events.length).find(event => event.type === "wheel");
      check(wheelAfter.observed.slice(wheelBefore.observed.length).some(item =>
        item.peer === "b" && item.input.type === "wheel" && item.input.deltaY > 0 &&
        item.input.deltaY === nativeWheel?.deltaY && item.input.deltaMode === nativeWheel?.deltaMode &&
        Number.isInteger(item.input.point.x)) && !appCommands(await commandsSince(wheelBefore, "b")).length,
      `Standalone wheel did not normalize/consume its event: ${JSON.stringify(wheelAfter.observed.slice(wheelBefore.observed.length))}`);
      const appBefore = await snapshot();
      await test.mouse.down();
      await wheel("b");
      await test.mouse.up();
      await tick();
      const held = appCommands(await commandsSince(appBefore, "b"));
      check(held.some(command => command.action === "wheel") &&
        held[0].action === "down" && held.at(-1).action === "up",
      `Custom wheel interception stole an already-held application gesture: ${JSON.stringify(held)}`);
      check(!(await snapshot()).observed.slice(appBefore.observed.length).some(item => item.input.type === "wheel"),
        "Wheel policy ran during an already-held application gesture");
      await configure({ consumeWheel: false });
      await worker("b", { tracking: 0 });
    });

    await section("custom browser route permits contextmenu while consume/action routes suppress it", async () => {
      await configure({ contextAction: false, browserRight: true });
      const before = await snapshot();
      await right("b");
      const after = await snapshot();
      const menu = after.events.slice(before.events.length).find(event => event.type === "contextmenu");
      const down = after.events.slice(before.events.length).find(event => event.type === "pointerdown");
      check(menu && !menu.defaultPrevented && down && !down.defaultPrevented, "Browser route cancelled native contextmenu or pointerdown");
      check(!appCommands(await commandsSince(before, "b")).length &&
        after.clipboardCalls.length === before.clipboardCalls.length, "Browser route leaked into application/clipboard");
      await test.keyboard.press("Escape");
      await configure({ contextAction: true, browserRight: false });
      const consumed = await snapshot();
      await right("b");
      check((await snapshot()).events.slice(consumed.events.length).some(event =>
        event.type === "contextmenu" && event.defaultPrevented), "Action route did not suppress contextmenu");
    });

    await section("policy, predicate, and asynchronous action failures are visible and fail closed", async () => {
      for (const failure of ["policy", "predicate", "action"]) {
        await configure({ intercept: failure === "policy" ? "throw" : "", failWhen: failure === "predicate",
          failAction: failure === "action" });
        await test.evaluate(() => fixture.views.b.focus());
        const before = await snapshot();
        await test.keyboard.press(failure === "policy" ? "q" : failure === "predicate" ? "F2" : "F9");
        await tick();
        const after = await snapshot();
        check(after.inputErrors.length === before.inputErrors.length + 1 &&
          after.inputErrors.at(-1).message.includes("exploded"), `${failure}: missing onInputError notification`);
        check(!appCommands(await commandsSince(before, "b")).length &&
          after.clipboardCalls.length === before.clipboardCalls.length, `${failure}: failed callback fell through to application input`);
        check(await test.locator("#b .inspection-message").isVisible() &&
          (await test.locator("#b .inspection-message").textContent()).includes("exploded"),
        `${failure}: error was not visibly surfaced inside the mount`);
      }
      await configure({ intercept: "", failWhen: false, failAction: false });
    });

    await section("ordinary text, IME, Dead/AltGraph and native paste are routed exactly once", async () => {
      await clear();
      await test.evaluate(() => fixture.views.a.focus());
      const before = await snapshot();
      await test.keyboard.type("v");
      await test.keyboard.insertText("猫");
      for (const afterEnd of [false, true]) {
        await test.evaluate(afterEnd => {
          const input = fixture.views.a.element.shadowRoot.querySelector("textarea");
          const text = afterEnd ? "終" : "先";
          input.dispatchEvent(new CompositionEvent("compositionstart", { bubbles: true }));
          input.dispatchEvent(new KeyboardEvent("keydown", { key: "Process", code: "KeyA", isComposing: true,
            bubbles: true, composed: true, cancelable: true }));
          input.dispatchEvent(new InputEvent("input", { data: "intermediate", isComposing: true, bubbles: true }));
          if (!afterEnd) input.dispatchEvent(new InputEvent("input", { data: text, bubbles: true }));
          input.dispatchEvent(new CompositionEvent("compositionend", { data: text, bubbles: true }));
          if (afterEnd) input.dispatchEvent(new InputEvent("input", { data: text, bubbles: true }));
        }, afterEnd);
        await tick();
      }
      await test.evaluate(() => {
        const input = fixture.views.a.element.shadowRoot.querySelector("textarea");
        for (const key of ["Dead", "AltGraph"]) {
          const event = new KeyboardEvent("keydown", { key, code: "KeyE", ctrlKey: key === "AltGraph",
            altKey: key === "AltGraph", bubbles: true, composed: true, cancelable: true });
          if (key === "AltGraph") Object.defineProperty(event, "getModifierState", { value: name => name === "AltGraph" });
          input.dispatchEvent(event);
        }
      });
      await test.keyboard.insertText("é€");
      const plain = appCommands(await commandsSince(before));
      check(plain.every(command => command.type === "input") &&
        plain.map(command => command.text).join("") === "v猫先終é€",
      `Committed text/IME was duplicated, lost, or encoded as a key: ${JSON.stringify(plain)}`);
      check(!(await snapshot()).observed.slice(before.observed.length).some(item =>
        item.input.type === "key" && ["Dead", "AltGraph", "Process"].includes(item.input.key)),
      "IME/Dead/AltGraph keydown incorrectly entered policy");
      for (const shortcut of ["Control+v", "Meta+v"]) {
        const nativeBefore = await snapshot();
        await test.keyboard.press(shortcut);
        const observed = (await snapshot()).events.slice(nativeBefore.events.length).find(event =>
          event.type === "key" && event.key === "v");
        check(observed && !observed.defaultPrevented, `${shortcut} was not browser-owned before fixture clipboard safety barrier`);
        check(await test.evaluate(text => fixture.nativePaste("a", text), `native ${shortcut}`),
          `${shortcut}: terminal did not consume the supplied native paste event`);
        const delivered = appCommands(await commandsSince(nativeBefore));
        check(delivered.length === 1 && delivered[0].type === "paste" && delivered[0].text === `native ${shortcut}` &&
          (await snapshot()).clipboardCalls.length === nativeBefore.clipboardCalls.length,
        `${shortcut}: native paste duplicated input or called clipboard.readText`);
      }
      const explicit = await snapshot();
      await test.evaluate(() => fixture.views.a.paste("raw\r\n\u001b[200~literal"));
      const delivered = appCommands(await commandsSince(explicit));
      check(delivered.length === 1 && delivered[0].type === "paste" && delivered[0].text === "raw\r\n\u001b[200~literal",
        "Public paste pre-encoded producer input or sent multiple messages");
      await test.evaluate(() => fixture.views.b.focus());
      await configure({ intercept: "browser-printable" });
      const browserPrintable = await snapshot();
      await test.keyboard.type("z");
      const nativeText = appCommands(await commandsSince(browserPrintable, "b"));
      check(nativeText.length === 1 && nativeText[0].type === "input" && nativeText[0].text === "z",
        "Browser-owned printable keydown blocked or duplicated its native committed text");
      await configure({ intercept: "text" });
      const consumed = await snapshot();
      await test.keyboard.type("z");
      await configure({ intercept: "paste" });
      await test.evaluate(() => fixture.nativePaste("b", "discard"));
      check(!appCommands(await commandsSince(consumed, "b")).length, "Custom committed-text/native-paste consume leaked input");
      await configure({ intercept: "" });
    });

    await section("named actions share toolbar copy/history behavior and custom action arguments", async () => {
      await select();
      const before = await snapshot();
      await test.locator("#a .copy-selection").click();
      await idle();
      await test.evaluate(() => fixture.views.a.runAction("copySelection"));
      await idle();
      const copied = await snapshot();
      check(copied.clipboardCalls.length === before.clipboardCalls.length + 2 &&
        copied.clipboardCalls.slice(-2).every(call => call.kind === "write" && call.text === "alpha") &&
        copied.selections.a.active, "Toolbar and named copy did not share non-clearing clipboard behavior");
      await test.evaluate(() => fixture.views.a.runAction("scrollLines", -4));
      await test.waitForFunction(() => !fixture.views.a.viewport.followTail);
      await test.locator("#a .return-live").click();
      await test.waitForFunction(() => fixture.views.a.viewport.followTail);
      await test.evaluate(async () => {
        await fixture.views.a.runAction("scrollLines", -2);
        await fixture.views.a.runAction("scrollToLive");
        await fixture.views.a.runAction("clearSelection");
      });
      await test.waitForFunction(() => fixture.views.a.selection.status === "none" && fixture.views.a.viewport.followTail);
      const viewportCommands = (await commandsSince(before)).filter(command => command.type === "viewport");
      check(viewportCommands.length === 4 && viewportCommands[0].delta === -4 && viewportCommands[1].live &&
        viewportCommands[2].delta === -2 && viewportCommands[3].live, "Toolbar/named history actions did not send matching viewport commands");
      await script("readText", "ok", "named paste");
      await test.evaluate(() => fixture.views.a.runAction("pasteClipboard"));
      check(appCommands(await commandsSince(before)).length === 1 &&
        appCommands(await commandsSince(before))[0].text === "named paste", "Named paste action did not use the HWT paste path");
      const result = await test.evaluate(() => fixture.views.b.runAction("host.context", { source: "toolbar" }));
      check(result === "host result" && (await snapshot()).actions.at(-1).args.source === "toolbar" &&
        (await snapshot()).actions.at(-1).input === null, "Public custom named action did not receive args or return its result");
      await configure({ intercept: "action" });
      await test.evaluate(() => fixture.views.b.focus());
      await test.keyboard.press("F8");
      await test.keyboard.press("F9");
      const actions = (await snapshot()).actions.slice(-2);
      check(actions[0].args.source === "interceptor" && actions[0].input.key === "F8" &&
        actions[1].args.source === "function" && actions[1].input.key === "F9",
      "Interceptor action result or function-valued binding did not execute exactly once");
    });

    await section("view disposal releases every mocked worker", async () => {
      await test.evaluate(() => Object.values(fixture.views).forEach(view => view.dispose()));
      check(await test.evaluate(() => Object.values(fixture.workers).every(worker => worker.stopped) &&
        document.querySelectorAll(".hex1b-terminal").length === 0 &&
        fixture.clipboardPlans.length === 0 && fixture.gates.length === 0),
      "Fixture leaked mounted DOM, workers, or unresolved clipboard scripts");
      await tick();
      check(browserErrors.length === 0, `Unexpected browser errors: ${browserErrors.join("; ")}`);
    });
    return { passed: true, covered, browserErrors };
  } catch (error) {
    const state = await snapshot().catch(() => null);
    throw new Error(`bindings.browser.js [${stage}]: ${error.message}\n${JSON.stringify({
      covered, browserErrors, commands: state?.commands.slice(-12), clipboardCalls: state?.clipboardCalls.slice(-5),
      inputErrors: state?.inputErrors.slice(-5), events: state?.events.slice(-8),
      observed: state?.observed.slice(-8), selections: state?.selections
    })}`);
  } finally {
    await test.evaluate(() => {
      window.fixture?.releaseClipboard();
      Object.values(window.fixture?.views ?? {}).forEach(view => view.dispose());
    }).catch(() => {});
    await context.close();
  }
}
