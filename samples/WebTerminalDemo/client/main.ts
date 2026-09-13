import { WebTerminal, MIN_FONT_SIZE, MAX_FONT_SIZE, getCmdlineUrl, type TerminalCloseDetails, type TerminalCommandMark } from "@hex1b/web-terminal";

interface TerminalInstance {
  id: string;
  name: string;
  scene: string;
  columns: number;
  rows: number;
  peerCount: number;
  paused: boolean | null;
  rate: number | null;
  batch: number | null;
  tapes: DemoTape[];
  tapePlayback: TapePlayback | null;
}

interface DemoTape {
  id: string;
  scene: string;
  name: string;
  description: string;
}

interface TapePlayback {
  tapeId: string;
  name: string;
  state: string;
  completedCommands: number | null;
  error: string | null;
  diagnostics: string[];
}

/**
 * A single command-mark rail entry, demo-only bookkeeping layered on top of the library's
 * `onCommandMarkChange` callback. `anchor` captures the viewport coordinates in effect when the
 * command line started, letting the demo scroll back to (roughly) that point later. It is null
 * when no viewport snapshot was available yet, in which case the dot cannot be jumped to.
 */
interface CommandMarkEntry {
  phase: TerminalCommandMark["phase"];
  exitCode: number | null;
  cmdlineUrl: string | null;
  anchor: { generation: string; top: number } | null;
}

interface TerminalView {
  id: string;
  instance: TerminalInstance;
  element: HTMLElement;
  controller: AbortController;
  connectionController?: AbortController;
  connectionId?: string;
  phase: "connecting" | "connected" | "closed";
  closure?: ViewClosure;
  terminal?: WebTerminal;
  stats: Partial<WebTerminal["stats"]>;
  text: string;
  transport: "direct" | "hmp1";
  viewport?: WebTerminal["viewport"];
  selection?: WebTerminal["selection"];
  commandMarks: CommandMarkEntry[];
}

interface ViewClosure {
  title: string;
  summary: string;
  detail: string;
  reconnect: boolean;
  close?: TerminalCloseDetails;
}

declare global {
  interface Window {
    webTerminalViews: Map<string, TerminalView>;
    webTerminalStats: Partial<WebTerminal["stats"]>;
    webTerminalScreenText: string;
  }
}

function elementAt<T extends Element>(root: ParentNode, selector: string, type: new () => T): T {
  const element = root.querySelector(selector);
  if (!(element instanceof type)) throw new Error(`Missing or invalid playground element: ${selector}`);
  return element;
}

const byId = (id: string) => elementAt(document, `#${id}`, HTMLElement);
const button = (id: string) => elementAt(document, `#${id}`, HTMLButtonElement);
const input = (id: string) => elementAt(document, `#${id}`, HTMLInputElement);
const select = (id: string) => elementAt(document, `#${id}`, HTMLSelectElement);
const message = (error: unknown) => error instanceof Error ? error.message : String(error);
const workspace = byId("workspace");
const instancesSelect = select("instances");
const views = new Map<string, TerminalView>();
const tapeSelections = new Map<string, string>();
const pendingTapeActions = new Set<string>();
let instances: TerminalInstance[] = [];
let selected: TerminalView | undefined;
let nextView = 0;
let zIndex = 0;
let refreshing: Promise<void> | undefined;
let shuttingDown = false;
const gridPresets = ["80x24", "80x25", "100x30", "120x40", "132x43", "160x50", "200x60", "240x80"];

// Playground diagnostics only; the mounted component has no window-global state.
window.webTerminalViews = views;
window.webTerminalStats = {};
window.webTerminalScreenText = "";

function report(message: string, level = "info") {
  byId("status").textContent = message;
  byId("status").dataset.level = level;
}

async function api(path: string, method = "GET", body?: object): Promise<unknown> {
  const response = await fetch(path, {
    method,
    ...(body === undefined ? {} : { headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) })
  });
  if (!response.ok) throw new Error(`${response.status}: ${await response.text() || response.statusText}`);
  return response.status === 204 ? null : response.json();
}

function readInstance(value: unknown): TerminalInstance {
  if (typeof value !== "object" || value === null ||
      !("id" in value) || typeof value.id !== "string" ||
      !("name" in value) || typeof value.name !== "string" ||
      !("scene" in value) || typeof value.scene !== "string" ||
      !("columns" in value) || typeof value.columns !== "number" ||
      !("rows" in value) || typeof value.rows !== "number" ||
      !("peerCount" in value) || typeof value.peerCount !== "number" ||
      !("paused" in value) || value.paused !== null && typeof value.paused !== "boolean" ||
      !("rate" in value) || value.rate !== null && typeof value.rate !== "number" ||
      !("batch" in value) || value.batch !== null && typeof value.batch !== "number" ||
      !("tapes" in value) || !Array.isArray(value.tapes) ||
      !("tapePlayback" in value)) {
    throw new Error("The server returned an invalid terminal instance");
  }
  return {
    id: value.id, name: value.name, scene: value.scene,
    columns: value.columns, rows: value.rows, peerCount: value.peerCount,
    paused: value.paused, rate: value.rate, batch: value.batch,
    tapes: value.tapes.map(readTape), tapePlayback: readTapePlayback(value.tapePlayback)
  };
}

