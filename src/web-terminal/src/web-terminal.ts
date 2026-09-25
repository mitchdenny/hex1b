import { captureMouse } from "./mouse-input.js";
import { defaultWorkerUrl } from "./worker-url.js";
import { selectTransport, workerWebSocketUrl } from "./websocket-transport.js";
import { TransportSession } from "./transport-session.js";
import type { TerminalTransport } from "./transport-types.js";
import { randomId } from "./random-id.js";
import { normalizeFont } from "./terminal-font.js";
import { normalizeRenderer } from "./renderer-options.js";
import { defaultDarkPalette, defaultLightPalette, normalizeColorMode, normalizePalette } from "./terminal-palette.js";
import type { TerminalColorMode, TerminalPalette } from "./terminal-palette.js";
import { paletteScrollbarColors } from "./scrollbar-colors.js";
import { dimensions, normalizeSizing, requestedGrid } from "./terminal-sizing.js";
import { HistoryState } from "./history-state.js";
import { MarkerState } from "./marker-state.js";
import { normalizePadding, contentSpace, terminalLayout } from "./terminal-layout.js";
import { normalizeScrollbar, ScrollbarController } from "./scrollbar.js";
import { ScrollbarTooltip } from "./scrollbar-tooltip.js";
import type { TerminalInsets, TerminalLayout, TerminalMarker, TerminalMarkerOptions, TerminalPadding,
  TerminalScrollbar, TerminalScrollbarConfiguration } from "./scrollbar-types.js";
import { terminalThemeCss } from "./terminal-theme.js";
import { InputPolicy, InputRoute, TerminalAction, inputModifiers } from "./input-policy.js";
import { assertCommandSize } from "./protocol.js";
import { SelectionUI } from "./selection-ui.js";
import { Hyperlinks } from "./hyperlinks.js";
import { LinkDetection } from "./link-detection.js";
import { normalizeLinks } from "./link-options.js";
import type { DetectedLink, LinkDetectionSnapshot } from "./link-detection.js";
import type { MouseCapture, PointerHyperlink } from "./mouse-input.js";
import type { CopySelectionOptions, InputActionHandler, InputDecision, InputBinding,
  TerminalActionName, TerminalGeometry, TerminalInput, TerminalInputContext, TerminalPeer,
  TerminalRendererPreference, TerminalSelection, TerminalSizing, TerminalSizingState, TerminalStats, TerminalViewport,
  TerminalProgress, TerminalShellIntegration, TerminalWorkingDirectory, TerminalCommandMark,
  TerminalLinkOptions, TerminalLinkActivation, TerminalPoint,
  WebTerminalHandle, WebTerminalOptions } from "./types.js";
import type { HyperlinkRange, InputCommand, TerminalCommand, WorkerInputMessage, WorkerOutputMessage } from "./wire-types.js";
import { errorMessage, isRecord } from "./validation.js";
export { InputRoute, TerminalAction, defaultInputBindings } from "./input-policy.js";

function requiredElement<T extends Element>(root: ParentNode, selector: string, type: new () => T): T {
  const element = root.querySelector(selector);
  if (!(element instanceof type)) throw new Error(`Missing terminal element: ${selector}`);
  return element;
}

/**
 * First-party HWT1 client. Owns only the element it appends, not the caller's
 * container or the server terminal. The HWT1 wire and this spike API evolve together.
 */
export class WebTerminal implements WebTerminalHandle {
  readonly element: HTMLDivElement;
  #options: WebTerminalOptions;
  #transport: TerminalTransport;
  #transportSession: TransportSession | undefined;
  #transportFrame: ReturnType<typeof Promise.withResolvers<void>> | undefined;
  #renderer: TerminalRendererPreference;
  #colorMode: TerminalColorMode;
  #palettes: { light: TerminalPalette; dark: TerminalPalette };
  #colorScheme: MediaQueryList | undefined;
  #systemColorChanged = () => {
    if (!this.#disposed && this.#colorMode === "system") this.#applyPalette();
  };
  // DOM and worker fields are initialized by mount before a handle is returned.
  #worker!: Worker;
  #surface!: HTMLDivElement;
  #canvas!: HTMLCanvasElement;
  #input!: HTMLTextAreaElement;
  #mouse: MouseCapture | undefined;
  #observer: ResizeObserver | undefined;
  #listeners = new AbortController();
  #size = { width: 0, height: 0 };
  #sizing = normalizeSizing();
  #geometry: TerminalGeometry = { columns: 80, rows: 24, cellWidth: 10, cellHeight: 20, mouseTracking: 0 };
  #peer: TerminalPeer = { id: null, primaryId: null, isPrimary: false };
  #connected = false;
  #closed = false;
  #readOnly: boolean;
  #disposed = false;
  #hasGeometry = false;
  #resizeTimer: ReturnType<typeof setTimeout> | undefined;
  #lastRequested: string | undefined;
  #compositionTimer: ReturnType<typeof setTimeout> | undefined;
  #resetComposition: (() => void) | undefined;
  #ready = Promise.withResolvers<WebTerminal>();
  #readyTimer: ReturnType<typeof setTimeout> | undefined;
  #stats: TerminalStats = {};
  #screenText = "";
  #title = "";
  #hasTitle = false;
  #progress: TerminalProgress = { state: "none", percentage: null };
  #shellIntegration: TerminalShellIntegration = { phase: "unknown", lastExitCode: null };
  #workingDirectory: TerminalWorkingDirectory = { uri: null, host: null, path: null };
  #commandMark: TerminalCommandMark | null = null;
  #hasActivity = false;
  #history: HistoryState;
  #markerState: MarkerState;
  #padding: TerminalInsets;
  #scrollbar: false | TerminalScrollbarConfiguration;
  #layout: TerminalLayout;
  #scrollbarController: ScrollbarController | undefined;
  #scrollbarTooltip: ScrollbarTooltip | undefined;
  #layoutNotification = false;
  #markerNotification = false;
  #highlights!: HTMLDivElement;
  #inspection!: HTMLDivElement;
  #inspectionError = "";
  #copySerial = 0;
  #copying = false;
  #policy: InputPolicy;
  #actions: Map<string, InputActionHandler>;
  #clipboardAction = false;
  #inputSerial = 0;
  #selectionUI: SelectionUI | undefined;
  #selectionOverlay!: HTMLDivElement;
  #selectionUIError = "";
  #canvasSize = { width: 0, height: 0 };
  #hyperlinks = new Hyperlinks();
  #links: false | TerminalLinkOptions;
  #linkDetector: LinkDetection;
  #linkGeneration = 1;
  #linkRevision = 0;
  #linkSerial = 0;
  #linkSnapshot: LinkDetectionSnapshot | undefined;
  #osc8Rows = new Map<number, HyperlinkRange[]>();
  #detectedLinks: readonly DetectedLink[] = [];
  #presentedLinks: readonly DetectedLink[] = [];
  #detectedRows = new Map<number, { startColumn: number; endColumn: number; link: DetectedLink }[]>();
  #hoveredLinkId: string | undefined;
  #pendingLinks: { revision: number; generation: number; serial: number; links: readonly DetectedLink[] } | undefined;

