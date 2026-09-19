import type {
  TerminalLayout, TerminalMarker, TerminalRectangle, TerminalScrollbar,
  TerminalScrollbarConfiguration, TerminalScrollbarMarker
} from "./scrollbar-types.js";
import type { TerminalViewport } from "./types.js";
import { renderDefaultScrollbar } from "./scrollbar-renderer.js";
import { scrollbarColors, scrollbarMarkerColor } from "./scrollbar-colors.js";
export { renderDefaultScrollbar } from "./scrollbar-renderer.js";

export function normalizeScrollbar(input?: TerminalScrollbar): false | TerminalScrollbarConfiguration {
  if (input === false) return false;
  if (input !== undefined && (input === null || typeof input !== "object" || Array.isArray(input)))
    throw new TypeError("scrollbar must be false or an options object");
  const options = input ?? {};
  const number = (value: number | undefined, fallback: number, name: string, min = 0, max = Infinity) => {
    const result = value === undefined ? fallback : value;
    if (!Number.isFinite(result) || result < min || result > max)
      throw new RangeError(`scrollbar.${name} must be a finite number between ${min} and ${max}`);
    return result;
  };
  if (options.placement !== undefined && options.placement !== "overlay" && options.placement !== "beside")
    throw new TypeError("scrollbar.placement must be overlay or beside");
  if (options.markers !== undefined && typeof options.markers !== "boolean")
    throw new TypeError("scrollbar.markers must be boolean");
  if (options.render !== undefined && (typeof options.render !== "function" ||
      options.render.constructor.name === "AsyncFunction"))
    throw new TypeError("scrollbar.render must be a synchronous function");
  if (options.tooltip !== undefined && options.tooltip !== false &&
    (typeof options.tooltip !== "function" || options.tooltip.constructor.name === "AsyncFunction"))
    throw new TypeError("scrollbar.tooltip must be false or a synchronous function");
  return Object.freeze({
    placement: options.placement ?? "overlay",
    width: number(options.width, 12, "width", 4, 64),
    proximity: number(options.proximity, 24, "proximity"),
    hideDelay: number(options.hideDelay, 900, "hideDelay"),
    fadeDuration: number(options.fadeDuration, 300, "fadeDuration"),
    markers: options.markers ?? true,
    ...(options.render ? { render: options.render } : {}),
    ...(options.tooltip !== undefined ? { tooltip: options.tooltip } : {})
  });
}

const clamp = (value: number, min: number, max: number) => Math.max(min, Math.min(max, value));
const contains = (rect: TerminalRectangle, x: number, y: number, margin = 0) =>
  x >= rect.left - margin && x < rect.left + rect.width + margin &&
  y >= rect.top - margin && y < rect.top + rect.height + margin;

export function scrollbarOpacity(now: number, lastActivityAt: number, active: boolean,
  configuration: TerminalScrollbarConfiguration, reducedMotion: boolean): number {
  if (configuration.placement === "beside" || active) return 1;
  const elapsed = now - lastActivityAt - configuration.hideDelay;
  if (elapsed < 0) return 1;
  if (reducedMotion || configuration.fadeDuration === 0) return 0;
  return clamp(1 - elapsed / configuration.fadeDuration, 0, 1);
}

export function scrollbarGeometry(layout: TerminalLayout, viewport: TerminalViewport,
  markers: readonly TerminalMarker[], target: number | null = null) {
  const track = layout.scrollbar;
  if (!track || track.width <= 0 || track.height <= 0 || !viewport.available ||
    viewport.buffer !== "main" || viewport.liveTop <= 0) return null;
  const visibleRows = Math.max(1, viewport.totalRows - viewport.liveTop);
  const height = Math.min(track.height, Math.max(24, track.height * visibleRows / viewport.totalRows));
  const thumb = Object.freeze({
    left: track.left, top: track.top + (track.height - height) *
      clamp(target ?? viewport.top, 0, viewport.liveTop) / viewport.liveTop,
    width: track.width, height
  });
  const ticks: TerminalScrollbarMarker[] = markers
    .filter(marker => marker.buffer === viewport.buffer && marker.row !== null &&
      Number.isFinite(marker.row) && marker.row >= 0 && marker.row < viewport.totalRows &&
      (marker.source === "custom" || marker.phase !== "prompt"))
    .map(marker => {
      const height = Math.min(3, track.height);
      return Object.freeze({
        marker: Object.freeze({ ...marker }),
        bounds: Object.freeze({
          left: track.left, top: track.top + (track.height - height) *
            marker.row! / Math.max(1, viewport.totalRows - 1), width: track.width, height
        })
      });
    });
  return Object.freeze({ track: Object.freeze({ ...track }), thumb, markers: Object.freeze(ticks) });
}