function readTape(value: unknown): DemoTape {
  if (typeof value !== "object" || value === null ||
      !("id" in value) || typeof value.id !== "string" ||
      !("scene" in value) || typeof value.scene !== "string" ||
      !("name" in value) || typeof value.name !== "string" ||
      !("description" in value) || typeof value.description !== "string") {
    throw new Error("The server returned an invalid tape");
  }
  return { id: value.id, scene: value.scene, name: value.name, description: value.description };
}

function readTapePlayback(value: unknown): TapePlayback | null {
  if (value === null) return null;
  if (typeof value !== "object" ||
      !("tapeId" in value) || typeof value.tapeId !== "string" ||
      !("name" in value) || typeof value.name !== "string" ||
      !("state" in value) || typeof value.state !== "string" ||
      !["running", "cancelling", "completed", "cancelled", "failed"].includes(value.state) ||
      !("completedCommands" in value) || value.completedCommands !== null && typeof value.completedCommands !== "number" ||
      !("error" in value) || value.error !== null && typeof value.error !== "string" ||
      !("diagnostics" in value) || !Array.isArray(value.diagnostics) ||
      !value.diagnostics.every(item => typeof item === "string")) {
    throw new Error("The server returned an invalid tape playback status");
  }
  return {
    tapeId: value.tapeId, name: value.name, state: value.state,
    completedCommands: value.completedCommands, error: value.error, diagnostics: value.diagnostics
  };
}

function mounted(view: TerminalView): WebTerminal {
  if (!view.terminal) throw new Error("The terminal view is not mounted");
  return view.terminal;
}

function action(button: HTMLButtonElement, operation: () => unknown, refresh?: () => void) {
  button.addEventListener("click", async () => {
    button.disabled = true;
    try { await operation(); }
    catch (error) { report(message(error), "error"); }
    finally { button.disabled = false; refresh?.(); }
  });
}

function updateInstanceControls() {
  const instance = instances.find(item => item.id === instancesSelect.value);
  button("attach").disabled = !instance;
  button("terminate").disabled = !instance;
  for (const id of ["pause", "apply-rate"]) button(id).disabled = !instance || instance.scene === "shell";
  for (const id of ["rate", "batch"]) input(id).disabled = !instance || instance.scene === "shell";
  byId("pause").textContent = instance?.paused ? "Resume" : "Pause";
  byId("pause").setAttribute("aria-pressed", String(instance?.paused ?? false));
  for (const id of ["rate", "batch"] as const) {
    if (document.activeElement !== input(id)) input(id).value = String(instance?.[id] ?? (id === "rate" ? 30 : 20));
  }
  updateTapeControls();
}

function updateTapeControls() {
  const instance = instances.find(item => item.id === instancesSelect.value);
  const tapes = instance?.tapes ?? [];
  const picker = select("tapes");
  const catalog = JSON.stringify(tapes);
  if (picker.dataset.catalog !== catalog) {
    picker.replaceChildren(...tapes.map(tape => new Option(tape.name, tape.id)));
    if (!tapes.length) picker.add(new Option("No tapes for this scene", ""));
    picker.dataset.catalog = catalog;
  }
  const previous = instance ? tapeSelections.get(instance.id) ?? instance.tapePlayback?.tapeId : undefined;
  const tape = tapes.find(tape => tape.id === previous) ?? tapes[0];
  picker.value = tape?.id ?? "";
  picker.title = tape?.description ?? "Choose an Interactive shell terminal to play a tape.";
  if (instance && tape) tapeSelections.set(instance.id, tape.id);
  const playback = instance?.tapePlayback;
  const busy = playback?.state === "running" || playback?.state === "cancelling";
  const pending = !!instance && pendingTapeActions.has(instance.id);
  picker.disabled = !tapes.length || busy || pending;
  button("play-tape").disabled = !tape || busy || pending;
  button("stop-tape").disabled = playback?.state !== "running" || pending;
  const status = byId("tape-status");
  status.dataset.instance = instance?.id ?? "";
  status.dataset.state = playback?.state ?? "idle";
  status.dataset.level = playback?.state === "failed" ? "error" : playback?.state === "completed" ? "ready" : "info";
  const text = !instance ? "Choose an existing terminal to play a tape."
    : !tapes.length ? "Tapes are currently bundled for the Interactive shell scene."
    : !playback ? `${tape?.description} Start at an idle shell prompt.`
    : [
      `${playback.name}: ${playback.state}${playback.completedCommands === null ? "" : ` (${playback.completedCommands} commands)`}.`,
      ...(playback.error ? [playback.error] : []),
      ...playback.diagnostics
    ].join("\n");
  if (status.textContent !== text) status.textContent = text;
}

async function controlTape(cancel: boolean) {
  const instance = instances.find(item => item.id === instancesSelect.value);
  if (!instance) {
    report("Choose an existing terminal to play a tape.", "error");
    return;
  }
  const tapeId = select("tapes").value;
  pendingTapeActions.add(instance.id);
  updateTapeControls();
  try {
    await api(`/api/terminals/${encodeURIComponent(instance.id)}/tape`, cancel ? "DELETE" : "POST",
      cancel ? undefined : { tapeId });
    await refreshInstances();
  } catch (error) {
    report(message(error), "error");
  } finally {
    pendingTapeActions.delete(instance.id);
    updateTapeControls();
  }
}

