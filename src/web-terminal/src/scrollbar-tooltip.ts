import { getCmdlineUrl } from "./command-mark.js";
import { isRecord } from "./validation.js";
import type { TerminalCommandMark } from "./types.js";
import type {
  TerminalLayout, TerminalScrollbarMarker, TerminalScrollbarTooltipContext, TerminalScrollbarTooltipRenderer
} from "./scrollbar-types.js";

/** Creates safe, theme-aware tooltip content; the controller owns mounting and positioning. */
export function renderDefaultScrollbarTooltip(context: TerminalScrollbarTooltipContext): HTMLElement {
  const element = document.createElement("div");
  element.className = "hex1b-scrollbar-tooltip";
  element.setAttribute("role", "tooltip");
  element.style.cssText = "padding:8px 10px;border:1px solid var(--cp-view-text-muted,#888);" +
    "border-radius:6px;background:var(--cp-view-surface,#202020);color:var(--cp-view-text,#eee);" +
    "font:12px/1.4 var(--cp-view-font-family,monospace);white-space:pre-wrap;overflow-wrap:anywhere";
  const { marker, details } = context;
  const lines = [marker.label || (marker.source === "custom" ? "Bookmark" : "Terminal command")];
  if (marker.source === "command") {
    lines.push(`Phase: ${marker.phase ?? "unknown"}`);
    if (marker.exitCode != null) lines.push(`Exit code: ${marker.exitCode}`);
    if (context.loading) lines.push("Loading command details...");
    if (context.error) lines.push(`Command details unavailable: ${context.error}`);
    if (details) {
      const encoded = getCmdlineUrl(details);
      if (encoded) {
        try { lines.push(decodeURIComponent(encoded)); }
        catch (error) {
          if (!(error instanceof URIError)) throw error;
          lines.push(`Command (invalid URL encoding): ${encoded}`);
        }
      } else if (details.rawParameters) lines.push(details.rawParameters);
      else lines.push("This shell did not provide command text.");
    }
  }
  element.textContent = lines.join("\n");
  return element;
}

interface TooltipTarget {
  tick: TerminalScrollbarMarker;
  layout: TerminalLayout;
  renderer: TerminalScrollbarTooltipRenderer;
  key: string;
  signature: string;
}

/** Owns only hover presentation; input and retained marker navigation stay in their controllers. */
export class ScrollbarTooltip {
  private target: TooltipTarget | null = null;
  private result: { key: string; details: TerminalCommandMark | null; error: string | null } | null = null;
  private fetching = false;
  private lifetime: AbortController | undefined;
  private observer: ResizeObserver | undefined;
  private element: HTMLElement | null = null;
  private failed = false;
  private disposed = false;

  constructor(private readonly options: {
    overlay: HTMLElement;
    getDetails: (id: string) => Promise<TerminalCommandMark>;
    reportError: (error: unknown) => void;
  }) {}

  update(tick: TerminalScrollbarMarker | null, layout: TerminalLayout,
    renderer: false | TerminalScrollbarTooltipRenderer = renderDefaultScrollbarTooltip): void {
    if (this.disposed) return;
    if (!tick || renderer === false) {
      this.target = this.result = null;
      this.failed = false;
      this.clearView();
      return;
    }
    const signature = JSON.stringify({ tick, layout });
    if (this.target?.signature === signature && this.target.renderer === renderer) return;
    const key = JSON.stringify([tick.marker.id, tick.marker.phase, tick.marker.exitCode]);
    if (this.target?.key !== key || this.target.renderer !== renderer) {
      this.result = null;
      this.failed = false;
    }
    this.target = { tick, layout, renderer, key, signature };
    this.render();
    this.loadDetails();
  }

  dispose(): void {
    this.disposed = true;
    this.target = this.result = null;
    this.clearView();
  }

  private clearView() {
    this.observer?.disconnect();
    this.observer = undefined;
    const lifetime = this.lifetime;
    this.lifetime = undefined;
    this.element = null;
    this.options.overlay.replaceChildren();
    lifetime?.abort();
  }

  private render() {
    const target = this.target;
    this.clearView();
    if (!target || this.target !== target || this.failed || this.disposed) return;
    const lifetime = this.lifetime = new AbortController();
    const result = this.result?.key === target.key ? this.result : null;
    const context: TerminalScrollbarTooltipContext = Object.freeze({
      marker: target.tick.marker, anchor: target.tick.bounds, layout: target.layout,
      details: result?.details ?? null, error: result?.error ?? null,
      loading: target.tick.marker.source === "command" && !result,
      signal: lifetime.signal
    });
    try {
      const element: unknown = target.renderer(context);
      if (isRecord(element) && typeof element.then === "function")
        void Promise.resolve(element).catch(error => {
          if (!lifetime.signal.aborted) this.options.reportError(error);
        });
      if (lifetime.signal.aborted) return;
      if (element !== null && !(element instanceof HTMLElement)) {
        throw new TypeError("scrollbar.tooltip must synchronously return an HTMLElement or null");
      }
      if (!element) return;
      this.element = element;
      Object.assign(element.style, {
        position: "absolute", boxSizing: "border-box", margin: "0", pointerEvents: "none",
        maxWidth: `${Math.max(0, Math.min(360, target.layout.width - 16))}px`,
        maxHeight: `${Math.max(0, target.layout.height - 16)}px`, overflow: "hidden"
      });
      this.options.overlay.append(element);
      this.position();
      this.observer = new ResizeObserver(() => this.position());
      this.observer.observe(element);
    } catch (error) {
      this.failed = true;
      this.clearView();
      this.options.reportError(error);
    }
  }

  private position() {
    if (!this.target || !this.element) return;
    const { bounds } = this.target.tick;
    const { width, height } = this.target.layout;
    const left = bounds.left - this.element.offsetWidth - 8;
    const top = bounds.top + bounds.height / 2 - this.element.offsetHeight / 2;
    this.element.style.left = `${Math.max(0, Math.min(Math.max(8, left), width - this.element.offsetWidth - 8))}px`;
    this.element.style.top = `${Math.max(0, Math.min(Math.max(8, top), height - this.element.offsetHeight - 8))}px`;
  }

  private loadDetails() {
    const target = this.target;
    if (!target || this.fetching || this.failed || this.disposed ||
      target.tick.marker.source !== "command" || this.result?.key === target.key) return;
    this.fetching = true;
    // Coalesce rapid hover changes: at most one RPC, followed by only the latest hovered mark.
    void Promise.resolve().then(() => this.disposed || this.target?.key !== target.key
      ? null : this.options.getDetails(target.tick.marker.id)).then(details => {
      if (!details || this.target?.key !== target.key || this.disposed) return;
      this.result = { key: target.key, details: Object.freeze({ ...details }), error: null };
      this.render();
    }, error => {
      if (this.target?.key !== target.key || this.disposed) return;
      this.result = { key: target.key, details: null, error: error instanceof Error ? error.message : String(error) };
      this.render();
    }).finally(() => {
      this.fetching = false;
      this.loadDetails();
    });
  }
}
