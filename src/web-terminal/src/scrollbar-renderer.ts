import type {
  TerminalScrollbarAppearance, TerminalScrollbarPartAppearance
} from "./scrollbar-appearance.js";
import type { TerminalScrollbarFrame, TerminalScrollbarRenderer } from "./scrollbar-types.js";

// Canvas accepts some environment-dependent colors (notably currentColor) as valid.
// Reject them before asking the browser to parse concrete CSS color syntax.
const contextualColor = /\b(?:var|env|attr|light-dark|currentcolor|inherit|initial|unset|revert|revert-layer|default|context-fill|context-stroke|accentcolor|accentcolortext|activetext|buttonborder|buttonface|buttontext|canvas|canvastext|field|fieldtext|graytext|highlight|highlighttext|linktext|mark|marktext|selecteditem|selecteditemtext|visitedtext|activeborder|activecaption|appworkspace|background|buttonhighlight|buttonshadow|captiontext|inactiveborder|inactivecaption|inactivecaptiontext|infobackground|infotext|menu|menutext|scrollbar|threeddarkshadow|threedface|threedhighlight|threedlightshadow|threedshadow|window|windowframe|windowtext)\b/i;

function assertObject(value: unknown, name: string): void {
  if (value === null || typeof value !== "object" ||
      Object.prototype.toString.call(value) !== "[object Object]")
    throw new TypeError(`${name} must be an options object`);
}

function snapshotAppearance(appearance: TerminalScrollbarAppearance = {}) {
  assertObject(appearance, "scrollbar.appearance");
  let validationContext: CanvasRenderingContext2D | OffscreenCanvasRenderingContext2D | null | undefined;
  const color = (value: unknown, name: string): string | undefined => {
    if (value === undefined) return undefined;
    if (typeof value !== "string" || value.length > 256 || !value.trim() ||
        /\\|\/\*|--/.test(value) || contextualColor.test(value))
      throw new TypeError(`${name} must be a concrete CSS color of 1-256 characters`);
    const result = value.trim();
    if (validationContext === undefined) {
      validationContext = typeof OffscreenCanvas !== "undefined"
        ? new OffscreenCanvas(1, 1).getContext("2d")
        : typeof document !== "undefined" ? document.createElement("canvas").getContext("2d") : null;
    }
    if (!validationContext)
      throw new TypeError(`${name} requires a browser canvas to validate CSS colors`);
    // Invalid assignments retain the old fillStyle. Two different sentinels also
    // accept valid colors that happen to equal either sentinel after serialization.
    validationContext.fillStyle = "#010203";
    validationContext.fillStyle = result;
    const first = validationContext.fillStyle;
    validationContext.fillStyle = "#040506";
    validationContext.fillStyle = result;
    if (first !== validationContext.fillStyle)
      throw new TypeError(`${name} must be a valid concrete CSS color`);
    return result;
  };
  const part = (value: TerminalScrollbarPartAppearance | undefined, fallback: number, name: string) => {
    if (value !== undefined) assertObject(value, name);
    const { color: partColor, opacity = fallback } = value ?? {};
    if (typeof opacity !== "number" || !Number.isFinite(opacity) || opacity < 0 || opacity > 1)
      throw new RangeError(`${name}.opacity must be a finite number between 0 and 1`);
    return Object.freeze({ color: color(partColor, `${name}.color`), opacity });
  };
  const { track, thumb, markers } = appearance;
  const markerPart = part(markers, 1, "scrollbar.appearance.markers");
  return Object.freeze({
    track: part(track, 0.35, "scrollbar.appearance.track"),
    thumb: part(thumb, 1, "scrollbar.appearance.thumb"),
    markers: Object.freeze({
      ...markerPart, errorColor: color(markers?.errorColor, "scrollbar.appearance.markers.errorColor")
    })
  });
}

/**
 * Creates a synchronous capsule scrollbar painter with snapshotted paint overrides.
 * Omitted colors are read from each frame. Invalid options throw at creation time.
 * Circle/capsule markers paint behind the thumb.
 * The focus outline uses the thumb color, is independent of part opacity, and is hidden during dragging.
 */
export function createDefaultScrollbarRenderer(
  appearance?: TerminalScrollbarAppearance
): TerminalScrollbarRenderer {
  const options = snapshotAppearance(appearance);
  return frame => {
    if (frame.opacity <= 0) return;
    const { context, track, thumb, colors } = frame;
    const alpha = context.globalAlpha * frame.opacity;
    context.save();
    try {
      context.globalAlpha = alpha * options.track.opacity;
      context.fillStyle = options.track.color ?? colors.track;
      if (track.width > 0 && track.height > 0)
        context.fillRect(track.left, track.top, track.width, track.height);

      context.globalAlpha = alpha * options.markers.opacity;
      for (const { marker, bounds, color } of frame.markers) {
        context.fillStyle = marker.exitCode != null && marker.exitCode !== 0
          ? options.markers.errorColor ?? color ?? colors.error : options.markers.color ?? color ?? colors.marker;
        // Keep the historical per-marker override and invalid-color fallback.
        if (marker.color) context.fillStyle = marker.color;
        if (bounds.width > 0 && bounds.height > 0) {
          context.beginPath();
          context.roundRect(bounds.left, bounds.top, bounds.width, bounds.height,
            Math.min(bounds.width, bounds.height) / 2);
          context.fill();
        }
      }

      if (thumb.width > 0 && thumb.height > 0) {
        const inset = Math.min(2, thumb.width / 4);
        const width = thumb.width - inset * 2;
        context.globalAlpha = alpha * options.thumb.opacity;
        context.fillStyle = options.thumb.color ?? colors.thumb;
        context.beginPath();
        context.roundRect(thumb.left + inset, thumb.top, width, thumb.height,
          Math.min(width, thumb.height) / 2);
        context.fill();
      }

      if (frame.interaction.focused && !frame.interaction.dragging && thumb.width > 0 && thumb.height > 0) {
        context.globalAlpha = alpha;
        context.strokeStyle = options.thumb.color ?? colors.thumb;
        const lineWidth = Math.min(1, thumb.width / 2, thumb.height / 2);
        const width = thumb.width - lineWidth, height = thumb.height - lineWidth;
        context.lineWidth = lineWidth;
        context.beginPath();
        context.roundRect(thumb.left + lineWidth / 2, thumb.top + lineWidth / 2,
          width, height, Math.min(width, height) / 2);
        context.stroke();
      }
    } finally {
      context.restore();
    }
  };
}

const defaultRenderer = createDefaultScrollbarRenderer();

/** Paints the built-in appearance without changing the caller's canvas drawing state. */
export function renderDefaultScrollbar(frame: TerminalScrollbarFrame): void {
  defaultRenderer(frame);
}