async function refreshInstances(preferred?: string): Promise<void> {
  if (shuttingDown) return;
  if (refreshing) {
    await refreshing;
    return refreshInstances(preferred);
  }
  refreshing = loadInstances(preferred);
  try {
    await refreshing;
  } finally {
    refreshing = undefined;
  }
}

async function loadInstances(preferred?: string) {
  const response = await api("/api/terminals");
  if (!Array.isArray(response)) throw new Error("The server returned an invalid terminal list");
  instances = response.map(readInstance);
  for (const id of tapeSelections.keys()) {
    if (!instances.some(instance => instance.id === id)) tapeSelections.delete(id);
  }
  const selectedId = preferred || instancesSelect.value;
  instancesSelect.replaceChildren(...instances.map(instance => {
    const option = document.createElement("option");
    option.value = instance.id;
    option.textContent = `${instance.name} - ${instance.columns}x${instance.rows}, ${instance.peerCount} peers`;
    return option;
  }));
  if (instances.some(instance => instance.id === selectedId)) instancesSelect.value = selectedId;
  updateInstanceControls();
}

function metrics(view: Pick<TerminalView, "id" | "stats" | "text" | "transport"> & { instance: Pick<TerminalInstance, "name"> }, force = false) {
  if (!force && selected !== view) return;
  const stats = view.stats || {};
  window.webTerminalStats = stats;
  window.webTerminalScreenText = view.text || "";
  const number = (value: number | undefined, digits = 1) => Number(value || 0).toFixed(digits);
  byId("metric-fps").textContent = number(stats.fps);
  byId("metric-renderer").textContent = stats.renderer || "Initializing";
  byId("metric-renderer").title = stats.rendererFallbackReason || "";
  byId("metric-transport").textContent = view.transport === "hmp1" ? "HMP1 relay → HWT1" : "Direct HWT1";
  byId("metric-received").textContent = number(stats.receivedKBps);
  byId("metric-workload").textContent = number(stats.workloadMBps, 2);
  byId("metric-projection").textContent = number(stats.captureMs, 2);
  byId("metric-cpu").textContent = number(stats.rendererCpuMs, 2);
  byId("metric-cells").textContent = number(stats.lastChangedCells, 0);
  byId("metric-images").textContent = `${stats.imageCount || 0} / ${number((stats.textureBytes ?? 0) / 1048576, 2)}`;
  byId("metric-atlas").textContent = `${stats.atlasGlyphs || 0} / ${number((stats.atlasBytes ?? 0) / 1048576, 1)}`;
  byId("metric-uploads").textContent = number((stats.imageUploadBytes ?? 0) / 1048576, 2);
  byId("metric-revision").textContent = `${stats.revision || 0} / ${stats.fullFrames || 0}`;
  byId("warnings").textContent = [
    ...(stats.rendererFallbackReason ? [`WebGL2 fallback: ${stats.rendererFallbackReason}`] : []),
    ...(stats.warnings || [])
  ].join("\n");
  byId("screen-mirror").textContent = view.text || "";
  byId("selected-view").textContent = `${view.instance.name} / view ${view.id}`;
}

function selectView(view: TerminalView) {
  selected?.element.classList.remove("selected");
  selected = view;
  view.element.classList.add("selected");
  view.element.style.zIndex = String(++zIndex);
  if (instances.some(instance => instance.id === view.instance.id)) {
    instancesSelect.value = view.instance.id;
    updateInstanceControls();
  }
  metrics(view);
}

function updateSizingControls(view: TerminalView) {
  const terminal = view.terminal;
  const primary = view.phase === "connected" && terminal?.connected && terminal.peer.isPrimary;
  const sizing = terminal?.sizing;
  const auto = primary && sizing?.mode === "auto";
  elementAt(view.element, ".font-smaller", HTMLButtonElement).disabled = !auto || sizing.fontSize <= MIN_FONT_SIZE;
  elementAt(view.element, ".font-larger", HTMLButtonElement).disabled = !auto || sizing.fontSize >= MAX_FONT_SIZE;
  elementAt(view.element, ".font-size", HTMLElement).textContent = auto ? `${sizing.fontSize}px` : "Fit";
  const resolution = elementAt(view.element, ".view-resolution", HTMLSelectElement);
  resolution.disabled = !primary;
  const fixed = sizing?.mode === "fixed" ? `${sizing.columns}x${sizing.rows}` : undefined;
  const custom = elementAt(resolution, '[value="custom"]', HTMLOptionElement);
  custom.textContent = fixed ? `Custom ${fixed}` : "Custom";
  resolution.value = !primary ? "follow" : auto ? "auto" : fixed && gridPresets.includes(fixed) ? fixed : "custom";
}

function updateViewControls(view: TerminalView) {
  const connected = view.phase === "connected" && !!view.terminal?.connected;
  elementAt(view.element, ".take-primary", HTMLButtonElement).disabled =
    !connected || !!view.terminal?.peer.isPrimary || view.terminal?.peer.id === null;
  elementAt(view.element, ".resync", HTMLButtonElement).disabled = !connected;
  elementAt(view.element, ".trigger-failure", HTMLButtonElement).disabled = !connected;
  elementAt(view.element, ".view-failure", HTMLSelectElement).disabled = !connected;
  elementAt(view.element, ".thumbnail", HTMLButtonElement).disabled = !!view.closure && !view.closure.reconnect;
  elementAt(view.element, ".reconnect-view", HTMLButtonElement).disabled =
    view.phase !== "closed" || !view.closure?.reconnect;
  updateSizingControls(view);
}

