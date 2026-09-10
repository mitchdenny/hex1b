import {
  WebTerminal, InputRoute, TerminalAction, defaultInputBindings, MIN_FONT_SIZE, MAX_FONT_SIZE,
  type WebTerminalOptions, type WebTerminalHandle, type TerminalInput, type InputBinding,
  type TerminalSelection, type TerminalViewport, type SelectionUIEvent, type TerminalStats,
  type TerminalRendererKind, type TerminalRendererPreference, type TerminalProgress,
  type TerminalShellIntegration, type TerminalCloseDetails
} from "@hex1b/web-terminal";

const container = document.createElement("div");
const minimumFontSize: 8 = MIN_FONT_SIZE;
const maximumFontSize: 32 = MAX_FONT_SIZE;
console.log(minimumFontSize, maximumFontSize);
const bindings: InputBinding[] = defaultInputBindings();
const options: WebTerminalOptions = {
  url: new URL("wss://example.test/terminal"),
  workerUrl: new URL("/web-terminal/terminal-worker.js", "https://example.test"),
  signal: new AbortController().signal,
  scale: "auto",
  renderer: "auto",
  sizing: { mode: "fixed", columns: 80, rows: 24, fontSize: 16 },
  font: { family: "Terminal Font", faces: [{ url: "/font.woff2", weight: "200 700" }] },
  actions: {
    inspect(context, args, input) {
      const selection: TerminalSelection = context.selection;
      const viewport: TerminalViewport = context.viewport;
      const intent: Readonly<TerminalInput> | undefined = input;
      if (typeof args === "string") context.terminal.paste(args);
      // @ts-expect-error Custom action arguments require narrowing.
      args.message;
      return { selection, viewport, intent };
    }
  },
  inputBindings: [
    ...bindings,
    { id: "remove-default", match: input => input.type === "key", route: InputRoute.Continue },
    { id: "inspect", match: input => input.type === "pointer" && input.button === "left", action: "inspect" }
  ],
  onInput(input, context) {
    if (input.type === "key") {
      const repeat: boolean = input.repeat;
      if (repeat) return InputRoute.Consume;
    }
    if (context.readOnly) return { action: TerminalAction.ClearSelection };
    return undefined;
  },
  onStats(stats, text) {
    const state: TerminalStats = stats;
    const revision: number | undefined = state.revision;
    const mirror: string | undefined = text;
    const renderer: TerminalRendererKind | undefined = stats.renderer;
    const reason: string | undefined = stats.rendererFallbackReason;
    console.log(revision, mirror, renderer, reason);
  },
  onTitleChange(title) {
    const currentTitle: string = title;
    console.log(currentTitle);
  },
  onClose(details) {
    const close: TerminalCloseDetails = details;
    console.log(close.code, close.reason, close.wasClean);
    // @ts-expect-error Native close details are immutable.
    close.code = 1000;
  },
  onProgressChange(progress) {
    const current: TerminalProgress = progress;
    if (current.state === "normal") console.log(current.percentage);
    // @ts-expect-error Progress callback values are readonly.
    current.percentage = 100;
  },
  onShellIntegrationChange(shell) {
    const current: TerminalShellIntegration = shell;
    if (current.phase === "finished") console.log(current.lastExitCode);
    // @ts-expect-error Shell callback values are readonly.
    current.phase = "executing";
  },
  onSelectionUI(event) {
    const notification: SelectionUIEvent = event;
    notification.preventDefault();
    const { selection, viewport, overlay, rects, runAction, signal } = notification.detail;
    if (selection.status === "valid") {
      const text: string = selection.text;
      console.log(text);
    }
    if (viewport.available) {
      const generation: string = viewport.generation;
      console.log(generation);
    }
    if (rects[0]) overlay.style.left = `${rects[0].left}px`;
    overlay.addEventListener("click", () => void runAction(TerminalAction.CopySelection), { signal });
    // @ts-expect-error Event snapshots are readonly.
    selection.copying = true;
    // @ts-expect-error Event ranges are readonly.
    selection.ranges[0].row = 42;
    // @ts-expect-error Built-in scrolling requires numeric arguments.
    runAction(TerminalAction.ScrollLines, "20");
  }
};

