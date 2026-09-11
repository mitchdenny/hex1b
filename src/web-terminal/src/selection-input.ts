import type { MouseTrackingMode, SelectionMode, TerminalPoint, TerminalSelection } from "./types.js";
import type { CellPosition } from "./wire-types.js";

export interface GestureState {
  tracking: MouseTrackingMode;
  historical?: boolean;
  readOnly?: boolean;
  selection?: Pick<TerminalSelection, "status" | "mode" | "canExtend">;
}
export type GestureStart = { owner: "app" } | { owner: "local"; mode: SelectionMode; extend: boolean };
type PointEvent = Pick<MouseEvent, "clientX" | "clientY" | "shiftKey" | "altKey" | "ctrlKey">;

export function cellPoint(event: PointEvent, bounds: Pick<DOMRect, "left" | "top" | "width" | "height">,
  columns: number, rows: number, clamp = false): CellPosition | null {
  if (!(bounds.width > 0) || !(bounds.height > 0)) return null;
  const x = Math.floor((event.clientX - bounds.left) * columns / bounds.width);
  const y = Math.floor((event.clientY - bounds.top) * rows / bounds.height);
  if (!clamp && (x < 0 || y < 0 || x >= columns || y >= rows)) return null;
  return {
    x: Math.max(0, Math.min(columns - 1, x)),
    y: Math.max(0, Math.min(rows - 1, y)),
    shift: !!event.shiftKey, alt: !!event.altKey, ctrl: !!event.ctrlKey
  };
}

export class WheelAccumulator {
  x = 0;
  y = 0;
  reset() { this.x = this.y = 0; }
  take(event: Pick<WheelEvent, "deltaMode" | "deltaX" | "deltaY">, bounds: Pick<DOMRect, "height">, rows: number): TerminalPoint {
    const cellHeight = bounds.height / rows;
    if (!(cellHeight > 0)) return { x: 0, y: 0 };
    const unit = event.deltaMode === 1 ? 1 : event.deltaMode === 2 ? rows : 1 / cellHeight;
    this.x += event.deltaX * unit;
    this.y += event.deltaY * unit;
    const x = Math.trunc(this.x);
    const y = Math.trunc(this.y);
    this.x -= x;
    this.y -= y;
    return { x: Math.max(-32, Math.min(32, x)), y: Math.max(-32, Math.min(32, y)) };
  }
}

/** Ownership and granularity are latched, independent of subsequent modifiers. */
export class SelectionGesture {
  owner: "local" | "app" | null = null;
  mode: SelectionMode = "character";
  endpoint: TerminalPoint | null = null;
  begin(event: Pick<MouseEvent, "button" | "shiftKey" | "altKey" | "detail">, point: TerminalPoint,
    { tracking, historical, readOnly, selection }: GestureState): GestureStart | null {
    if (this.owner) return null;
    const local = historical || readOnly || !tracking || event.shiftKey;
    if (local && event.button !== 0) return null;
    this.owner = local ? "local" : "app";
    if (!local) return { owner: "app" };
    const extend = !!event.shiftKey && !!selection && ["valid", "pending"].includes(selection.status) && selection.canExtend !== false;
    this.mode = extend && selection ? selection.mode : event.altKey ? "rectangle"
      : event.detail >= 3 ? "line" : event.detail === 2 ? "word" : "character";
    this.endpoint = { x: point.x, y: point.y };
    return { owner: this.owner, mode: this.mode, extend };
  }
  move(point: TerminalPoint): TerminalPoint | null {
    if (this.owner !== "local") return null;
    this.endpoint = { x: point.x, y: point.y };
    return { ...this.endpoint };
  }
  scrollPoint(point: TerminalPoint): TerminalPoint | undefined {
    if (this.owner !== "local") return undefined;
    // A wheel cannot widen a rectangle; only actual pointer movement can.
    return { x: this.mode === "rectangle" && this.endpoint ? this.endpoint.x : point.x, y: point.y };
  }
  wheelOwner(event: Pick<MouseEvent, "shiftKey">, { tracking, historical, readOnly }: GestureState): "local" | "app" {
    return this.owner ?? (historical || readOnly || !tracking || event.shiftKey ? "local" : "app");
  }
  end() { this.owner = null; }
}