function showClosure(view: TerminalView, closure: ViewClosure) {
  if (view.controller.signal.aborted) return closure;
  const mount = elementAt(view.element, ".terminal-mount", HTMLElement);
  const hadTerminalFocus = mount.contains(document.activeElement);
  view.closure = closure;
  view.phase = "closed";
  view.element.dataset.phase = "closed";
  view.element.dataset.connected = "false";
  view.element.dataset.primary = "false";
  mount.inert = true;
  elementAt(view.element, ".closed-title", HTMLElement).textContent = closure.title;
  elementAt(view.element, ".closed-summary", HTMLElement).textContent = closure.summary;
  elementAt(view.element, ".closed-detail", HTMLElement).textContent = closure.detail;
  elementAt(view.element, ".closed-overlay", HTMLElement).hidden = false;
  elementAt(view.element, ".view-role", HTMLElement).textContent = closure.reconnect ? "Disconnected" : "Ended";
  elementAt(view.element, ".view-role", HTMLElement).title = closure.detail;
  const status = elementAt(view.element, ".view-status", HTMLElement);
  status.textContent = closure.title;
  status.title = closure.summary;
  status.dataset.level = closure.close?.code === 1000 || closure.close?.code === 4000 ? "info" : "error";
  updateViewControls(view);
  view.terminal?.dispose();
  if (hadTerminalFocus) {
    elementAt(view.element, closure.reconnect ? ".reconnect-view" : ".dismiss-view", HTMLButtonElement)
      .focus({ preventScroll: true });
  }
  return closure;
}

/** Rebuilds the left-edge command-mark dots for `view` from `view.commandMarks`. */
function renderCommandMarksRail(view: TerminalView) {
  const rail = elementAt(view.element, ".command-marks-rail", HTMLElement);
  rail.replaceChildren(...view.commandMarks.map(entry => {
    const dot = document.createElement("button");
    dot.type = "button";
    dot.className = "command-mark-dot";
    dot.setAttribute("role", "listitem");
    dot.dataset.phase = entry.phase;
    if (entry.exitCode !== null) dot.dataset.exit = entry.exitCode === 0 ? "ok" : "error";
    const description = entry.phase === "finished"
      ? `Command finished${entry.exitCode === null ? "" : ` (exit ${entry.exitCode})`}`
      : entry.phase === "executing" ? "Command running" : "Command entered";
    const detail = [description, entry.cmdlineUrl ?? ""].filter(Boolean).join("\n");
    dot.title = entry.anchor ? `${detail}\nClick to jump back to this point.`
      : `${detail}\nThis point can no longer be located in scrollback.`;
    dot.setAttribute("aria-label", dot.title.replace(/\n/g, ". "));
    dot.disabled = entry.anchor === null;
    dot.addEventListener("click", () => jumpToCommandMark(view, entry));
    return dot;
  }));
}

/**
 * Scrolls `view`'s terminal back to where `entry.anchor` was captured, using a relative delta from
 * the current viewport top (the library only exposes relative scrolling). Refuses to jump when the
 * viewport generation has changed since capture (resize/reflow/reset/history-clear/alt-screen
 * transition), since row coordinates are no longer comparable at that point.
 */
function jumpToCommandMark(view: TerminalView, entry: CommandMarkEntry) {
  const viewport = view.viewport;
  if (!view.terminal || !entry.anchor || !viewport?.available || viewport.generation !== entry.anchor.generation) {
    report("That point is no longer reachable in scrollback (the view was reset, resized, or reflowed since).", "error");
    return;
  }
  view.terminal.scrollLines(entry.anchor.top - viewport.top);
}

function connectionClosed(view: TerminalView, close: TerminalCloseDetails) {
  const stage = view.phase === "connected" ? "After mounting" : "Before mounting completed";
  const summary = close.code === 4000 ? "The producer has ended. This terminal cannot be reconnected."
    : close.code === 1006 ? "The connection ended without a WebSocket close frame. This does not prove the producer ended."
    : close.code === 1008 ? "The server closed this view because of a policy violation."
    : close.code === 1011 ? "The server reported a failure while serving this view."
    : close.code === 1001 ? "The server is going away. The producer may no longer be available."
    : "The server closed this view. The producer may still be running; reconnect explicitly to attach again.";
  showClosure(view, {
    title: close.code === 4000 ? "Terminal ended" : close.code === 1006 ? "Connection lost"
      : close.code === 1008 ? "View rejected" : close.code === 1011 ? "Server error" : "View closed",
    summary,
    detail: `${stage} · WebSocket ${close.code} · ${close.wasClean ? "Clean" : "Incomplete"} closing handshake\n` +
      (close.reason || "The browser did not receive a close reason."),
    reconnect: close.code !== 4000,
    close
  });
}

function changeSizing(view: TerminalView, sizing: Parameters<WebTerminal["setSizing"]>[0]) {
  try { mounted(view).setSizing(sizing); }
  catch (error) { report(message(error), "error"); }
  finally { updateSizingControls(view); }
}

