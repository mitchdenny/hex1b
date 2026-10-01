import { fittedScale } from "./terminal-sizing.js";
import { isRecord } from "./validation.js";
import type { TerminalGeometry, TerminalSize, TerminalSizingState } from "./types.js";
import type { TerminalInsets, TerminalLayout, TerminalPadding, TerminalScrollbarConfiguration } from "./scrollbar-types.js";

export function normalizePadding(value: TerminalPadding = 0): TerminalInsets {
  if (typeof value !== "number" && !isRecord(value)) throw new TypeError("Padding must be a number or per-edge object");
  const edge = (name: keyof TerminalInsets) => {
    const amount = typeof value === "number" ? value : value[name] === undefined ? 0 : value[name];
    if (typeof amount !== "number" || !Number.isFinite(amount) || amount < 0)
      throw new RangeError(`Padding ${name} must be finite nonnegative CSS pixels`);
    return amount;
  };
  return Object.freeze({ top: edge("top"), right: edge("right"), bottom: edge("bottom"), left: edge("left") });
}

export function contentSpace(size: TerminalSize, padding: TerminalInsets,
  scrollbar: false | TerminalScrollbarConfiguration): TerminalSize {
  return {
    width: Math.max(0, size.width - padding.left - padding.right - (scrollbar && scrollbar.placement === "beside" ? scrollbar.width : 0)),
    height: Math.max(0, size.height - padding.top - padding.bottom)
  };
}

export function terminalLayout(size: TerminalSize, geometry: TerminalGeometry, primary: boolean,
  sizing: TerminalSizingState, padding: TerminalInsets, scrollbar: false | TerminalScrollbarConfiguration): TerminalLayout {
  const available = contentSpace(size, padding, scrollbar);
  const scale = fittedScale(available, geometry, primary, sizing);
  const width = geometry.columns * geometry.cellWidth * scale;
  const height = geometry.rows * geometry.cellHeight * scale;
  const left = Math.min(size.width, padding.left) + (available.width - width) / 2;
  const top = Math.min(size.height, padding.top) + (available.height - height) / 2;
  const trackWidth = scrollbar ? Math.min(scrollbar.width,
    scrollbar.placement === "overlay" ? width : Math.max(0, size.width - padding.right - left - width)) : 0;
  return Object.freeze({
    width: size.width, height: size.height,
    content: Object.freeze({ left, top, width, height }),
    scrollbar: scrollbar ? Object.freeze({
      left: left + width - (scrollbar.placement === "overlay" ? trackWidth : 0),
      top, width: trackWidth, height
    }) : null,
    padding,
    cellWidth: width / geometry.columns,
    cellHeight: height / geometry.rows
  });
}