export function scrollbarMarkerAt(markers: readonly TerminalScrollbarMarker[], x: number, y: number) {
  return markers.filter(tick => contains(tick.bounds, x, y, 3))
    .sort((a, b) => Math.abs(a.bounds.top + a.bounds.height / 2 - y) -
      Math.abs(b.bounds.top + b.bounds.height / 2 - y) ||
      (a.marker.id < b.marker.id ? -1 : a.marker.id > b.marker.id ? 1 : 0))[0]?.marker;
}

interface ScrollbarState {
  layout: TerminalLayout;
  viewport: TerminalViewport;
  markers: readonly TerminalMarker[];
  connected: boolean;
  configuration: false | TerminalScrollbarConfiguration;
}

interface ScrollbarControllerOptions {
  element: HTMLElement;
  canvas: HTMLCanvasElement;
  accessibility: HTMLDivElement;
  getState: () => ScrollbarState;
  scrollToRow: (top: number) => void;
  scrollToLive: () => void;
  scrollToMarker: (id: string) => Promise<void>;
  reportError: (error: unknown) => void;
  onMarkerHover?: (marker: TerminalScrollbarMarker | null) => void;
}

export class ScrollbarController {
  private context: CanvasRenderingContext2D | null | undefined;
  private readonly listeners: (() => void)[] = [];
  private readonly motion: MediaQueryList | undefined;
  private configuration: false | TerminalScrollbarConfiguration | undefined;
  private frameId: number | undefined;
  private fadeTimer: ReturnType<typeof setTimeout> | undefined;
  private disposed = false;
  private failed = false;
  private dormant = false;
  private near = false;
  private hovered = false;
  private focused = false;
  private lastActivityAt = -Infinity;
  private pointer: number | null = null;
  private dragOffset: number | null = null;
  private contentPointers = new Set<number>();
  private suppressClick = false;
  private desired: number | null = null;
  private queued: number | null = null;
  private requestBaseline = 0;
  private generation: string | undefined;
  private wheelRemainder = 0;
  private hoverPoint: { clientX: number; clientY: number } | null = null;

  constructor(private readonly options: ScrollbarControllerOptions) {
    this.motion = typeof matchMedia === "function" ? matchMedia("(prefers-reduced-motion: reduce)") : undefined;
    const on = <K extends keyof HTMLElementEventMap>(target: HTMLElement, name: K,
      handler: (event: HTMLElementEventMap[K]) => void, passive = false) => {
      target.addEventListener(name, handler, { capture: true, passive });
      this.listeners.push(() => target.removeEventListener?.(name, handler, { capture: true }));
    };
    on(options.element, "pointerdown", event => this.pointerDown(event));
    on(options.element, "pointermove", event => this.pointerMove(event));
    on(options.element, "pointerup", event => this.pointerEnd(event, false));
    on(options.element, "pointercancel", event => this.pointerEnd(event, true));
    on(options.element, "lostpointercapture", event => this.pointerEnd(event, true));
    on(options.element, "pointerleave", () => {
      this.near = this.hovered = false;
      this.clearHover();
      this.activity();
    });
    on(options.element, "wheel", event => this.wheel(event));
    for (const name of ["mousedown", "mouseup", "click", "dblclick", "contextmenu"] as const)
      on(options.element, name, event => {
        if (this.contentPointers.size) return;
        if (this.pointer !== null || this.hit(event) ||
          (this.suppressClick && (name === "click" || name === "mouseup"))) {
          this.consume(event);
          if (name === "click") this.suppressClick = false;
        }
      });
    on(options.accessibility, "keydown", event => this.keyDown(event));
    on(options.accessibility, "focus", () => { this.focused = true; this.activity(); });
    on(options.accessibility, "blur", () => { this.focused = false; this.activity(); });
    if (this.motion) {
      const changed = () => this.invalidate();
      this.motion.addEventListener("change", changed);
      this.listeners.push(() => this.motion?.removeEventListener("change", changed));
    }
    options.accessibility.setAttribute("role", "scrollbar");
    options.accessibility.setAttribute("aria-label", "Terminal scrollback");
    options.accessibility.setAttribute("aria-orientation", "vertical");
    options.accessibility.style.position = "absolute";
    options.accessibility.style.background = "transparent";
    this.refresh();
  }