  /** Resolves after a connected terminal frame is presented. Supply signal to cancel mounting. */
  static async mount(container: HTMLElement, options: WebTerminalOptions): Promise<WebTerminal> {
    if (!(container instanceof HTMLElement)) throw new TypeError("A terminal container HTMLElement is required");
    const transport = selectTransport(options);
    if (options.signal?.aborted) throw options.signal.reason;
    if (normalizeRenderer(options.renderer) === "webgpu" && (!window.isSecureContext || !navigator.gpu)) {
      throw new Error("The requested WebGPU renderer requires WebGPU over HTTPS or localhost");
    }
    if (!window.Worker || !window.ResizeObserver || !window.OffscreenCanvas ||
        !HTMLCanvasElement.prototype.transferControlToOffscreen) {
      throw new Error("WebTerminal requires module workers, ResizeObserver, and a transferable OffscreenCanvas");
    }
    const terminal = new WebTerminal(options, transport);
    try {
      await Promise.all([terminal.#ready.promise, Promise.resolve().then(() => terminal.#start(container))]);
      return terminal;
    } catch (error) {
      terminal.dispose();
      throw error;
    }
  }

  private constructor(options: WebTerminalOptions, transport = selectTransport(options)) {
    this.#options = options;
    this.#transport = transport;
    if (options.readOnly !== undefined && typeof options.readOnly !== "boolean")
      throw new TypeError("readOnly must be a boolean");
    this.#readOnly = options.readOnly ?? false;
    this.#renderer = normalizeRenderer(options.renderer);
    this.#colorMode = normalizeColorMode(options.colorMode);
    this.#palettes = {
      light: normalizePalette(options.lightModePalette === undefined ? defaultLightPalette : options.lightModePalette),
      dark: normalizePalette(options.darkModePalette === undefined ? defaultDarkPalette : options.darkModePalette),
    };
    this.#padding = normalizePadding(options.padding);
    this.#scrollbar = normalizeScrollbar(options.scrollbar);
    this.#layout = terminalLayout(this.#size, this.#geometry, false, this.#sizing, this.#padding, this.#scrollbar);
    if (options.workerUrl !== undefined && !(options.workerUrl instanceof URL) &&
        (typeof options.workerUrl !== "string" || !options.workerUrl.trim()))
      throw new TypeError("workerUrl must be a nonempty URL string or URL");
    if (options.linkDetectionWorkerUrl !== undefined && !(options.linkDetectionWorkerUrl instanceof URL) &&
        (typeof options.linkDetectionWorkerUrl !== "string" || !options.linkDetectionWorkerUrl.trim()))
      throw new TypeError("linkDetectionWorkerUrl must be a nonempty URL string or URL");
    if (options.onLinkDetectionError !== undefined && typeof options.onLinkDetectionError !== "function")
      throw new TypeError("onLinkDetectionError must be a function");
    if (options.onSelectionUI !== undefined && (typeof options.onSelectionUI !== "function" ||
        options.onSelectionUI.constructor.name === "AsyncFunction"))
      throw new TypeError("onSelectionUI must be a synchronous event handler");
    this.#policy = new InputPolicy(options);
    this.#actions = new Map(Object.entries(options.actions ?? {}));
    this.#links = normalizeLinks(options.links, new Set(this.#actions.keys()));
    this.#linkDetector = new LinkDetection({
      workerUrl: options.linkDetectionWorkerUrl === undefined ? undefined : new URL(options.linkDetectionWorkerUrl, location.href),
      actions: new Set(this.#actions.keys()),
      onChange: (revision, links) => {
        if (this.#disposed || !this.#connected || revision !== this.#linkRevision) return;
        this.#detectedLinks = links;
        this.#replacePresentedLinks([]);
        this.#requestLinkDecorations();
      },
      onError: error => {
        if (this.#disposed) return;
        try { this.#options.onStatus?.(`Link detection: ${error.message}`, "error"); }
        finally { this.#options.onLinkDetectionError?.(error); }
      },
    });
    this.#linkDetector.configure(this.#links ? this.#links.detection : false);
    this.#history = new HistoryState(command => this.#send(command), () => this.#inspectionChanged());
    this.#markerState = new MarkerState(command => this.#send(command), () => this.#markersChanged());
    this.element = document.createElement("div");
    this.element.className = "hex1b-terminal";
    this.element.tabIndex = -1;
    this.element.style.cssText = "width:100%;height:100%;min-width:0;min-height:0;contain:strict";
  }

  get geometry(): TerminalGeometry { return { ...this.#geometry }; }
  get peer(): TerminalPeer { return { ...this.#peer }; }
  get connected() { return this.#connected; }
  get readOnly(): boolean { return this.#readOnly; }
  /** Current presented workload title; retained on disconnect/dispose. Treat as untrusted text. */
  get title(): string { return this.#title; }
  get progress(): TerminalProgress { return { ...this.#progress }; }
  get shellIntegration(): TerminalShellIntegration { return { ...this.#shellIntegration }; }
  get workingDirectory(): TerminalWorkingDirectory { return { ...this.#workingDirectory }; }
  get commandMark(): TerminalCommandMark | null { return this.#commandMark ? { ...this.#commandMark } : null; }
  get stats(): TerminalStats { return { ...this.#stats }; }
  get screenText() { return this.#screenText; }
  get sizing(): TerminalSizingState { return { ...this.#sizing }; }
  get layout(): TerminalLayout { return this.#layout; }
  get padding(): TerminalInsets { return this.#padding; }
  get scrollbar(): false | TerminalScrollbarConfiguration { return this.#scrollbar; }
  get markers(): readonly TerminalMarker[] { return this.#markerState.markers; }
  get inputBindings(): InputBinding[] { return this.#policy.bindings; }
  get viewport(): TerminalViewport {
    const viewport = this.#history.viewport;
    return { ...viewport, followTail: viewport.following,
      offset: viewport.available ? viewport.liveTop - viewport.top : 0 };
  }
  get selection(): TerminalSelection {
    const selection = this.#history.selection;
    return { ...selection, active: selection.status === "valid", pending: selection.status === "pending",
      copying: this.#copying, copyError: this.#inspectionError };
  }

  #start(container: HTMLElement): void {
    if (this.#options.signal?.aborted) throw this.#options.signal.reason;
    const url = workerWebSocketUrl(this.#transport);
    const scale = this.#options.scale === undefined || this.#options.scale === "auto"
      ? Math.min(3, Math.max(0.5, window.devicePixelRatio || 1)) : this.#options.scale;
    if (!Number.isFinite(scale) || scale < 0.5 || scale > 3) throw new RangeError("Backing scale must be 0.5-3 or 'auto'");
    const font = normalizeFont(this.#options.font, location.href);
    this.#sizing = normalizeSizing(this.#options.sizing);
    const shadow = this.element.attachShadow({ mode: "open" });
    shadow.innerHTML = `
      <style>
        ${terminalThemeCss}
        :host { display: block; }
        .viewport { position: relative; width: 100%; height: 100%; display: flex; align-items: center; justify-content: center; overflow: hidden; }
        .surface { position: absolute; flex: none; overflow: hidden; }
        canvas { display: block; width: 100%; height: 100%; user-select: none; touch-action: none; }
        .scrollbar-canvas { position: absolute; inset: 0; pointer-events: none; }
        .scrollbar-tooltip-slot { position: absolute; inset: 0; display: block; pointer-events: none; }
        .scrollbar-accessibility { position: absolute; pointer-events: none; outline: none; }
        .highlights { position: absolute; inset: 0; pointer-events: none; overflow: hidden; }
        .highlight { position: absolute; }
        .selection-ui-slot { position: absolute; inset: 0; display: block; pointer-events: none; font: 11px/1.4 var(--cp-view-font-family); color: var(--cp-view-text); }
        .inspection { position: absolute; right: 4px; bottom: 4px; left: 4px; display: flex; flex-wrap: wrap; gap: 4px; justify-content: end; align-items: center; pointer-events: none; font: 11px/1.4 var(--cp-view-font-family); }
        .inspection [hidden] { display: none; }
        .inspection button, .inspection-message { font: inherit; background: var(--cp-view-surface); color: var(--cp-view-text); border: 1px solid var(--cp-view-border-strong); border-radius: .625rem; padding: 4px 8px; }
        .inspection button { pointer-events: auto; cursor: pointer; }
        .inspection button:disabled { opacity: .6; cursor: not-allowed; }
        .inspection button:hover { background: var(--cp-view-accent-soft); }
        .inspection button:focus-visible { outline: 2px solid var(--cp-view-accent); outline-offset: 2px; }
        .inspection-message { color: var(--cp-view-text-muted); max-width: 100%; }
        .inspection-message[data-level=error] { color: var(--cp-view-danger); }
        textarea { position: absolute; width: 1px; height: 1px; opacity: 0; left: 0; top: 0; padding: 0; border: 0; resize: none; }
      </style>
      <div class="viewport"><div class="surface">
        <canvas aria-hidden="true"></canvas>
        <div class="highlights" part="selection-highlights" aria-hidden="true"></div>
        <textarea autocomplete="off" autocapitalize="off" spellcheck="false"></textarea>
        <slot name="selection-ui" class="selection-ui-slot"></slot>
      </div>
      <canvas class="scrollbar-canvas" aria-hidden="true"></canvas>
      <div class="scrollbar-accessibility" role="scrollbar" aria-label="Terminal scrollback" aria-orientation="vertical" tabindex="0"></div>
      <slot name="scrollbar-tooltip" class="scrollbar-tooltip-slot"></slot>
      <div class="inspection">
        <span class="inspection-message" role="status" aria-live="polite" hidden></span>
        <button class="copy-selection" part="selection-copy-button" hidden disabled>Copy</button>
        <button class="return-live" hidden>Return to live</button>
      </div></div>`;
    this.#surface = requiredElement(shadow, ".surface", HTMLDivElement);
    this.#canvas = requiredElement(shadow, "canvas", HTMLCanvasElement);
    this.#input = requiredElement(shadow, "textarea", HTMLTextAreaElement);
    this.#highlights = requiredElement(shadow, ".highlights", HTMLDivElement);
    this.#inspection = requiredElement(shadow, ".inspection", HTMLDivElement);
    this.#canvas.id = `hex1b-content-${randomId()}`;
    const scrollbarAccessibility = requiredElement(shadow, ".scrollbar-accessibility", HTMLDivElement);
    scrollbarAccessibility.setAttribute("aria-controls", this.#canvas.id);
    const tooltipOverlay = document.createElement("div");
    tooltipOverlay.slot = "scrollbar-tooltip";
    tooltipOverlay.className = "hex1b-scrollbar-tooltip-overlay";
    tooltipOverlay.style.cssText = "position:relative;width:100%;height:100%;pointer-events:none";
    this.element.append(tooltipOverlay);
    this.#scrollbarTooltip = new ScrollbarTooltip({
      overlay: tooltipOverlay, getDetails: id => this.getCommandMarkDetails(id),
      reportError: error => {
        this.#inspectionError = `Scrollbar tooltip: ${errorMessage(error)}`;
        this.#renderInspectionStatus();
        this.#options.onStatus?.(this.#inspectionError, "error");
      }
    });
    this.#scrollbarController = new ScrollbarController({
      element: requiredElement(shadow, ".viewport", HTMLDivElement),
      canvas: requiredElement(shadow, ".scrollbar-canvas", HTMLCanvasElement),
      accessibility: scrollbarAccessibility,
      getState: () => ({ layout: this.layout, viewport: this.viewport, markers: this.markers,
        connected: this.#connected, configuration: this.#scrollbar }),
      scrollToRow: top => this.scrollToRow(top),
      scrollToLive: () => this.scrollToLive(),
      scrollToMarker: id => this.scrollToMarker(id),
      onMarkerHover: marker => this.#scrollbarTooltip?.update(marker, this.layout,
        this.#scrollbar ? this.#scrollbar.tooltip : false),
      reportError: error => {
        this.#inspectionError = `Scrollbar: ${errorMessage(error)}`;
        this.#renderInspectionStatus();
        this.#options.onStatus?.(this.#inspectionError, "error");
      }
    });
    this.#selectionOverlay = document.createElement("div");
    this.#selectionOverlay.slot = "selection-ui";
    this.#selectionOverlay.className = "hex1b-selection-overlay";
    this.#selectionOverlay.style.cssText = "position:relative;width:100%;height:100%;pointer-events:none";
    this.element.append(this.#selectionOverlay);
    this.#selectionUI = new SelectionUI({
      element: this.element, overlay: this.#selectionOverlay,
      button: requiredElement(this.#inspection, ".copy-selection", HTMLButtonElement), signal: this.#listeners.signal,
      getState: () => ({ selection: this.selection, viewport: this.viewport, geometry: this.geometry,
        canvasSize: this.#canvasSize, connected: this.#connected, readOnly: this.#readOnly }),
      runAction: this.runAction.bind(this),
      onSelectionUI: this.#options.onSelectionUI,
      reportError: error => {
        this.#selectionUIError = error ? `Selection UI failed: ${errorMessage(error)}` : "";
        this.#selectionOverlay.hidden = !!error;
        this.#renderInspectionStatus();
        if (error) this.#options.onStatus?.(this.#selectionUIError, "error");
      }
    });
    this.#selectionUI.refresh();
    this.#input.setAttribute("aria-label", this.#options.label || "Terminal input. Click outside to use page controls.");
    this.#input.disabled = true;
    container.append(this.element);
    const inspect = (operation: () => void) => {
      try { this.#inspectionError = ""; operation(); }
      catch (error) { this.#inspectionError = errorMessage(error); this.#inspectionChanged(); }
    };
    this.#mouse = captureMouse(this.#canvas, command => this.#inputCommand(command), () => this.focus(), {
      state: () => ({ historical: !this.viewport.following || this.viewport.pending, readOnly: this.#readOnly,
        selection: this.selection }),
      begin: (point, selection) => inspect(() => this.#history.begin(point, selection)),
      extend: point => inspect(() => this.#history.extend(point)),
      scroll: (delta, endpoint) => inspect(() => {
        this.#history.scroll(delta, endpoint);
        this.#scrollbarController?.activity();
      }),
      end: cancelled => this.#history.endGesture(cancelled),
      resolve: input => this.#resolveInput(input),
      execute: (decision, input) => this.#executeInputAction(decision, input),
      hyperlink: point => this.#linkAt(point),
      hoverHyperlink: link => {
        this.#hoveredLinkId = link?.id;
        if (this.#links && this.#links.detection && this.#links.detection.decoration === "hover")
          this.#requestLinkDecorations();
      },
      openHyperlink: (link, input) => this.#activateLink(link, input),
    });
    this.#bindKeyboard();
    requiredElement(this.#inspection, ".return-live", HTMLButtonElement).addEventListener("click", () => {
      this.runAction(TerminalAction.ScrollToLive).catch(error => this.#actionFailed(error));
    }, { signal: this.#listeners.signal });
    requiredElement(this.#inspection, ".copy-selection", HTMLButtonElement).addEventListener("click", () => {
      this.runAction(TerminalAction.CopySelection).catch(error => this.#actionFailed(error));
    }, { signal: this.#listeners.signal });
    requiredElement(shadow, ".viewport", HTMLDivElement).addEventListener("pointerdown", event => {
      if (event.composedPath().includes(this.#selectionOverlay)) return;
      if ((event.target instanceof Element && event.target.closest("button")) ||
          event.target === this.#canvas || event.target === this.#input) return;
      event.preventDefault();
      this.focus();
    }, { signal: this.#listeners.signal });
    this.#observer = new ResizeObserver(entries => {
      const { width, height } = entries[0].contentRect;
      const changed = width !== this.#size.width || height !== this.#size.height;
      this.#size = { width, height };
      this.#fit();
      if (changed) this.#queueResize();
    });
    // Observe the caller's outer box, never the fitted inner surface.
    this.#observer.observe(container);
    window.addEventListener("pagehide", () => this.dispose(), { signal: this.#listeners.signal });
    this.#options.signal?.addEventListener("abort", () => this.dispose(), { once: true, signal: this.#listeners.signal });
    this.#readyTimer = setTimeout(() => this.#fail(new Error("Timed out waiting for the terminal's first frame")), 30000);
    this.#worker = this.#options.workerUrl === undefined
      ? new Worker(defaultWorkerUrl("terminal"), { type: "module", name: "Hex1b WebTerminal" })
      : new Worker(new URL(this.#options.workerUrl, location.href), { type: "module", name: "Hex1b WebTerminal" });
    this.#worker.addEventListener("message", (event: MessageEvent<WorkerOutputMessage>) => this.#message(event.data));
    this.#worker.addEventListener("error", event => {
      event.preventDefault();
      this.#fail(new Error(event.message || "Terminal worker failed"));
    });
    this.#worker.addEventListener("messageerror", () => this.#fail(new Error("Terminal worker message could not be decoded")));
    const canvas = this.#canvas.transferControlToOffscreen();
    this.#colorScheme = window.matchMedia?.("(prefers-color-scheme: dark)");
    this.#colorScheme?.addEventListener("change", this.#systemColorChanged);
    this.#post({ type: "init", canvas, transport: url === undefined ? { type: "custom" } : { type: "websocket", url }, scale, font,
      renderer: this.#renderer, palette: this.#palettes[this.resolvedColorMode] }, [canvas]);
    this.#applyPalette();
    this.#postLinkConfiguration();
  }

  #message(message: WorkerOutputMessage): void {
    if (this.#disposed) return;
    if (message.type === "transportConnect") {
      if (this.#transportSession) { this.#fail(new Error("Transport is already initialized")); return; }
      this.#transportSession = new TransportSession({
        onReady: () => this.#post({ type: "transportConnected" }),
        onFrame: buffer => {
          this.#transportFrame = Promise.withResolvers<void>();
          this.#post({ type: "transportFrame", buffer }, [buffer]);
          return this.#transportFrame.promise;
        },
        onClose: details => this.#post({ type: "transportClosed", details }),
        onError: error => this.#post({ type: "transportError", message: error.message })
      });
      this.#transportSession.start(this.#transport);
    } else if (message.type === "transportSend") {
      // An ACK can be emitted while accepting an inventory fragment. Release the
      // inbound bridge slot before the adapter can synchronously deliver its reply.
      void Promise.resolve(this.#transportFrame?.promise)
        .then(() => this.#transportSession?.send(message.control))
        .then(() => this.#post({ type: "transportSent" }))
        .catch(error => {
          if (!this.#disposed) this.#post({ type: "transportError", message: errorMessage(error) });
        });
    } else if (message.type === "transportReceived") {
      this.#transportFrame?.resolve();
      this.#transportFrame = undefined;
    } else if (message.type === "connected") {
      this.#connected = true;
      this.#input.disabled = !this.#canInput();
    } else if (message.type === "closed") {
      if (this.#closed) return;
      this.#closed = true;
      clearTimeout(this.#readyTimer);
      try {
        this.#disconnect();
        if (!this.#disposed) {
          const details = Object.freeze({ ...message.details });
          if (this.#options.transport !== undefined) this.#options.onClose?.(details);
          else if (details.code !== undefined) this.#options.onClose?.(details);
        }
      } finally {
        this.#ready.reject(new Error(`Terminal ${message.details.code === undefined ? "transport" : "WebSocket"} closed (${
          message.details.code === undefined ? message.details.reason :
            `${message.details.code}${message.details.reason ? `: ${message.details.reason}` : ""}`}) before mounting completed`));
      }
    } else if (message.type === "status") {
      if (message.level === "error") {
        this.#stopTransport();
        this.#disconnect();
        this.#ready.reject(new Error(message.message));
      }
      this.#options.onStatus?.(message.message, message.level);
    } else if (message.type === "geometry") {
      this.#linkRevision = message.revision;
      this.#osc8Rows.clear();
      for (const range of message.hyperlinks) {
        const row = this.#osc8Rows.get(range.row) ?? [];
        row.push(range);
        this.#osc8Rows.set(range.row, row);
      }
      this.#replacePresentedLinks([]);
      this.#pendingLinks = undefined;
      const first = !this.#hasGeometry;
      const geometryChanged = first || (["columns", "rows", "cellWidth", "cellHeight", "mouseTracking"] as const)
        .some(field => this.#geometry[field] !== message[field]);
      this.#geometry = {
        columns: message.columns, rows: message.rows,
        cellWidth: message.cellWidth, cellHeight: message.cellHeight, mouseTracking: message.mouseTracking
      };
      this.#hasGeometry = true;
      const oldPeer = this.#peer;
      this.#peer = message.peer;
      this.#input.disabled = !this.#canInput();
      if (this.#canInput() && document.activeElement === this.element && !this.element.shadowRoot?.activeElement) this.focus();
      this.#hyperlinks.update(message.hyperlinks);
      if (geometryChanged || oldPeer.isPrimary !== this.#peer.isPrimary) this.#fit();
      if (!this.#peer.isPrimary) {
        clearTimeout(this.#resizeTimer);
        this.#resizeTimer = undefined;
        this.#lastRequested = undefined;
      }
      if (first || (!oldPeer.isPrimary && this.#peer.isPrimary)) this.#queueResize(true);
      if (this.#lastRequested === `${message.columns}x${message.rows}`) this.#lastRequested = undefined;
      if (Object.hasOwn(message, "history")) {
        this.#screenText = message.text;
        this.#markerState.accept(message.history, message.revision);
        this.#history.accept(message.history, message.revision);
      }
      if (message.linkGeneration === this.#linkGeneration) {
        if (message.linkSnapshot) this.#acceptLinkSnapshot(message.linkSnapshot);
        else {
          if (this.#linkSnapshot) this.#linkSnapshot = { ...this.#linkSnapshot, revision: message.revision };
          this.#linkDetector.advance(message.revision);
        }
      }
      this.#mouse?.update(message.columns, message.rows, message.mouseTracking);
      if (geometryChanged) this.#options.onGeometry?.(this.geometry);
      if (first) this.#options.onSizingChange?.(this.sizing);
      if (oldPeer.id !== this.#peer.id || oldPeer.primaryId !== this.#peer.primaryId || oldPeer.isPrimary !== this.#peer.isPrimary) {
        this.#options.onRoleChange?.(this.peer);
      }
      if (!this.#disposed && this.#connected && (this.#peer.id !== null || this.#peer.isPrimary)) {
        const titleChanged = !this.#hasTitle || this.#title !== message.title;
        const progressChanged = !this.#hasActivity || this.#progress.state !== message.progress.state ||
          this.#progress.percentage !== message.progress.percentage;
        const shellChanged = !this.#hasActivity || this.#shellIntegration.phase !== message.shellIntegration.phase ||
          this.#shellIntegration.lastExitCode !== message.shellIntegration.lastExitCode;
        const workingDirectoryChanged = !this.#hasActivity ||
          this.#workingDirectory.uri !== message.workingDirectory.uri;
        const commandMarkChanged = !this.#hasActivity || this.#commandMark?.phase !== message.commandMark?.phase ||
          this.#commandMark?.exitCode !== message.commandMark?.exitCode ||
          this.#commandMark?.rawParameters !== message.commandMark?.rawParameters;
        this.#title = message.title;
        this.#hasTitle = true;
        this.#progress = { ...message.progress };
        this.#shellIntegration = { ...message.shellIntegration };
        this.#workingDirectory = { ...message.workingDirectory };
        this.#commandMark = message.commandMark ? { ...message.commandMark } : null;
        this.#hasActivity = true;
        if (titleChanged) this.#options.onTitleChange?.(this.#title);
        if (!this.#disposed && progressChanged) this.#options.onProgressChange?.(this.progress);
        if (!this.#disposed && shellChanged) this.#options.onShellIntegrationChange?.(this.shellIntegration);
        if (!this.#disposed && workingDirectoryChanged) this.#options.onWorkingDirectoryChange?.(this.workingDirectory);
        if (!this.#disposed && commandMarkChanged) this.#options.onCommandMarkChange?.(this.commandMark);
      }
    } else if (message.type === "linkSnapshot") {
      if (message.generation === this.#linkGeneration && message.snapshot.revision === this.#linkRevision)
        this.#acceptLinkSnapshot(message.snapshot);
    } else if (message.type === "linkDecorations") {
      const pending = this.#pendingLinks;
      if (this.#connected && pending && pending.serial === message.serial &&
          pending.generation === message.generation && message.generation === this.#linkGeneration &&
          pending.revision === message.revision && message.revision === this.#linkRevision) {
        this.#pendingLinks = undefined;
        this.#replacePresentedLinks(pending.links);
        this.#mouse?.refresh();
      }
    } else if (message.type === "history") {
      this.#screenText = message.text;
      this.#markerState.accept(message.history, message.revision);
      this.#history.accept(message.history, message.revision);
    } else if (message.type === "stats") {
      this.#stats = message.stats;
      if (message.text !== undefined) this.#screenText = message.text;
      if (message.stats.revision > 0 && this.#hasTitle && this.#hasActivity && this.#connected &&
          (this.#peer.id !== null || this.#peer.isPrimary)) {
        clearTimeout(this.#readyTimer);
        this.#ready.resolve(this);
      }
      this.#options.onStats?.(this.stats, message.text);
    }
  }

  #fit() {
    const next = terminalLayout(this.#size, this.#geometry, this.#peer.isPrimary, this.#sizing, this.#padding, this.#scrollbar);
    const changed = JSON.stringify(next) !== JSON.stringify(this.#layout);
    this.#layout = next;
    const { width, height, left, top } = next.content;
    this.#surface.style.width = `${width}px`;
    this.#surface.style.height = `${height}px`;
    this.#surface.style.left = `${left}px`;
    this.#surface.style.top = `${top}px`;
    // Overlay positions use layout pixels, before any ancestor CSS transforms.
    const style = getComputedStyle(this.#surface);
    this.#canvasSize = { width: Number.parseFloat(style.width), height: Number.parseFloat(style.height) };
    this.#selectionUI?.refresh();
    this.#scrollbarController?.refresh();
    if (changed && !this.#layoutNotification) {
      this.#layoutNotification = true;
      queueMicrotask(() => {
        this.#layoutNotification = false;
        if (!this.#disposed) this.#options.onLayoutChange?.(this.layout);
      });
    }
    const dpr = window.devicePixelRatio || 1;
    this.#post({ type: "viewport", width: Math.ceil(width * dpr), height: Math.ceil(height * dpr) });
  }

  #fittedGrid() {
    return requestedGrid(contentSpace(this.#size, this.#padding, this.#scrollbar), this.#geometry, this.#sizing);
  }

  #queueResize(includeFixed = false) {
    if (this.#sizing.mode === "fixed" && !includeFixed) return;
    if (!this.#canInput() || !this.#peer.isPrimary || this.#resizeTimer !== undefined || this.#disposed) return;
    // Throttle (rather than debounce) so dragging a primary view updates peers live.
    this.#resizeTimer = setTimeout(() => {
      this.#resizeTimer = undefined;
      const grid = this.#fittedGrid();
      if (!grid || !this.#peer.isPrimary || !this.#canInput()) return;
      const key = `${grid.columns}x${grid.rows}`;
      if ((grid.columns === this.#geometry.columns && grid.rows === this.#geometry.rows) || key === this.#lastRequested) return;
      this.resize(grid.columns, grid.rows);
    }, 50);
  }

  #post(message: WorkerInputMessage, transfer: Transferable[] = []): void {
    this.#worker?.postMessage(message, transfer);
  }

  #send(command: TerminalCommand): void {
    if (!this.#connected || this.#disposed) throw new Error("Terminal view is not connected");
    if (this.#readOnly && ["input", "paste", "key", "mouse", "resize", "requestPrimary"].includes(command.type))
      throw new Error("Terminal view does not accept input");
    this.#post({ type: "command", command });
  }

  #inputCommand(command: InputCommand): void {
    if (this.#disposed || !this.#canInput()) return;
    assertCommandSize(command);
    this.#inputSerial++;
    if (["input", "paste", "key"].includes(command.type) && this.viewport.available) {
      this.#mouse?.cancel();
      if (this.selection.status !== "none") this.clearSelection();
      if (!this.viewport.following || this.viewport.pending) this.scrollToLive();
    }
    this.#send(command);
  }

  #inspectionChanged() {
    const viewport = this.viewport;
    const selection = this.selection;
    this.#mouse?.refresh();
    if (selection.status === "invalidated") this.#mouse?.cancel();
    if (this.#highlights) {
      this.#highlights.replaceChildren(...selection.ranges.map(range => {
        const element = document.createElement("span");
        element.className = "highlight";
        element.setAttribute("part", "selection-highlight");
        element.style.cssText = `left:${range.startColumn / this.#geometry.columns * 100}%;top:${range.row / this.#geometry.rows * 100}%;width:${(range.endColumn - range.startColumn) / this.#geometry.columns * 100}%;height:${100 / this.#geometry.rows}%`;
        return element;
      }));
      const live = requiredElement(this.#inspection, ".return-live", HTMLButtonElement);
      live.hidden = !viewport.available || (viewport.following && !viewport.pending);
      live.disabled = !this.#connected;
      this.#renderInspectionStatus();
    }
    if (!this.#disposed) this.#selectionUI?.refresh();
    this.#scrollbarController?.refresh();
    this.#options.onViewportChange?.(viewport);
    this.#options.onSelectionChange?.(selection);
  }

  scrollLines(delta: number): void { this.#history.scroll(delta); this.#scrollbarController?.activity(); }
  scrollToRow(top: number): void { this.#history.scrollTo(top); this.#scrollbarController?.activity(); }
  scrollToLive() { this.#history.live(); this.#scrollbarController?.activity(); }
  scrollToMarker(id: string): Promise<void> {
    this.#scrollbarController?.activity();
    return this.#markerState.jump(id);
  }
  addMarker(options: TerminalMarkerOptions): Promise<TerminalMarker> { return this.#markerState.add(options); }
  removeMarker(id: string): Promise<void> { return this.#markerState.remove(id); }
  getCommandMarkDetails(id: string): Promise<TerminalCommandMark> { return this.#markerState.details(id); }

  #markersChanged(): void {
    this.#scrollbarController?.activity();
    if (this.#markerNotification) return;
    this.#markerNotification = true;
    queueMicrotask(() => {
      this.#markerNotification = false;
      if (!this.#disposed) this.#options.onMarkersChange?.(this.markers);
    });
  }

  setPadding(padding: TerminalPadding): void {
    if (this.#disposed) throw new Error("Terminal view is disposed");
    this.#padding = normalizePadding(padding);
    this.#fit();
    this.#queueResize(true);
  }

  setScrollbar(scrollbar: TerminalScrollbar): void {
    if (this.#disposed) throw new Error("Terminal view is disposed");
    this.#scrollbar = normalizeScrollbar(scrollbar);
    this.#scrollbarController?.cancel();
    this.#fit();
    this.#queueResize(true);
  }

  refreshScrollbar(): void {
    if (this.#disposed) throw new Error("Terminal view is disposed");
    this.#scrollbarController?.invalidate();
  }
  clearSelection() { this.#inspectionError = ""; this.#history.clear(); }

  /** Re-notifies selection UI hosts after an external styling/policy change. */
  refreshSelectionUI() {
    if (this.#disposed) throw new Error("Terminal view is disposed");
    this.#selectionUI?.refresh(true);
  }

  #renderInspectionStatus() {
    if (!this.#inspection) return;
    const selection = this.selection;
    const viewport = this.viewport;
    const status = requiredElement(this.#inspection, ".inspection-message", HTMLSpanElement);
    status.textContent = this.#selectionUIError || this.#inspectionError || viewport.navigationError ||
      (selection.status === "unavailable" ? "" : selection.message) ||
      (viewport.available && !viewport.following ? `${viewport.liveTop - viewport.top} rows above live` : "");
    status.hidden = !status.textContent;
    status.dataset.level = this.#selectionUIError || this.#inspectionError || viewport.navigationError ||
      selection.status === "invalidated" ? "error" : "info";
  }

  #actionFailed(error: unknown): void {
    const failure = error instanceof Error ? error : new Error(String(error));
    this.#inspectionError = `Input action failed: ${failure.message}`;
    this.#inspectionChanged();
    this.#options.onInputError?.(failure);
  }

  async copySelection({ clear = false }: CopySelectionOptions = {}): Promise<string> {
    if (typeof clear !== "boolean") throw new TypeError("Copy clear must be a boolean");
    const serial = ++this.#copySerial;
    const selectionId = this.selection.requestId;
    const generation = this.viewport.generation;
    this.#inspectionError = "";
    try {
      if (!this.#connected || this.#disposed) throw new Error("Terminal view is not connected");
      if (!navigator.clipboard?.write || typeof ClipboardItem !== "function") {
        throw new Error("Clipboard writing is unavailable. Use a secure browser context with clipboard permission.");
      }
      const text = this.#history.copy();
      this.#copying = true;
      this.#inspectionChanged();
      // Invoke the clipboard during the user gesture; producer extraction can complete asynchronously.
      const item = new ClipboardItem({ "text/plain": text.then(value => new Blob([value], { type: "text/plain" })) });
      const [value] = await Promise.all([text, navigator.clipboard.write([item])]);
      if (clear && serial === this.#copySerial && this.selection.status === "valid" &&
          this.selection.requestId === selectionId && this.viewport.generation === generation)
        this.clearSelection();
      return value;
    } catch (error) {
      if (serial === this.#copySerial) {
        this.#history.cancelCopy(error);
        this.#inspectionError = `Copy failed: ${errorMessage(error)}`;
      }
      throw error;
    } finally {
      if (serial === this.#copySerial) this.#copying = false;
      this.#inspectionChanged();
    }
  }

  #canInput() {
    return this.#connected && this.#hasGeometry && !this.#readOnly &&
      (this.#peer.id !== null || this.#peer.isPrimary);
  }

  get inputContext(): TerminalInputContext {
    return Object.freeze({
      terminal: this, selection: this.selection, viewport: this.viewport,
      buffer: this.viewport.buffer ?? null, mouseCaptured: !this.#readOnly && this.#geometry.mouseTracking !== 0,
      historical: !this.viewport.following || this.viewport.pending,
      readOnly: this.#readOnly, connected: this.#connected, peer: this.peer
    });
  }

  #resolveInput(input: TerminalInput): InputDecision {
    try {
      const decision = this.#policy.resolve(Object.freeze(input), this.inputContext);
      if (this.#readOnly && decision.route === InputRoute.Application)
        return { route: input.type === "pointer" || input.type === "wheel" ? InputRoute.Continue : InputRoute.Consume };
      return decision;
    }
    catch (error) {
      this.#actionFailed(error);
      return { route: InputRoute.Consume };
    }
  }

  #executeInputAction(decision: Extract<InputDecision, { action: unknown }>, input: TerminalInput): void {
    this.#performAction(decision.action, decision.args, input).catch(error => this.#actionFailed(error));
  }

  /** Named actions are shared by controls and bindings. Custom callbacks receive context, args, and input. */
  runAction(action: "copySelection", args?: CopySelectionOptions, input?: TerminalInput): Promise<string>;
  runAction(action: "pasteClipboard", args?: undefined, input?: TerminalInput): Promise<string>;
  runAction(action: "copyOrPaste", args?: undefined, input?: TerminalInput): Promise<string | undefined>;
  runAction(action: "clearSelection" | "scrollToLive", args?: undefined, input?: TerminalInput): Promise<void>;
  runAction(action: "scrollLines", args: number, input?: TerminalInput): Promise<void>;
  runAction<Name extends string>(action: Name extends TerminalActionName ? never : Name,
    args?: unknown, input?: TerminalInput): Promise<unknown>;
  runAction(action: InputActionHandler, args?: unknown, input?: TerminalInput): Promise<unknown>;
  async runAction(action: string | InputActionHandler, args?: unknown, input?: TerminalInput): Promise<unknown> {
    return this.#performAction(action, args, input);
  }

  async #performAction(action: string | InputActionHandler, args?: unknown, input?: TerminalInput): Promise<unknown> {
    if (this.#disposed) throw new Error("Terminal view is disposed");
    this.#inspectionError = "";
    if (typeof action === "function") return action(this.inputContext, args, input);
    const customAction = this.#actions.get(action);
    if (customAction) return customAction(this.inputContext, args, input);
    switch (action) {
      case TerminalAction.CopySelection:
        if (args === undefined) return this.copySelection();
        if (!isRecord(args)) throw new TypeError("Copy options must be an object");
        if (args.clear !== undefined && typeof args.clear !== "boolean") throw new TypeError("Copy clear must be a boolean");
        return this.copySelection({ clear: args.clear });
      case TerminalAction.PasteClipboard: return this.pasteClipboard();
      case TerminalAction.ClearSelection: return this.clearSelection();
      case TerminalAction.ScrollToLive: return this.scrollToLive();
      case TerminalAction.ScrollLines:
        if (typeof args !== "number") throw new RangeError("Scroll delta must be a signed 32-bit integer");
        return this.scrollLines(args);
      case TerminalAction.CopyOrPaste:
        if (this.#clipboardAction) throw new Error("A clipboard action is still in progress. Try again when it finishes.");
        this.#clipboardAction = true;
        try {
          if (this.selection.active || (this.selection.pending && this.selection.canExtend))
            return await this.copySelection({ clear: true });
          if (!this.#readOnly) return await this.pasteClipboard();
          return;
        } finally { this.#clipboardAction = false; }
      default: throw new TypeError(`Unknown terminal action: ${action}`);
    }
  }

  /** Sends an explicit paste through the producer's mode-aware input encoder. */
  paste(text: string): void {
    if (typeof text !== "string") throw new TypeError("Paste text must be a string");
    if (!this.#canInput()) throw new Error("Terminal view does not accept input");
    if (text) this.#inputCommand({ type: "paste", text });
  }

  async pasteClipboard(): Promise<string> {
    if (!this.#canInput()) throw new Error("Terminal view does not accept input");
    if (!navigator.clipboard?.readText)
      throw new Error("Clipboard reading is unavailable. Use the browser's paste shortcut instead.");
    const serial = this.#inputSerial;
    const generation = this.viewport.generation;
    const selectionId = this.selection.requestId;
    const focused = document.activeElement;
    const text = await navigator.clipboard.readText();
    if (!this.#canInput() || serial !== this.#inputSerial || generation !== this.viewport.generation ||
        selectionId !== this.selection.requestId || document.activeElement !== focused)
      throw new Error("Terminal input, selection, focus, or buffer changed while reading the clipboard. Paste again.");
    this.paste(text);
    return text;
  }

  get colorMode(): TerminalColorMode { return this.#colorMode; }
  get resolvedColorMode(): "light" | "dark" {
    return this.#colorMode === "system" ? (this.#colorScheme?.matches ? "dark" : "light") : this.#colorMode;
  }

  setColorMode(mode: TerminalColorMode): void {
    if (this.#disposed) throw new Error("Terminal view is disposed");
    if (mode === undefined) throw new TypeError("A color mode is required");
    this.#colorMode = normalizeColorMode(mode);
    this.#applyPalette();
  }

  setPalette(mode: "light" | "dark", palette: TerminalPalette): void {
    if (this.#disposed) throw new Error("Terminal view is disposed");
    if (mode !== "light" && mode !== "dark") throw new TypeError('Palette mode must be "light" or "dark"');
    this.#palettes[mode] = normalizePalette(palette);
    if (this.resolvedColorMode === mode) this.#applyPalette();
  }

  #applyPalette(): void {
    const palette = this.#palettes[this.resolvedColorMode];
    this.element.dataset.theme = this.resolvedColorMode;
    this.element.style.colorScheme = this.resolvedColorMode;
    this.element.style.backgroundColor = palette.background;
    for (const [name, color] of Object.entries(paletteScrollbarColors(palette)))
      this.element.style.setProperty(`--cp-terminal-scrollbar-${name}`, color);
    this.#post({ type: "palette", palette });
    this.#scrollbarController?.refresh();
  }

  /** Changes per-view input policy without reconnecting; server authorization remains host-owned. */
  setReadOnly(readOnly: boolean): void {
    if (typeof readOnly !== "boolean") throw new TypeError("readOnly must be a boolean");
    if (this.#disposed) throw new Error("Terminal view is disposed");
    if (this.#readOnly === readOnly) return;
    const inputFocused = document.activeElement === this.element &&
      (!this.element.shadowRoot?.activeElement || this.element.shadowRoot.activeElement === this.#input);
    this.#readOnly = readOnly;
    this.#inputSerial++;
    this.#resetComposition?.();
    if (this.#input) {
      this.#input.value = "";
      this.#input.disabled = !this.#canInput();
    }
    // Set the policy before cancelling so pending moves and button releases cannot leak.
    this.#mouse?.cancel();
    this.#mouse?.refresh();
    clearTimeout(this.#resizeTimer);
    this.#resizeTimer = undefined;
    this.#lastRequested = undefined;
    this.#queueResize(true);
    if (inputFocused) this.focus();
    this.#selectionUI?.refresh();
  }

  setLinks(options: false | TerminalLinkOptions): void {
    if (this.#disposed) throw new Error("Terminal view is disposed");
    if (options === undefined) throw new TypeError("Link options must be an object or false");
    const next = normalizeLinks(options, new Set(this.#actions.keys()));
    this.#links = next;
    this.#linkGeneration++;
    this.#detectedLinks = [];
    this.#replacePresentedLinks([]);
    this.#pendingLinks = undefined;
    this.#linkSnapshot = undefined;
    this.#hoveredLinkId = undefined;
    this.#linkDetector.configure(next ? next.detection : false);
    this.#mouse?.cancel();
    this.#mouse?.refresh();
    this.#postLinkConfiguration();
  }

  #postLinkConfiguration(): void {
    const enabled = !!(this.#links && (this.#links.osc8 ||
      (this.#links.detection && this.#links.detection.rules.some(rule => rule.enabled !== false))));
    this.#post({ type: "linkDetection", enabled, generation: this.#linkGeneration });
  }

  #acceptLinkSnapshot(snapshot: LinkDetectionSnapshot): void {
    this.#linkSnapshot = snapshot;
    this.#detectedLinks = [];
    this.#replacePresentedLinks([]);
    this.#pendingLinks = undefined;
    this.#linkDetector.update(snapshot);
    this.#mouse?.refresh();
  }

  #detectedPointerId(link: DetectedLink): string {
    return `detected/${this.#linkGeneration}/${this.#linkRevision}/${link.id}`;
  }

  #replacePresentedLinks(links: readonly DetectedLink[]): void {
    this.#presentedLinks = links;
    this.#detectedRows.clear();
    for (const link of links) {
      for (const range of link.activation.ranges) {
        const row = this.#detectedRows.get(range.row) ?? [];
        row.push({ startColumn: range.startColumn, endColumn: range.endColumn, link });
        this.#detectedRows.set(range.row, row);
      }
    }
  }

  #requestLinkDecorations(): void {
    if (!this.#worker || !this.#connected || !this.#linkRevision) return;
    const detection = this.#links && this.#links.detection;
    if (!detection) return;
    const visible = detection.decoration === "none" ? [] : detection.decoration === "hover"
      ? this.#detectedLinks.filter(link => this.#detectedPointerId(link) === this.#hoveredLinkId)
      : this.#detectedLinks;
    this.#pendingLinks = { revision: this.#linkRevision, generation: this.#linkGeneration,
      serial: ++this.#linkSerial, links: this.#detectedLinks };
    this.#post({ type: "linkDecorations", revision: this.#linkRevision, generation: this.#linkGeneration,
      serial: this.#linkSerial, ranges: visible.flatMap(link => link.activation.ranges),
      underlineStyle: detection.underlineStyle ?? "solid" });
  }

  #linkAt(point: TerminalPoint): PointerHyperlink | null {
    if (!this.#connected || this.viewport.pending || !this.#links) return null;
    const osc8 = this.#osc8Rows.get(point.y)?.find(range =>
      point.x >= range.startColumn && point.x < range.endColumn);
    if (osc8) {
      if (this.#links.osc8 === false) return null;
      const target = this.#links.osc8
        ? (!/[\u0000-\u0020\u007f]/u.test(osc8.uri) && URL.canParse(osc8.uri) ? osc8.uri : null)
        : this.#hyperlinks.at(point);
      if (!target || (this.#links.osc8 && this.#linkSnapshot?.revision !== this.#linkRevision)) return null;
      return { id: `osc8/${this.#linkGeneration}/${this.#linkRevision}/${osc8.row}/${osc8.startColumn}/${osc8.endColumn}`,
        target, activation: "modifierClick" };
    }
    const detection = this.#links.detection;
    if (!detection) return null;
    const link = this.#detectedRows.get(point.y)?.find(range =>
      point.x >= range.startColumn && point.x < range.endColumn)?.link;
    return link ? { id: this.#detectedPointerId(link), target: link.activation.target,
      activation: detection.activation ?? "modifierClick" } : null;
  }

  #activateLink(pointer: PointerHyperlink, input: TerminalInput): void {
    if (input.type !== "pointer" || this.#linkAt(input.point)?.id !== pointer.id || !this.#links) return;
    const detected = this.#presentedLinks.find(link => this.#detectedPointerId(link) === pointer.id);
    if (detected) {
      this.#performAction(detected.action, detected.activation, input).catch(error => this.#actionFailed(error));
      return;
    }
    if (!this.#links.osc8) {
      try { window.open(pointer.target, "_blank", "noopener,noreferrer"); }
      catch (error) { this.#actionFailed(error); }
      return;
    }
    const range = this.#osc8Rows.get(input.point.y)?.find(range =>
      input.point.x >= range.startColumn && input.point.x < range.endColumn);
    const snapshot = this.#linkSnapshot;
    if (!range || !snapshot) return;
    let text = "";
    for (let column = range.startColumn; column < range.endColumn; column++) {
      const cell = snapshot.cells[range.row * snapshot.columns + column];
      if (cell?.width && !(cell.attributes & 64)) text += cell.text;
    }
    const activation: TerminalLinkActivation = Object.freeze({
      source: "osc8", ruleId: null, kind: "uri", target: pointer.target, text,
      revision: this.#linkRevision,
      ranges: Object.freeze([Object.freeze({ row: range.row, startColumn: range.startColumn, endColumn: range.endColumn })]),
    });
    this.#performAction(this.#links.osc8.action, activation, input).catch(error => this.#actionFailed(error));
  }

  #forwardInput(input: TerminalInput): void {
    if (input.type === "key") {
      if (input.meta) throw new Error("Meta has no terminal key encoding; bind this shortcut to a local action instead.");
      this.#inputCommand({ type: "key", key: input.key, ctrl: input.ctrl, alt: input.alt, shift: input.shift });
    } else if (input.type === "paste") {
      if (this.#canInput()) this.paste(input.text);
    } else if (input.type === "text") {
      this.#inputCommand({ type: "input", text: input.text });
    }
  }

  #dispatchInput(input: TerminalInput, event?: Event, allowApplication = true): void {
    const decision = this.#resolveInput(input);
    // Shortcuts may run on inspection controls, but Enter/Space must still activate the control.
    if (!allowApplication && decision.action === undefined && decision.route !== InputRoute.Consume) return;
    if (decision.route === InputRoute.Browser ||
        (decision.route === InputRoute.Continue && input.type === "key")) return;
    event?.preventDefault();
    event?.stopPropagation();
    if (decision.action !== undefined) this.#executeInputAction(decision, input);
    else if (decision.route !== InputRoute.Consume) {
      try { this.#forwardInput(input); }
      catch (error) { this.#actionFailed(error); }
    }
  }

  #bindKeyboard() {
    const input = this.#input;
    const options = { signal: this.#listeners.signal };
    let composing = false;
    let compositionCommit: string | null = null;
    this.#resetComposition = () => {
      composing = false;
      compositionCommit = null;
      clearTimeout(this.#compositionTimer);
    };
    this.element.addEventListener("keydown", event => {
      if (event.defaultPrevented) return;
      if (event.composedPath().includes(this.#selectionOverlay)) return;
      const target = event.composedPath()[0];
      if (!this.#connected || event.isComposing || composing || event.key === "Process" || event.key === "Dead") return;
      if (event.getModifierState("AltGraph")) return;
      this.#dispatchInput({ type: "key", key: event.key, code: event.code, repeat: event.repeat,
        ...inputModifiers(event) }, event, target === input || target === this.element);
    }, { ...options, capture: true });
    input.addEventListener("paste", event => {
      if (event.defaultPrevented || !this.#connected || !event.clipboardData) return;
      this.#dispatchInput({ type: "paste", text: event.clipboardData.getData("text/plain") }, event);
      input.value = "";
    }, options);
    input.addEventListener("compositionstart", () => {
      if (!this.#canInput()) return;
      composing = true;
      compositionCommit = null;
      clearTimeout(this.#compositionTimer);
    }, options);
    input.addEventListener("compositionend", event => {
      if (!composing) return;
      composing = false;
      // Accommodate browsers placing the final input before or after compositionend.
      compositionCommit = typeof event.data === "string" ? event.data : input.value;
      this.#compositionTimer = setTimeout(() => {
        if (compositionCommit) this.#dispatchInput({ type: "text", text: compositionCommit });
        compositionCommit = null;
        input.value = "";
      }, 0);
    }, options);
    input.addEventListener("input", event => {
      const inputEvent = event instanceof InputEvent ? event : undefined;
      if (inputEvent?.isComposing || composing) return;
      clearTimeout(this.#compositionTimer);
      const text = compositionCommit ?? inputEvent?.data ?? input.value;
      compositionCommit = null;
      if (text && inputEvent?.inputType !== "insertFromPaste") this.#dispatchInput({ type: "text", text }, event);
      input.value = "";
    }, options);
  }

  focus() {
    if (this.#disposed) return;
    const target = this.#canInput() ? this.#input : this.element;
    target.focus({ preventScroll: true });
  }

  /** Request HMP1 primary explicitly; peer notifications confirm the result. */
  requestPrimary() {
    if (!this.#canInput()) throw new Error("Terminal view does not accept input");
    if (this.#size.width <= 0 || this.#size.height <= 0) throw new Error("Show the terminal container before taking primary");
    const grid = this.#fittedGrid();
    if (!grid) throw new Error("Show the terminal container before taking primary");
    if (this.#peer.id === null) {
      if (!this.#peer.isPrimary) throw new Error("Waiting for the HMP1 connection");
      this.resize(grid.columns, grid.rows);
      return;
    }
    this.#send({ type: "requestPrimary", ...grid });
  }

  /** Request a grid; never reflow locally before the authoritative response. */
  resize(columns: number, rows: number): void {
    const grid = dimensions(columns, rows);
    if (!this.#canInput()) throw new Error("Terminal view does not accept input");
    if (!this.#peer.isPrimary) throw new Error("Only the primary view can request a terminal resize");
    this.#send({ type: "resize", ...grid });
    this.#lastRequested = `${columns}x${rows}`;
  }

  /** Change the primary's sizing policy; applied grid dimensions still come from the server. */
  setSizing(sizing: TerminalSizing): void {
    const next = normalizeSizing(sizing, this.#sizing.fontSize);
    if (!this.#connected || this.#disposed) throw new Error("Terminal view is not connected");
    if (this.#readOnly) throw new Error("Terminal view does not accept input");
    if (!this.#peer.isPrimary) throw new Error("Only the primary view can change terminal sizing");
    this.#sizing = next;
    clearTimeout(this.#resizeTimer);
    this.#resizeTimer = undefined;
    this.#lastRequested = undefined;
    this.#fit();
    this.#queueResize(true);
    this.#options.onSizingChange?.(this.sizing);
  }

  resync() { this.#send({ type: "resync" }); }

  #disconnect() {
    this.#inputSerial++;
    this.#connected = false;
    this.#hyperlinks.update([]);
    this.#osc8Rows.clear();
    this.#detectedLinks = [];
    this.#replacePresentedLinks([]);
    this.#linkSnapshot = undefined;
    this.#pendingLinks = undefined;
    this.#linkDetector.clear();
    if (this.#input) this.#input.disabled = true;
    this.#mouse?.update(1, 1, 0);
    this.#history.disconnect();
    this.#markerState.disconnect();
    this.#scrollbarController?.cancel();
    this.#inspectionChanged();
    clearTimeout(this.#resizeTimer);
    this.#resizeTimer = undefined;
  }

  #fail(error: Error): void {
    if (this.#disposed) return;
    this.#stopTransport();
    this.#disconnect();
    this.#ready.reject(error);
    this.#options.onStatus?.(error.message, "error");
    this.#worker?.terminate();
  }

  /** Detach this view. The server-side shared terminal is not terminated. */
  dispose() {
    if (this.#disposed) return;
    this.#disposed = true;
    this.#stopTransport();
    this.#disconnect();
    this.#linkDetector.dispose();
    this.#ready.reject(new DOMException("Terminal view was disposed", "AbortError"));
    clearTimeout(this.#readyTimer);
    clearTimeout(this.#compositionTimer);
    this.#observer?.disconnect();
    this.#colorScheme?.removeEventListener("change", this.#systemColorChanged);
    this.#mouse?.dispose();
    this.#scrollbarController?.dispose();
    this.#scrollbarTooltip?.dispose();
    this.#listeners.abort();
    this.#post({ type: "stop" });
    this.#worker?.terminate();
    this.element.remove();
  }

  #stopTransport(): void {
    this.#transportSession?.dispose();
    this.#transportFrame?.reject(new DOMException("Terminal transport was disposed or closed", "AbortError"));
    this.#transportFrame = undefined;
  }
}