function moveAndResize(view: TerminalView) {
  const signal = view.controller.signal;
  const handles: [HTMLElement, boolean][] = [
    [elementAt(view.element, ".view-titlebar", HTMLElement), false],
    [elementAt(view.element, ".resize-handle", HTMLElement), true]
  ];
  for (const [handle, resize] of handles) {
    let gesture: { pointer: number; x: number; y: number; left: number; top: number; width: number; height: number } | undefined;
    handle.addEventListener("pointerdown", event => {
      if (event.button !== 0 || event.target instanceof Element && event.target.closest("button")) return;
      event.preventDefault();
      selectView(view);
      gesture = {
        pointer: event.pointerId, x: event.clientX, y: event.clientY,
        left: view.element.offsetLeft, top: view.element.offsetTop,
        width: view.element.offsetWidth, height: view.element.offsetHeight
      };
      handle.setPointerCapture(event.pointerId);
    }, { signal });
    handle.addEventListener("pointermove", event => {
      if (!gesture || gesture.pointer !== event.pointerId) return;
      const dx = event.clientX - gesture.x;
      const dy = event.clientY - gesture.y;
      if (resize) {
        view.element.style.width = `${Math.max(240, Math.min(3200, gesture.width + dx))}px`;
        view.element.style.height = `${Math.max(180, Math.min(2200, gesture.height + dy))}px`;
      } else {
        view.element.style.left = `${Math.max(0, gesture.left + dx)}px`;
        view.element.style.top = `${Math.max(0, gesture.top + dy)}px`;
      }
    }, { signal });
    const end = (event: PointerEvent) => {
      if (!gesture || gesture.pointer !== event.pointerId) return;
      gesture = undefined;
      if (handle.hasPointerCapture(event.pointerId)) handle.releasePointerCapture(event.pointerId);
    };
    handle.addEventListener("pointerup", end, { signal });
    handle.addEventListener("pointercancel", end, { signal });
    handle.addEventListener("lostpointercapture", () => { gesture = undefined; }, { signal });
  }
}

function closeView(view: TerminalView) {
  view.controller.abort();
  view.connectionController?.abort();
  view.terminal?.dispose();
  view.element.remove();
  views.delete(view.id);
  if (selected === view) {
    selected = undefined;
    const remaining = [...views.values()].at(-1);
    if (remaining) selectView(remaining);
    else {
      metrics({ instance: { name: "" }, id: "", stats: {}, text: "", transport: "direct" }, true);
      byId("selected-view").textContent = "No view selected";
      window.webTerminalStats = {};
      window.webTerminalScreenText = "";
    }
  }
  workspace.classList.toggle("empty", views.size === 0);
  refreshInstances().catch(error => report(message(error), "error"));
}