  refresh(): void {
    if (this.disposed) return;
    const state = this.options.getState();
    if (state.configuration !== this.configuration) {
      this.cancel();
      this.configuration = state.configuration;
      this.failed = false;
      this.lastActivityAt = this.now();
    }
    if (state.viewport.generation !== this.generation) {
      this.endGesture();
      this.desired = this.queued = null;
      this.generation = state.viewport.generation;
    }
    if (this.pointer === null && this.queued === null && state.viewport.available &&
      !state.viewport.pending && (state.viewport.top === this.desired ||
        state.viewport.requestId > this.requestBaseline)) this.desired = null;
    const geometry = this.geometry(state);
    if (!geometry) {
      this.cancel();
      this.options.accessibility.hidden = true;
      this.options.accessibility.tabIndex = -1;
      this.clear();
      return;
    }
    const { track } = geometry;
    const accessibility = this.options.accessibility;
    accessibility.hidden = false;
    accessibility.tabIndex = 0;
    if (accessibility.matches?.(":focus")) this.focused = true;
    Object.assign(accessibility.style, {
      left: `${track.left}px`, top: `${track.top}px`,
      width: `${track.width}px`, height: `${track.height}px`
    });
    accessibility.setAttribute("aria-valuemin", "0");
    accessibility.setAttribute("aria-valuemax", String(state.viewport.liveTop));
    accessibility.setAttribute("aria-valuenow", String(this.desired ?? state.viewport.top));
    accessibility.setAttribute("aria-valuetext", state.viewport.following ? "Live terminal output" :
      `Scrollback row ${this.desired ?? state.viewport.top}`);
    accessibility.setAttribute("aria-busy", String(state.viewport.pending));
    this.options.onMarkerHover?.(this.hoveredMarker());
    this.schedule();
  }

  activity(): void {
    if (this.disposed) return;
    this.dormant = false;
    this.lastActivityAt = this.now();
    if (this.fadeTimer !== undefined) clearTimeout(this.fadeTimer);
    this.fadeTimer = undefined;
    this.schedule();
  }

  invalidate(): void {
    this.dormant = false;
    this.refresh();
  }

