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
 * Circular markers move aside near the thumb and paint behind it.
 * One smooth contour surrounds nearby displaced markers and tapers back into the track.
 * Track ends are rounded; focus does not add an outline.
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
      if (track.width > 0 && track.height > 0) {
        const step = 2, padding = 6, taper = 12;
        const radius = Math.min(track.width, track.height) / 2;
        const right = track.left + track.width, bottom = track.top + track.height;
        let offsets: Float64Array | undefined;
        let left = track.left;
        for (const { bounds } of frame.markers) {
          if (bounds.width <= 0 || bounds.height <= 0 || bounds.left >= track.left ||
            bounds.top >= track.top + track.height || bounds.top + bounds.height <= track.top) continue;
          const buffer = padding * Math.min(1, (track.left - bounds.left) / bounds.width);
          const shadowLeft = Math.max(0, bounds.left - buffer);
          if (shadowLeft >= track.left) continue;
          left = Math.min(left, shadowLeft);
          offsets ??= new Float64Array(Math.ceil(track.height / step) + 1);
          const first = Math.max(0, Math.floor((bounds.top - padding - taper - track.top) / step));
          const last = Math.min(offsets.length - 1,
            Math.ceil((bounds.top + bounds.height + padding + taper - track.top) / step));
          for (let i = first; i <= last; i++) {
            const y = track.top + Math.min(i * step, track.height);
            const distance = Math.max(bounds.top - padding - y, y - bounds.top - bounds.height - padding, 0);
            const proximity = Math.max(0, 1 - distance / taper);
            const extent = (track.left - shadowLeft) * proximity * proximity * (3 - 2 * proximity);
            // A shared envelope bridges nearby marks without density-dependent darkening or scallops.
            offsets[i] = Math.max(offsets[i]!, extent);
          }
        }
        if (offsets) {
          const gradient = context.createLinearGradient(left, 0, track.left, 0);
          gradient.addColorStop(0, "transparent");
          gradient.addColorStop(1, options.track.color ?? colors.track);
          context.fillStyle = gradient;
          const topLeft = track.left - offsets[0]!;
          const bottomLeft = track.left - offsets[offsets.length - 1]!;
          // Round an extended shadow within its padding, without cutting into floated marks.
          const topRadius = topLeft < track.left ? Math.min(radius, padding) : radius;
          const bottomRadius = bottomLeft < track.left ? Math.min(radius, padding) : radius;
          context.beginPath();
          context.moveTo(right - radius, track.top);
          context.lineTo(topLeft + topRadius, track.top);
          context.quadraticCurveTo(topLeft, track.top, topLeft, track.top + topRadius);
          let straight = false;
          for (let i = 1; i < offsets.length - 1; i++) {
            const y = track.top + i * step;
            const nextY = track.top + Math.min((i + 1) * step, track.height);
            if (y - step / 2 <= track.top + topRadius || (y + nextY) / 2 >= bottom - bottomRadius) continue;
            if (offsets[i - 1] === offsets[i] && offsets[i] === offsets[i + 1]) {
              straight = true;
              continue;
            }
            const x = track.left - offsets[i]!, nextX = track.left - offsets[i + 1]!;
            if (straight) {
              context.lineTo((track.left - offsets[i - 1]! + x) / 2, y - step / 2);
              straight = false;
            }
            context.quadraticCurveTo(x, y, (x + nextX) / 2, (y + nextY) / 2);
          }
          context.lineTo(bottomLeft, bottom - bottomRadius);
          context.quadraticCurveTo(bottomLeft, bottom, bottomLeft + bottomRadius, bottom);
          context.lineTo(right - radius, bottom);
          context.quadraticCurveTo(right, bottom, right, bottom - radius);
          context.lineTo(right, track.top + radius);
          context.quadraticCurveTo(right, track.top, right - radius, track.top);
          context.closePath();
          context.fill();
        } else {
          context.beginPath();
          context.roundRect(track.left, track.top, track.width, track.height, radius);
          context.fill();
        }
      }
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