async function openView(instance: TerminalInstance, { primary = false, thumbnail = false } = {}) {
  if (views.size >= 8) throw new Error("Close a view before opening another (eight views per playground)");
  const transport = select("transport").value;
  if (transport !== "direct" && transport !== "hmp1") throw new Error("Invalid transport selection");
  const id = String(++nextView);
  const element = document.createElement("section");
  element.className = "terminal-window";
  element.tabIndex = -1;
  element.dataset.view = id;
  element.dataset.instance = instance.id;
  element.dataset.transport = transport;
  element.setAttribute("aria-label", `${instance.name}, view ${id}`);
  element.innerHTML = `
    <header class="view-titlebar">
      <span class="view-title"></span><span class="view-role">Joining</span>
      <button class="close-view" title="Close this view; keep the terminal running" aria-label="Close view">Close</button>
    </header>
    <div class="view-tools">
      <button class="take-primary" disabled>Take primary</button>
      <button class="thumbnail">Thumbnail</button>
      <button class="resync" disabled>Resync</button>
      <span class="view-grid"></span>
    </div>
    <div class="view-failures" role="group" aria-label="Connection failure demonstration">
      <label>Failure
        <select class="view-failure" aria-label="Failure condition" disabled>
          <option value="close">Graceful close (1000)</option>
          <option value="abort">Abrupt connection loss</option>
          <option value="policy">Policy violation (1008)</option>
          <option value="server-error">Server error (1011)</option>
        </select>
      </label>
      <button class="trigger-failure" disabled title="Affect only this view, not the producer or other views">Trigger</button>
    </div>
    <div class="view-activity" aria-live="polite">
      <span class="shell-status">Shell activity unknown</span>
      <progress class="activity-progress" max="100" hidden aria-label="Application progress"></progress>
      <span class="progress-status"></span>
      <span class="cwd-status"></span>
      <span class="command-mark-status"></span>
    </div>
    <div class="terminal-stage">
      <div class="command-marks-rail" role="list" aria-label="Command history"></div>
      <div class="terminal-mount"></div>
      <div class="closed-overlay" hidden>
        <div class="closed-card">
          <div role="status" aria-live="polite">
            <h2 class="closed-title"></h2>
            <p class="closed-summary"></p>
            <p class="closed-detail"></p>
          </div>
          <div class="closed-actions">
            <button class="reconnect-view">Reconnect view</button>
            <button class="dismiss-view">Close view</button>
          </div>
        </div>
      </div>
    </div>
    <footer class="view-footer">
      <span class="view-status">Initializing renderer...</span>
      <button class="font-smaller" disabled title="Smaller text; more cells (Auto mode)" aria-label="Decrease terminal font size">-</button>
      <span class="font-size" title="Requested font size in Auto mode; fixed grids scale to fit">Fit</span>
      <button class="font-larger" disabled title="Larger text; fewer cells (Auto mode)" aria-label="Increase terminal font size">+</button>
      <select class="view-resolution" disabled aria-label="Terminal resolution" title="Auto uses the font size; presets hold the grid and scale to fit">
        <option value="follow" hidden>Follow primary</option>
        <option value="auto">Auto</option>
        ${gridPresets.map(grid => `<option value="${grid}">${grid}</option>`).join("")}
        <option value="custom" hidden>Custom</option>
      </select>
    </footer>
    <span class="resize-handle" title="Drag to resize view" aria-hidden="true"></span>`;
  const header = elementAt(element, ".view-title", HTMLElement);
  const fallbackTitle = `${instance.name} / ${id} / ${transport === "hmp1" ? "HMP1 relay" : "Direct HWT1"}`;
  header.textContent = fallbackTitle;
  const width = thumbnail ? 320 : Math.min(1040, Math.max(300, workspace.clientWidth - 64));
  const height = thumbnail ? 240 : Math.min(660, Math.max(300, workspace.clientHeight - 64));
  element.style.width = `${width}px`;
  element.style.height = `${height}px`;
  element.style.left = `${thumbnail ? Math.max(0, workspace.clientWidth - width - 24) : 24 + (views.size % 5) * 32}px`;
  element.style.top = `${24 + (views.size % 5) * (thumbnail ? 48 : 32)}px`;
  workspace.append(element);
  workspace.classList.remove("empty");
  const view: TerminalView = {
    id, instance, element, controller: new AbortController(), phase: "connecting", stats: {}, text: "", transport,
    commandMarks: []
  };
  views.set(id, view);
  selectView(view);
  moveAndResize(view);
  element.addEventListener("pointerdown", event => {
    if (selected !== view) selectView(view);
    if (!(event.target instanceof Element && event.target.closest("button, select, input"))) {
      if (view.phase === "connected" && view.terminal) view.terminal.focus();
      else element.focus({ preventScroll: true });
    }
  }, { capture: true, signal: view.controller.signal });
  element.addEventListener("focusin", () => {
    if (selected !== view) selectView(view);
  }, { signal: view.controller.signal });
  elementAt(element, ".close-view", HTMLButtonElement).addEventListener("click", () => closeView(view), { signal: view.controller.signal });
  elementAt(element, ".dismiss-view", HTMLButtonElement).addEventListener("click", () => closeView(view), { signal: view.controller.signal });
  action(elementAt(element, ".thumbnail", HTMLButtonElement), () => openView(instance, { thumbnail: true }),
    () => updateViewControls(view));
  action(elementAt(element, ".take-primary", HTMLButtonElement), () => mounted(view).requestPrimary(), () => updateViewControls(view));
  action(elementAt(element, ".resync", HTMLButtonElement), () => mounted(view).resync(), () => updateViewControls(view));
  action(elementAt(element, ".reconnect-view", HTMLButtonElement), () => mountView(view), () => updateViewControls(view));
  action(elementAt(element, ".trigger-failure", HTMLButtonElement), async () => {
    if (view.phase !== "connected" || !view.connectionId) throw new Error("Connect this view before triggering a failure");
    await api(`/api/terminals/${encodeURIComponent(instance.id)}/views/${encodeURIComponent(view.connectionId)}/failure`,
      "POST", { mode: elementAt(element, ".view-failure", HTMLSelectElement).value });
  }, () => updateViewControls(view));
  elementAt(element, ".font-smaller", HTMLButtonElement).addEventListener("click", () =>
    changeSizing(view, { mode: "auto", fontSize: mounted(view).sizing.fontSize - 1 }), { signal: view.controller.signal });
  elementAt(element, ".font-larger", HTMLButtonElement).addEventListener("click", () =>
    changeSizing(view, { mode: "auto", fontSize: mounted(view).sizing.fontSize + 1 }), { signal: view.controller.signal });
  const resolution = elementAt(element, ".view-resolution", HTMLSelectElement);
  resolution.addEventListener("change", () => {
    if (resolution.value === "auto") changeSizing(view, { mode: "auto" });
    else {
      const [columns, rows] = resolution.value.split("x").map(Number);
      changeSizing(view, { mode: "fixed", columns, rows });
    }
  }, { signal: view.controller.signal });
  await mountView(view, primary, select("failure").value, !thumbnail);
}