const mountedTerminal: WebTerminal = await WebTerminal.mount(container, options);
const terminal: WebTerminalHandle = mountedTerminal;
const readOnly: boolean = terminal.readOnly;
terminal.setReadOnly(!readOnly);
mountedTerminal.setReadOnly(false);
const title: string = terminal.title;
const mountedTitle: string = mountedTerminal.title;
console.log(title, mountedTitle);
const progress: TerminalProgress = terminal.progress;
const shell: TerminalShellIntegration = mountedTerminal.shellIntegration;
console.log(progress, shell);
const forcedRenderer: TerminalRendererPreference = "webgl2";
const forcedOptions: WebTerminalOptions = { ...options, renderer: forcedRenderer };
console.log(forcedOptions);
const copied: string = await terminal.runAction(TerminalAction.CopySelection, { clear: true });
const pasted: string = await terminal.runAction(TerminalAction.PasteClipboard);
await terminal.runAction(TerminalAction.ScrollLines, -20);
await terminal.runAction("inspect", { source: "button" });
await terminal.runAction(context => context.terminal.screenText);
terminal.setSizing({ mode: "auto" });
terminal.resize(80, 24);
terminal.refreshSelectionUI();
terminal.paste(copied + pasted);
terminal.dispose();

// @ts-expect-error Input policy is changed through the runtime setter.
terminal.readOnly = true;
// @ts-expect-error Read-only policy must be a boolean.
terminal.setReadOnly("true");
// @ts-expect-error Consumers must use mount to obtain an initialized handle.
new WebTerminal(options);
// @ts-expect-error The current title is read-only on the public handle.
terminal.title = "host title";
// @ts-expect-error The WebTerminal class exposes only a getter.
mountedTerminal.title = "host title";
// @ts-expect-error Terminal activity is read-only on the public handle.
terminal.progress = { state: "none", percentage: null };
// @ts-expect-error Shell phase is read-only, not a way to execute commands.
terminal.shellIntegration.phase = "executing";
// @ts-expect-error Fixed sizing requires both grid dimensions.
terminal.setSizing({ mode: "fixed", columns: 80 });
// @ts-expect-error Paste is text, not bytes.
terminal.paste(new Uint8Array());
// @ts-expect-error Copy options use a boolean.
terminal.runAction(TerminalAction.CopySelection, { clear: "yes" });
// @ts-expect-error A built-in scrolling action requires its argument.
terminal.runAction(TerminalAction.ScrollLines);
// @ts-expect-error Selection UI ownership must be decided synchronously.
const asyncUI: WebTerminalOptions = { url: "/terminal", onSelectionUI: async () => {} };
// @ts-expect-error Routing callbacks must be synchronous.
const asyncRouting: WebTerminalOptions = { url: "/terminal", onInput: async () => InputRoute.Browser };
// @ts-expect-error Async binding match functions are not supported.
const asyncBinding: InputBinding = { id: "async", match: async () => true, route: InputRoute.Consume };
// @ts-expect-error Worker entries must be URL strings or URL objects.
const invalidWorker: WebTerminalOptions = { url: "/terminal", workerUrl: 42 };
// @ts-expect-error Only supported rendering backends are selectable.
const invalidRenderer: WebTerminalOptions = { url: "/terminal", renderer: "canvas2d" };
// @ts-expect-error Routing decisions specify exactly one route or action.
const ambiguous: InputBinding = { id: "both", match: () => true, route: InputRoute.Browser, action: "inspect" };
// @ts-expect-error Protocol internals are not public package exports.
import { decodeFrame } from "@hex1b/web-terminal";
// @ts-expect-error Internal modules are not public package subpaths.
import { TerminalRenderer } from "@hex1b/web-terminal/renderer.js";
