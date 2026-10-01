import type { FrameMetadata, TerminalCell } from "./wire-types.js";
import { isRecord } from "./validation.js";

/** Browser theme selection. System follows prefers-color-scheme. */
export type TerminalColorMode = "light" | "dark" | "system";

/** JSON-compatible terminal colors. Every color is an opaque #RRGGBB string. */
export interface TerminalPalette {
  readonly foreground: string;
  readonly background: string;
  /** Exactly 16 entries: black, red, green, yellow, blue, magenta, cyan, white, then their bright variants. */
  readonly ansi: readonly string[];
  readonly cursor?: string;
  /** Selected text color; defaults to the palette background. */
  readonly selectionForeground?: string;
  /** Opaque selection fill; defaults to the palette foreground. */
  readonly selectionBackground?: string;
  /** Optional overrides for indices 16 through 255; otherwise the xterm cube and gray ramp apply. */
  readonly extended?: Readonly<Record<number, string>>;
}

/** Hex1b Dark: muted Tomorrow Night Eighties-derived hues on neutral charcoal. */
export const defaultDarkPalette: TerminalPalette = Object.freeze({
  foreground: "#d4d0c8", background: "#323232",
  selectionForeground: "#323232", selectionBackground: "#d4d0c8",
  ansi: Object.freeze([
    "#242424", "#ed888f", "#8dae82", "#c3a150", "#7baad1", "#bc99c5", "#64b1b5", "#b7b4ae",
    "#9a9a9a", "#ff99a0", "#9bc08e", "#d7b156", "#87bbe7", "#d0a8da", "#6cc4c8", "#dedad3",
  ]),
});

/** Hex1b Light swaps the default neutrals and retunes the same hues for warm stone. */
export const defaultLightPalette: TerminalPalette = Object.freeze({
  foreground: "#323232", background: "#d4d0c8",
  selectionForeground: "#d4d0c8", selectionBackground: "#323232",
  ansi: Object.freeze([
    "#24262b", "#9e3141", "#3e6131", "#705402", "#255c86", "#704b7a", "#006367", "#b7b4ae",
    "#595959", "#982338", "#355a27", "#664d01", "#175480", "#694273", "#015a5e", "#e3dfd7",
  ]),
});

export function normalizeColorMode(value: unknown = "dark"): TerminalColorMode {
  if (value === "light" || value === "dark" || value === "system") return value;
  throw new TypeError('colorMode must be "light", "dark", or "system"');
}

function color(value: unknown, name: string): string {
  if (typeof value !== "string" || !/^#[0-9a-f]{6}$/iu.test(value))
    throw new TypeError(`${name} must be a #RRGGBB color`);
  return value.toLowerCase();
}

export function normalizePalette(value: unknown): TerminalPalette {
  if (!isRecord(value)) throw new TypeError("A terminal palette object is required");
  if (!Array.isArray(value.ansi) || value.ansi.length !== 16)
    throw new TypeError("palette.ansi must contain exactly 16 colors");
  const ansi = Array.from(value.ansi, (entry, index) => color(entry, `palette.ansi[${index}]`));
  const extended: Record<number, string> = {};
  if (value.extended !== undefined) {
    if (!isRecord(value.extended)) throw new TypeError("palette.extended must be an object");
    for (const [key, entry] of Object.entries(value.extended)) {
      const index = Number(key);
      if (!Number.isInteger(index) || index < 16 || index > 255 || String(index) !== key)
        throw new TypeError("palette.extended indices must be integers from 16 to 255");
      extended[index] = color(entry, `palette.extended[${key}]`);
    }
  }
  return Object.freeze({
    foreground: color(value.foreground, "palette.foreground"),
    background: color(value.background, "palette.background"),
    ansi: Object.freeze(ansi),
    ...(value.cursor === undefined ? {} : { cursor: color(value.cursor, "palette.cursor") }),
    ...(value.selectionForeground === undefined ? {} :
      { selectionForeground: color(value.selectionForeground, "palette.selectionForeground") }),
    ...(value.selectionBackground === undefined ? {} :
      { selectionBackground: color(value.selectionBackground, "palette.selectionBackground") }),
    ...(value.extended === undefined ? {} : { extended: Object.freeze(extended) }),
  });
}

export interface RenderPalette {
  foreground: number;
  background: number;
  selectionForeground: number;
  selectionBackground: number;
  cursor?: number;
  indexed: Uint32Array;
}

function pack(red: number, green: number, blue: number): number {
  return (red | green << 8 | blue << 16 | 0xff000000) >>> 0;
}

function packHex(value: string): number {
  const rgb = Number.parseInt(value.slice(1), 16);
  return pack(rgb >> 16, (rgb >> 8) & 255, rgb & 255);
}

export function compilePalette(palette: TerminalPalette): RenderPalette {
  const indexed = new Uint32Array(256);
  for (let i = 0; i < 16; i++) indexed[i] = packHex(palette.ansi[i]);
  const levels = [0, 95, 135, 175, 215, 255];
  for (let i = 16; i < 232; i++) {
    const n = i - 16;
    indexed[i] = pack(levels[Math.floor(n / 36)], levels[Math.floor(n / 6) % 6], levels[n % 6]);
  }
  for (let i = 232; i < 256; i++) {
    const gray = 8 + (i - 232) * 10;
    indexed[i] = pack(gray, gray, gray);
  }
  for (const [index, value] of Object.entries(palette.extended ?? {})) indexed[Number(index)] = packHex(value);
  return { foreground: packHex(palette.foreground), background: packHex(palette.background),
    selectionForeground: packHex(palette.selectionForeground ?? palette.background),
    selectionBackground: packHex(palette.selectionBackground ?? palette.foreground),
    ...(palette.cursor === undefined ? {} : { cursor: packHex(palette.cursor) }), indexed };
}

export function validateColorReference(value: number, underline = false): void {
  if ((value >>> 24) === 255 || (value >= 0x01000000 && value <= 0x010000ff) ||
      value === 0x02000000 || value === 0x03000000 || (underline && value === 0x04000000)) return;
  throw new Error("Invalid indexed-v1 color reference");
}

function resolve(value: number, palette: RenderPalette): number {
  switch (value >>> 24) {
    case 1: return palette.indexed[value & 255];
    case 2: return palette.foreground;
    case 3: return palette.background;
    default: return value;
  }
}

export function foregroundColor(cell: TerminalCell, metadata: FrameMetadata, palette: RenderPalette): number {
  if (metadata.colorEncoding !== "indexed-v1") return cell.foreground;
  const value = resolve(cell.attributes & 32 ? cell.background : cell.foreground, palette);
  return cell.attributes & 2
    ? (((value & 255) >> 1) | ((((value >>> 8) & 255) >> 1) << 8) |
      ((((value >>> 16) & 255) >> 1) << 16) | 0xff000000) >>> 0
    : value;
}

export function backgroundColor(cell: TerminalCell, metadata: FrameMetadata, palette: RenderPalette): number {
  if (metadata.colorEncoding !== "indexed-v1") return cell.background;
  if (!(cell.attributes & 32) && cell.background === 0x03000000) return palette.background & 0x00ffffff;
  return resolve(cell.attributes & 32 ? cell.foreground : cell.background, palette);
}

export function underlineColor(cell: TerminalCell, metadata: FrameMetadata, palette: RenderPalette): number {
  if (metadata.colorEncoding !== "indexed-v1") return cell.underlineColor;
  return cell.underlineColor === 0x04000000
    ? foregroundColor(cell, metadata, palette) : resolve(cell.underlineColor, palette);
}