async function mountView(view: TerminalView, primary = false, failure = "", focus = true) {
  if (view.controller.signal.aborted) return;
  if (view.closure && !view.closure.reconnect) return;
  view.connectionController?.abort();
  view.terminal?.dispose();
  view.terminal = undefined;
  const controller = new AbortController();
  view.connectionController = controller;
  view.connectionId = crypto.randomUUID();
  view.phase = "connecting";
  view.closure = undefined;
  view.stats = {};
  view.text = "";
  view.commandMarks = [];
  let previousMarkPhase: TerminalCommandMark["phase"] | null = null;
  const { element, instance, id, transport } = view;
  const current = () => !controller.signal.aborted && !view.controller.signal.aborted;
  element.dataset.phase = "connecting";
  element.dataset.connected = "false";
  elementAt(element, ".closed-overlay", HTMLElement).hidden = true;
  elementAt(element, ".terminal-mount", HTMLElement).inert = false;
  elementAt(element, ".command-marks-rail", HTMLElement).replaceChildren();
  elementAt(element, ".view-role", HTMLElement).textContent = "Joining";
  elementAt(element, ".view-role", HTMLElement).title = "";
  updateViewControls(view);
  const header = elementAt(element, ".view-title", HTMLElement);
  const fallbackTitle = `${instance.name} / ${id} / ${transport === "hmp1" ? "HMP1 relay" : "Direct HWT1"}`;
  header.textContent = fallbackTitle;
  const url = new URL("/ws", location.href);
  url.search = new URLSearchParams({
    instance: instance.id, name: `Web view ${id}`, transport, view: view.connectionId, failure
  }).toString();
  try {
    const renderer = select("renderer").value;
    if (renderer !== "auto" && renderer !== "webgpu" && renderer !== "webgl2") throw new Error("Invalid renderer selection");
    const terminal = await WebTerminal.mount(elementAt(element, ".terminal-mount", HTMLElement), {
      url, signal: controller.signal,
      renderer,
      scale: select("scale").value === "auto" ? "auto" : Number(select("scale").value),
      font: select("font").value === "monospace" ? { family: "monospace" } : undefined,
      label: `${instance.name}, view ${id}, terminal input`,
      onClose(details) {
        if (current()) connectionClosed(view, details);
      },
      onTitleChange(title) {
        header.textContent = title || fallbackTitle;
      },
      onProgressChange(progress) {
        const bar = elementAt(element, ".activity-progress", HTMLProgressElement);
        bar.hidden = progress.state === "none";
        if (progress.percentage === null) bar.removeAttribute("value");
        else bar.value = progress.percentage;
        element.dataset.progress = progress.state;
        elementAt(element, ".progress-status", HTMLElement).textContent = progress.state === "none" ? ""
          : `${progress.state}${progress.percentage === null ? "" : ` ${progress.percentage}%`}`;
      },
      onShellIntegrationChange(shell) {
        const labels = {
          unknown: "Shell activity unknown", prompt: "Prompt", commandLine: "Command input",
          executing: "Command running", finished: "Command finished"
        };
        element.dataset.shellPhase = shell.phase;
        elementAt(element, ".shell-status", HTMLElement).textContent = labels[shell.phase] +
          (shell.lastExitCode === null ? "" : ` / last exit ${shell.lastExitCode}`);
      },
      onWorkingDirectoryChange(workingDirectory) {
        elementAt(element, ".cwd-status", HTMLElement).textContent =
          workingDirectory.path === null ? "" : `cwd: ${workingDirectory.path}`;
      },
      onCommandMarkChange(mark) {
        const status = elementAt(element, ".command-mark-status", HTMLElement);
        if (mark === null) {
          status.textContent = "";
          previousMarkPhase = null;
          return;
        }
        const cmdlineUrl = getCmdlineUrl(mark);
        status.textContent = `mark: ${mark.phase}` +
          (mark.exitCode === null ? "" : ` (exit ${mark.exitCode})`) +
          (cmdlineUrl === null ? "" : ` / ${cmdlineUrl}`);
        // A rising edge into "commandLine" starts a new logical command; every other phase update
        // (executing, finished) refines the same trailing rail entry in place.
        if (mark.phase === "commandLine" && previousMarkPhase !== "commandLine") {
          const viewport = view.viewport;
          const anchor = viewport?.available ? { generation: viewport.generation, top: viewport.top } : null;
          view.commandMarks.push({ phase: mark.phase, exitCode: mark.exitCode, cmdlineUrl, anchor });
          if (view.commandMarks.length > 20) view.commandMarks.shift();
        } else if (view.commandMarks.length > 0) {
          const entry = view.commandMarks[view.commandMarks.length - 1];
          entry.phase = mark.phase;
          entry.exitCode = mark.exitCode;
          if (cmdlineUrl !== null) entry.cmdlineUrl = cmdlineUrl;
        }
        previousMarkPhase = mark.phase;
        renderCommandMarksRail(view);
      },
      onStatus(message, level) {
        if (!current() || view.closure?.close) return;
        const status = elementAt(element, ".view-status", HTMLElement);
        status.textContent = message;
        status.title = message;
        status.dataset.level = level;
        if (level === "error" && !view.terminal?.connected) {
          showClosure(view, {
            title: view.phase === "connecting" ? "Unable to open terminal" : "Terminal view failed",
            summary: "This view stopped locally. No producer completion or WebSocket close status is implied.",
            detail: message, reconnect: true
          });
        }
        updateSizingControls(view);
      },
      onGeometry(geometry) {
        elementAt(element, ".view-grid", HTMLElement).textContent = `${geometry.columns}x${geometry.rows}`;
      },
      onRoleChange(peer) {
        if (!current() || view.phase === "closed") return;
        element.dataset.primary = String(peer.isPrimary);
        element.dataset.peer = peer.id ?? "";
        element.dataset.primaryPeer = peer.primaryId ?? "";
        elementAt(element, ".view-role", HTMLElement).textContent = peer.isPrimary ? "Primary" : peer.id === null ? "Joining" : "Secondary";
        elementAt(element, ".view-role", HTMLElement).title = `Peer: ${peer.id ?? "local"}; primary: ${peer.primaryId ?? "unassigned"}`;
        elementAt(element, ".take-primary", HTMLButtonElement).disabled = peer.isPrimary || peer.id === null || !view.terminal?.connected;
        updateSizingControls(view);
      },
      onSizingChange() { updateSizingControls(view); },
      onViewportChange(viewport) {
        view.viewport = viewport;
        element.dataset.following = String(viewport.following);
      },
      onSelectionChange(selection) {
        view.selection = selection;
        element.dataset.selection = selection.status;
      },
      onStats(stats, text) {
        if (!current()) return;
        view.stats = stats;
        if (view.phase !== "closed") element.dataset.connected = String(stats.connected ?? false);
        if (text !== undefined) view.text = text;
        metrics(view);
      }
    });
    if (!current()) {
      terminal.dispose();
      return;
    }
    view.terminal = terminal;
    if (view.closure) {
      terminal.dispose();
      return;
    }
    view.phase = "connected";
    element.dataset.phase = "connected";
    updateViewControls(view);
    if (primary) view.terminal.requestPrimary();
    if (selected === view && (focus || element.contains(document.activeElement))) view.terminal.focus();
    report("Use -/+ in Auto mode to change text size, or choose a fixed grid. Secondary views follow the primary.");
    await refreshInstances(instance.id).catch(error => report(message(error), "error"));
  } catch (error) {
    if (!current()) return;
    const closure = view.closure ?? showClosure(view, {
      title: "Unable to open terminal",
      summary: "Initialization failed before this view was ready. You can retry without stopping the producer.",
      detail: message(error), reconnect: true
    });
    report(`${closure.title}: ${closure.detail}`, "error");
  }
}

