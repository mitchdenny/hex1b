import type { WebTerminalHandle } from "@hex1b/web-terminal";

// Below the scroll-height limits of supported browsers. Read back the actual range,
// since rounding and platform scrollbar sizes can still change it.
const maximumExtent = 8_000_000;

/** Demo-owned chrome: only the spacer scrolls; the terminal mount is its fixed sibling. */
export class NativeScrollbar {
  readonly element = document.createElement("div");
  readonly rail = document.createElement("div");
  private readonly spacer = document.createElement("div");
  private readonly ticks = document.createElement("div");
  private readonly controller = new AbortController();
  private markerController = new AbortController();
  private frame = 0;
  private desired: number | undefined;
  private synchronizedTop = 0;
  private markerSignature = "";
  private showMarkers = true;
  private disposed = false;

  constructor(private readonly terminal: WebTerminalHandle, parent: HTMLElement,
    private readonly reportError: (error: unknown) => void) {
    this.element.className = "native-scrollbar";
    this.rail.className = "native-scrollbar-rail";
    this.rail.tabIndex = 0;
    this.rail.setAttribute("aria-label", "Terminal history");
    this.spacer.className = "native-scrollbar-spacer";
    this.spacer.setAttribute("aria-hidden", "true");
    this.ticks.className = "native-scrollbar-markers";
    this.ticks.setAttribute("aria-label", "Retained terminal markers");
    this.rail.append(this.spacer);
    this.element.append(this.ticks, this.rail);
    parent.append(this.element);
    this.rail.addEventListener("scroll", () => {
      if (Math.abs(this.rail.scrollTop - this.synchronizedTop) < .5) return;
      this.synchronizedTop = this.rail.scrollTop;
      const viewport = terminal.viewport;
      if (!terminal.connected || !viewport.available) return;
      const range = this.rail.scrollHeight - this.rail.clientHeight;
      this.desired = range > 0 ? Math.round(this.rail.scrollTop / range * viewport.liveTop) : 0;
      if (!this.frame) this.frame = requestAnimationFrame(() => {
        this.frame = 0;
        const target = this.desired;
        this.desired = undefined;
        if (target !== undefined && terminal.connected) {
          try { terminal.scrollToRow(target); }
          catch (error) { this.reportError(error); }
        }
      });
    }, { signal: this.controller.signal });
    this.update();
  }

  setMarkers(visible: boolean) {
    this.showMarkers = visible;
    this.update();
  }

  update() {
    if (this.disposed) return;
    const { viewport, layout, connected } = this.terminal;
    this.element.style.marginTop = `${layout.content.top}px`;
    this.element.style.height = `${layout.content.height}px`;
    this.element.inert = !connected || !viewport.available;
    const maximumRow = viewport.available ? viewport.liveTop : 0;
    const height = this.rail.clientHeight;
    this.spacer.style.height = `${Math.min(maximumExtent, height + maximumRow * layout.cellHeight)}px`;
    const range = Math.max(0, this.rail.scrollHeight - height);
    // Pending frames may describe a previous drag target. Never pull a native
    // thumb backwards until the final authoritative presentation has arrived.
    if (!viewport.pending && this.desired === undefined && !this.frame) {
      this.rail.scrollTop = viewport.available && maximumRow > 0 ? viewport.top / maximumRow * range : 0;
    }
    this.synchronizedTop = this.rail.scrollTop;
    this.rail.setAttribute("aria-description", viewport.available
      ? `Row ${viewport.top} of ${maximumRow}${viewport.pending ? ", loading" : ""}` : "History unavailable");
    const markers = this.showMarkers && viewport.available
      ? this.terminal.markers.filter(marker => marker.buffer === viewport.buffer && marker.row !== null) : [];
    const signature = JSON.stringify([markers, viewport.totalRows]);
    if (signature === this.markerSignature) return;
    this.markerSignature = signature;
    const focusedId = this.ticks.contains(document.activeElement) && document.activeElement instanceof HTMLElement
      ? document.activeElement.dataset.marker : undefined;
    this.markerController.abort();
    this.markerController = new AbortController();
    this.ticks.replaceChildren(...markers.map(marker => {
      const tick = document.createElement("button");
      tick.type = "button";
      tick.className = "native-marker";
      tick.dataset.marker = marker.id;
      tick.dataset.source = marker.source;
      tick.dataset.error = String(marker.exitCode != null && marker.exitCode !== 0);
      tick.style.top = `${Math.min(100, marker.row! / Math.max(1, (viewport.totalRows ?? 1) - 1) * 100)}%`;
      if (marker.color) tick.style.backgroundColor = marker.color;
      tick.title = marker.label ?? `${marker.phase ?? "Bookmark"} · row ${marker.row}` +
        (marker.exitCode == null ? "" : ` · exit ${marker.exitCode}`);
      tick.setAttribute("aria-label", tick.title);
      tick.addEventListener("click", () => {
        void this.terminal.scrollToMarker(marker.id).catch(this.reportError);
      }, { signal: this.markerController.signal });
      return tick;
    }));
    if (focusedId && connected && viewport.available) {
      const replacement = [...this.ticks.querySelectorAll<HTMLButtonElement>("button")]
        .find(tick => tick.dataset.marker === focusedId);
      (replacement ?? this.rail).focus({ preventScroll: true });
    }
  }

  dispose() {
    if (this.disposed) return;
    this.disposed = true;
    this.controller.abort();
    this.markerController.abort();
    cancelAnimationFrame(this.frame);
    this.frame = 0;
    this.desired = undefined;
    this.element.remove();
  }
}
