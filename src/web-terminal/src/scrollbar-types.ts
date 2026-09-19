import type { TerminalBuffer, TerminalCommandMark, TerminalShellIntegrationPhase, TerminalViewport } from "./types.js";

/** Display coordinates are CSS pixels, relative to the mounted terminal element. */
export interface TerminalRectangle {
  readonly left: number;
  readonly top: number;
  readonly width: number;
  readonly height: number;
}
export interface TerminalInsets {
  readonly top: number;
  readonly right: number;
  readonly bottom: number;
  readonly left: number;
}
/** Nonnegative CSS pixels. Omitted edges are zero. */
export type TerminalPadding = number | Partial<TerminalInsets>;
/** Local presentation layout; changing it never changes the producer's cell metrics. */
export interface TerminalLayout {
  readonly width: number;
  readonly height: number;
  readonly content: TerminalRectangle;
  readonly scrollbar: TerminalRectangle | null;
  readonly padding: TerminalInsets;
  readonly cellWidth: number;
  readonly cellHeight: number;
}
/** A producer-backed position from a presented viewport. Registration rejects expired positions. */
export interface TerminalTextPosition {
  readonly generation: string;
  readonly rowId: string;
  /** Cell position, including the insertion boundary immediately after the last column. */
  readonly column: number;
}
/** A retained point of interest. Null row means unavailable, not row zero. */
export interface TerminalMarker {
  readonly id: string;
  readonly source: "command" | "custom";
  readonly buffer: TerminalBuffer;
  readonly row: number | null;
  readonly column: number;
  readonly phase?: TerminalShellIntegrationPhase;
  readonly exitCode?: number | null;
  /** Host-only text; never interpreted as HTML or sent to the producer. */
  readonly label?: string;
  /** Host-only CSS color used by the default renderer. */
  readonly color?: string;
}
export interface TerminalMarkerOptions {
  readonly position: TerminalTextPosition;
  readonly label?: string;
  readonly color?: string;
}
export interface TerminalScrollbarMarker {
  readonly marker: TerminalMarker;
  readonly bounds: TerminalRectangle;
  /** Resolved default paint color for this kind/outcome; marker.color overrides it. */
  readonly color?: string;
}
export interface TerminalScrollbarInteraction {
  readonly near: boolean;
  readonly hovered: boolean;
  readonly dragging: boolean;
  readonly focused: boolean;
  readonly lastActivityAt: number;
  readonly reducedMotion: boolean;
}
/** Immutable state for a synchronous host painter. The context already uses CSS pixels. */
export interface TerminalScrollbarFrame {
  readonly canvas: HTMLCanvasElement;
  readonly context: CanvasRenderingContext2D;
  readonly layout: TerminalLayout;
  readonly viewport: TerminalViewport;
  /** Locally requested scroll top while dragging or awaiting presentation, otherwise null. */
  readonly pendingTarget: number | null;
  readonly track: TerminalRectangle;
  readonly thumb: TerminalRectangle;
  readonly markers: readonly TerminalScrollbarMarker[];
  /** The marker under an idle pointer; null during a gesture or outside a marker. */
  readonly hoveredMarker: TerminalScrollbarMarker | null;
  readonly interaction: TerminalScrollbarInteraction;
  readonly now: number;
  readonly opacity: number;
  readonly colors: Readonly<{ track: string; thumb: string; marker: string; error: string }>;
}
/** Return true to request another animation frame (for example while a custom fade is running). */
export type TerminalScrollbarRenderer = (frame: TerminalScrollbarFrame) => boolean | void;
/** Snapshot for a non-interactive marker tooltip. Anchor coordinates are terminal-local CSS pixels. */
export interface TerminalScrollbarTooltipContext {
  readonly marker: TerminalMarker;
  readonly anchor: TerminalRectangle;
  readonly layout: TerminalLayout;
  readonly details: TerminalCommandMark | null;
  readonly loading: boolean;
  readonly error: string | null;
  /** Aborted when this rendering is replaced or hidden. Use it to clean up host-owned UI. */
  readonly signal: AbortSignal;
}
/** Return an HTML element to position automatically, or null to manage presentation yourself. */
export type TerminalScrollbarTooltipRenderer = (context: TerminalScrollbarTooltipContext) => HTMLElement | null;
export interface TerminalScrollbarOptions {
  /** Overlay auto-hides; beside reserves a gutter and stays visible while scrollable. */
  readonly placement?: "overlay" | "beside";
  /** Track width in CSS pixels. Default 12; allowed range 4-64. */
  readonly width?: number;
  /** Proximity distance in CSS pixels. Default 24. */
  readonly proximity?: number;
  /** Overlay painter inactivity delay in milliseconds. Default 900; ignored beside. */
  readonly hideDelay?: number;
  /** Overlay painter fade duration in milliseconds. Default 300; ignored beside. */
  readonly fadeDuration?: number;
  readonly markers?: boolean;
  readonly render?: TerminalScrollbarRenderer;
  /** Synchronous HTML tooltip renderer. Omitted uses the default; false disables tooltips. */
  readonly tooltip?: false | TerminalScrollbarTooltipRenderer;
}
export interface TerminalScrollbarConfiguration {
  readonly placement: "overlay" | "beside";
  readonly width: number;
  readonly proximity: number;
  readonly hideDelay: number;
  readonly fadeDuration: number;
  readonly markers: boolean;
  readonly render?: TerminalScrollbarRenderer;
  readonly tooltip?: false | TerminalScrollbarTooltipRenderer;
}
export type TerminalScrollbar = false | TerminalScrollbarOptions;

/** Internal RPC result; exported command detail values use the existing TerminalCommandMark type. */
export interface MarkerResult {
  requestId: number;
  success: boolean;
  error?: string;
  markerId?: string;
  details?: TerminalCommandMark;
}