async function createInstance() {
  const instance = readInstance(await api("/api/terminals", "POST", { scene: select("scene").value, columns: 100, rows: 30 }));
  await refreshInstances(instance.id);
  await openView(instance, { primary: true });
}

action(button("create"), createInstance);
action(button("attach"), () => {
  const instance = instances.find(item => item.id === instancesSelect.value);
  if (!instance) throw new Error("Choose an existing terminal instance");
  return openView(instance);
});
action(button("terminate"), async () => {
  const instance = instances.find(item => item.id === instancesSelect.value);
  if (!instance || !window.confirm(`End ${instance.name}? This stops its workload and disconnects every attached view.`)) return;
  await api(`/api/terminals/${encodeURIComponent(instance.id)}`, "DELETE");
  await refreshInstances();
});
action(button("pause"), async () => {
  const instance = instances.find(item => item.id === instancesSelect.value);
  if (!instance) return;
  await api(`/api/terminals/${encodeURIComponent(instance.id)}/controls`, "POST", { paused: !instance.paused });
  await refreshInstances();
});
action(button("apply-rate"), async () => {
  if (!input("rate").reportValidity() || !input("batch").reportValidity()) return;
  const instance = instances.find(item => item.id === instancesSelect.value);
  if (!instance || instance.scene === "shell") return;
  await api(`/api/terminals/${encodeURIComponent(instance.id)}/controls`, "POST", {
    rate: Number(input("rate").value), batch: Number(input("batch").value)
  });
  await refreshInstances();
});
instancesSelect.addEventListener("change", updateInstanceControls);
select("tapes").addEventListener("change", () => {
  tapeSelections.set(instancesSelect.value, select("tapes").value);
  updateTapeControls();
});
button("play-tape").addEventListener("click", () => { void controlTape(false); });
button("stop-tape").addEventListener("click", () => { void controlTape(true); });
const refreshTimer = setInterval(() => {
  if (!refreshing) refreshInstances().catch(error => report(message(error), "error"));
}, 2000);
window.addEventListener("pagehide", () => {
  shuttingDown = true;
  clearInterval(refreshTimer);
  for (const view of views.values()) {
    view.controller.abort();
    view.connectionController?.abort();
    view.terminal?.dispose();
  }
});

try {
  const parameters = new URLSearchParams(location.search);
  const transport = parameters.get("transport");
  if (transport !== null) {
    if (transport !== "direct" && transport !== "hmp1") throw new Error("Invalid transport query parameter");
    select("transport").value = transport;
  }
  const renderer = parameters.get("renderer");
  if (renderer !== null) {
    if (!["auto", "webgpu", "webgl2"].includes(renderer)) throw new Error("Invalid renderer query parameter");
    select("renderer").value = renderer;
  }
  const scene = parameters.get("scene");
  const requestedScene = scene !== null && [...select("scene").options].some(option => option.value === scene);
  if (requestedScene) select("scene").value = scene;
  const scale = parameters.get("scale");
  if (scale !== null && [...select("scale").options].some(option => option.value === scale)) select("scale").value = scale;
  await refreshInstances();
  if (parameters.get("empty") !== "1") {
    if (instances.length && !requestedScene) await openView(instances[0]);
    else await createInstance();
  }
} catch (error) {
  report(message(error), "error");
}