  cancel(): void {
    this.clearHover();
    this.endGesture();
    this.contentPointers.clear();
    this.desired = this.queued = null;
    this.near = this.hovered = this.focused = this.suppressClick = false;
    this.dormant = false;
    this.wheelRemainder = 0;
    if (this.frameId !== undefined) cancelAnimationFrame(this.frameId);
    if (this.fadeTimer !== undefined) clearTimeout(this.fadeTimer);
    this.frameId = this.fadeTimer = undefined;
    this.options.accessibility.hidden = true;
    this.options.accessibility.tabIndex = -1;
    this.options.accessibility.title = "";
    this.clear();
  }

  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.cancel();
    for (const remove of this.listeners) remove();
    this.options.accessibility.hidden = true;
    this.options.accessibility.tabIndex = -1;
  }

  private now() { return performance.now(); }

  private geometry(state = this.options.getState()) {
    return state.connected && state.configuration ?
      scrollbarGeometry(state.layout, state.viewport, state.configuration.markers ? state.markers : [], this.desired) : null;
  }

  private point(event: Pick<MouseEvent, "clientX" | "clientY">) {
    const bounds = this.options.element.getBoundingClientRect();
    const layout = this.options.getState().layout;
    return {
      x: bounds.width > 0 ? (event.clientX - bounds.left) * layout.width / bounds.width : -Infinity,
      y: bounds.height > 0 ? (event.clientY - bounds.top) * layout.height / bounds.height : -Infinity
    };
  }

  private hit(event: MouseEvent) {
    const geometry = this.geometry();
    const point = this.point(event);
    return !!geometry && contains(geometry.track, point.x, point.y);
  }

  private consume(event: Event) {
    event.preventDefault();
    event.stopImmediatePropagation();
  }

  private pointerDown(event: PointerEvent) {
    this.clearHover();
    this.suppressClick = false;
    if (!this.hit(event)) {
      this.contentPointers.add(event.pointerId);
      return;
    }
    if (this.contentPointers.size) return;
    this.consume(event);
    if (event.button !== 0 || this.pointer !== null) return;
    const geometry = this.geometry()!;
    const point = this.point(event);
    this.pointer = event.pointerId;
    try { this.options.element.setPointerCapture(event.pointerId); }
    catch (error) {
      this.endGesture();
      this.report(error);
      return;
    }
    this.options.accessibility.setAttribute("data-pointer-active", "true");
    this.options.accessibility.focus({ preventScroll: true });
    this.focused = true;
    this.activity();
    if (contains(geometry.thumb, point.x, point.y)) {
      this.dragOffset = point.y - geometry.thumb.top;
      return;
    }
    const marker = scrollbarMarkerAt(geometry.markers, point.x, point.y);
    if (marker) { this.jump(marker.id); return; }
    const viewport = this.options.getState().viewport;
    if (viewport.available) this.navigate((this.desired ?? viewport.top) +
      (point.y < geometry.thumb.top ? -1 : 1) * Math.max(1, viewport.totalRows - viewport.liveTop));
  }

  private pointerMove(event: PointerEvent) {
    const geometry = this.geometry();
    if (!geometry) return;
    const point = this.point(event);
    if (this.pointer === event.pointerId) {
      this.consume(event);
      if (this.dragOffset !== null) {
        const viewport = this.options.getState().viewport;
        const travel = geometry.track.height - geometry.thumb.height;
        if (viewport.available && travel > 0)
          this.navigate((point.y - this.dragOffset - geometry.track.top) / travel * viewport.liveTop);
      }
      this.activity();
      return;
    }
    if (this.contentPointers.size || event.buttons !== 0) { this.clearHover(); return; }
    const previous = this.hoveredMarker();
    this.hoverPoint = { clientX: event.clientX, clientY: event.clientY };
    const marker = this.hoveredMarker();
    this.options.onMarkerHover?.(marker);
    if (previous?.marker.id !== marker?.marker.id) {
      this.dormant = false;
      this.schedule();
    }
    const configuration = this.options.getState().configuration;
    const near = contains(geometry.track, point.x, point.y, configuration ? configuration.proximity : 0);
    const hovered = contains(geometry.track, point.x, point.y);
    if (near !== this.near || hovered !== this.hovered) {
      this.near = near;
      this.hovered = hovered;
      this.activity();
    }
    if (hovered) this.consume(event);
  }

  private clearHover() {
    this.hoverPoint = null;
    this.options.onMarkerHover?.(null);
  }

  private hoveredMarker(): TerminalScrollbarMarker | null {
    if (!this.hoverPoint || this.pointer !== null || this.contentPointers.size) return null;
    const geometry = this.geometry();
    if (!geometry) return null;
    const point = this.point(this.hoverPoint);
    if (!contains(geometry.track, point.x, point.y)) return null;
    const marker = scrollbarMarkerAt(geometry.markers, point.x, point.y);
    return geometry.markers.find(tick => tick.marker.id === marker?.id) ?? null;
  }

  private pointerEnd(event: PointerEvent, cancelled: boolean) {
    this.contentPointers.delete(event.pointerId);
    if (this.pointer !== event.pointerId) return;
    this.consume(event);
    this.suppressClick = !cancelled;
    this.endGesture();
    if (cancelled) this.desired = this.queued = null;
    this.activity();
  }

  private endGesture() {
    const pointer = this.pointer;
    this.pointer = this.dragOffset = null;
    this.options.accessibility.setAttribute("data-pointer-active", "false");
    if (pointer !== null) {
      try {
        if (this.options.element.hasPointerCapture(pointer)) this.options.element.releasePointerCapture(pointer);
      } catch (error) { this.report(error); }
    }
  }

  private wheel(event: WheelEvent) {
    if (this.contentPointers.size || !this.hit(event)) return;
    this.consume(event);
    const { viewport, layout } = this.options.getState();
    if (!viewport.available || !Number.isFinite(event.deltaY)) return;
    const rows = Math.max(1, viewport.totalRows - viewport.liveTop);
    const bounds = this.options.element.getBoundingClientRect();
    const scale = layout.height > 0 ? bounds.height / layout.height : 1;
    this.wheelRemainder += event.deltaY * (event.deltaMode === 2 ? rows :
      event.deltaMode === 1 ? 1 : 1 / Math.max(1, layout.cellHeight * scale));
    const amount = Math.trunc(this.wheelRemainder);
    this.wheelRemainder -= amount;
    if (amount !== 0) this.navigate((this.desired ?? viewport.top) + amount);
    this.activity();
  }

  private keyDown(event: KeyboardEvent) {
    const { viewport } = this.options.getState();
    if (!this.geometry() || !viewport.available || event.ctrlKey || event.metaKey) return;
    const top = this.desired ?? viewport.top;
    const page = Math.max(1, viewport.totalRows - viewport.liveTop);
    if (event.altKey && (event.key === "ArrowDown" || event.key === "ArrowUp")) {
      this.consume(event);
      const forward = event.key === "ArrowDown";
      const markers = this.geometry()!.markers.map(tick => tick.marker)
        .filter(marker => forward ? marker.row! > top : marker.row! < top)
        .sort((a, b) => (forward ? 1 : -1) * (a.row! - b.row!) ||
          (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
      if (markers[0]) this.jump(markers[0].id);
      this.activity();
      return;
    }
    if (event.altKey) return;
    const targets: Record<string, number> = {
      ArrowUp: top - 1, ArrowDown: top + 1, PageUp: top - page, PageDown: top + page,
      Home: 0, End: viewport.liveTop
    };
    if (!Object.hasOwn(targets, event.key)) return;
    this.consume(event);
    this.navigate(targets[event.key]!);
    this.activity();
  }

  private jump(id: string) {
    this.desired = this.queued = null;
    try { void this.options.scrollToMarker(id).catch(error => this.report(error)); }
    catch (error) { this.report(error); }
  }

  private navigate(top: number) {
    const viewport = this.options.getState().viewport;
    if (!viewport.available || !Number.isFinite(top)) return;
    this.requestBaseline = viewport.requestId;
    this.desired = this.queued = Math.round(clamp(top, 0, viewport.liveTop));
    this.schedule();
  }

  private schedule() {
    if (this.disposed || this.frameId !== undefined || !this.geometry() ||
      (this.dormant && this.queued === null) ||
      typeof requestAnimationFrame !== "function") return;
    this.frameId = requestAnimationFrame(() => {
      this.frameId = undefined;
      this.paint();
    });
  }

  private clear() {
    if (!this.context) return;
    this.context.save();
    this.context.setTransform(1, 0, 0, 1, 0, 0);
    this.context.clearRect(0, 0, this.options.canvas.width, this.options.canvas.height);
    this.context.restore();
  }

  private paint() {
    if (this.disposed) return;
    const state = this.options.getState();
    if (!this.geometry(state) || !state.configuration) return;
    if (this.queued !== null) {
      const target = this.queued;
      this.queued = null;
      try {
        if (state.viewport.available && target >= state.viewport.liveTop) this.options.scrollToLive();
        else this.options.scrollToRow(target);
      } catch (error) { this.desired = null; this.report(error); }
    }
    const geometry = this.geometry(state)!;
    this.options.onMarkerHover?.(this.hoveredMarker());
    if (this.disposed || state.configuration !== this.options.getState().configuration) return;
    const { canvas, accessibility } = this.options;
    accessibility.setAttribute("aria-valuenow", String(this.desired ?? state.viewport.top));
    if (!this.failed && this.context === undefined) {
      try {
        this.context = canvas.getContext("2d");
        if (!this.context) throw new Error("The scrollbar Canvas2D context is unavailable");
      }
      catch (error) {
        this.failed = true;
        this.endGesture();
        this.report(error);
      }
    }
    if (!this.context || this.failed) return;
    const context = this.context;
    const dpr = clamp(typeof devicePixelRatio === "number" ? devicePixelRatio : 1, 1, 3);
    const width = Math.max(1, Math.round(state.layout.width * dpr));
    const height = Math.max(1, Math.round(state.layout.height * dpr));
    if (canvas.width !== width) canvas.width = width;
    if (canvas.height !== height) canvas.height = height;
    this.clear();
    const now = this.now();
    const active = this.near || this.focused || this.pointer !== null;
    const autoHide = state.configuration.placement === "overlay";
    const reducedMotion = this.motion?.matches ?? false;
    const opacity = scrollbarOpacity(now, this.lastActivityAt, active, state.configuration, reducedMotion);
    const style = typeof getComputedStyle === "function" ? getComputedStyle(this.options.element) : null;
    const color = (name: string, fallback: string) => style?.getPropertyValue?.(name).trim() || fallback;
    const scrollbarColor = (name: keyof typeof scrollbarColors) =>
      color(`--cp-view-scrollbar-${name}`, scrollbarColors[name]);
    const layout = Object.freeze({
      ...state.layout, content: Object.freeze({ ...state.layout.content }),
      padding: Object.freeze({ ...state.layout.padding }), scrollbar: geometry.track
    });
    const viewport = Object.freeze({ ...state.viewport,
      ...(state.viewport.rowIds ? { rowIds: Object.freeze([...state.viewport.rowIds]) } : {})
    }) as TerminalViewport;
    const frame = Object.freeze({
      canvas, context, layout, viewport, ...geometry, now, opacity, pendingTarget: this.desired,
      markers: Object.freeze(geometry.markers.map(tick => Object.freeze({
        ...tick, color: scrollbarMarkerColor(tick.marker, scrollbarColor)
      }))),
      hoveredMarker: this.hoveredMarker(),
      interaction: Object.freeze({
        near: this.near, hovered: this.hovered, dragging: this.dragOffset !== null,
        focused: this.focused, lastActivityAt: this.lastActivityAt, reducedMotion
      }),
      colors: Object.freeze({
        track: scrollbarColor("track"),
        thumb: scrollbarColor("thumb"),
        marker: scrollbarColor("marker"),
        error: scrollbarColor("error")
      })
    });
    let again: boolean | void = false;
    context.save();
    try {
      context.setTransform(dpr, 0, 0, dpr, 0, 0);
      const result: unknown = (state.configuration.render ?? renderDefaultScrollbar)(frame);
      if (result !== undefined && typeof result !== "boolean") {
        // Attach a rejection observer before reporting an unsupported asynchronous painter.
        if (result instanceof Promise) void result.catch(error => {
          if (!this.disposed) this.report(error);
        });
        throw new TypeError("scrollbar.render must synchronously return boolean or undefined");
      }
      again = result;
    } catch (error) {
      this.failed = true;
      this.endGesture();
      this.desired = this.queued = null;
      if (this.frameId !== undefined) cancelAnimationFrame(this.frameId);
      if (this.fadeTimer !== undefined) clearTimeout(this.fadeTimer);
      this.frameId = this.fadeTimer = undefined;
      this.report(error);
    } finally { context.restore(); }
    if (this.failed) { this.clear(); return; }
    if (this.disposed || state.configuration !== this.options.getState().configuration) return;
    this.dormant = !active && opacity === 0 && again !== true;
    if (again === true || (autoHide && !active && opacity > 0 && now >= this.lastActivityAt + state.configuration.hideDelay))
      this.schedule();
    else if (autoHide && !active && opacity > 0 && this.fadeTimer === undefined) {
      this.fadeTimer = setTimeout(() => {
        this.fadeTimer = undefined;
        this.schedule();
      }, Math.min(2_147_483_647, Math.max(0, this.lastActivityAt + state.configuration.hideDelay - now)));
    }
  }

  private report(error: unknown) {
    if (!this.disposed) this.options.reportError(error);
  }
}
