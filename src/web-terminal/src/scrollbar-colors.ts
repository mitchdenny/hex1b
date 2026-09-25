import type { TerminalMarker } from "./scrollbar-types.js";
import type { TerminalPalette } from "./terminal-palette.js";

// Fallbacks for standalone controllers without a mounted terminal's palette tokens.
export const scrollbarColors = Object.freeze({
  track: "#202020",
  thumb: "#999999",
  marker: "#aaaaaa",
  error: "#eeeeee",
  prompt: "#666666",
  "command-line": "#888888",
  executing: "#bbbbbb",
  success: "#999999",
  custom: "#dddddd"
});

export function paletteScrollbarColors(palette: TerminalPalette) {
  const foreground = [1, 3, 5].map(offset => parseInt(palette.foreground.slice(offset, offset + 2), 16));
  const background = [1, 3, 5].map(offset => parseInt(palette.background.slice(offset, offset + 2), 16));
  const shade = (weight: number) => "#" + foreground.map((channel, index) =>
    Math.round(channel * weight + background[index]! * (1 - weight)).toString(16).padStart(2, "0")).join("");
  return Object.freeze({
    track: shade(0.5),
    thumb: palette.foreground,
    marker: shade(0.78),
    error: palette.foreground,
    prompt: shade(0.45),
    "command-line": shade(0.6),
    executing: shade(0.85),
    success: shade(0.7),
    custom: shade(0.95)
  });
}

export function scrollbarMarkerColor(
  marker: TerminalMarker, resolve: (name: keyof typeof scrollbarColors) => string
): string {
  if (marker.source === "custom") return resolve("custom");
  if (marker.exitCode != null && marker.exitCode !== 0) return resolve("error");
  switch (marker.phase) {
    case "prompt": return resolve("prompt");
    case "commandLine": return resolve("command-line");
    case "executing": return resolve("executing");
    case "finished": return resolve(marker.exitCode === 0 ? "success" : "marker");
    default: return resolve("marker");
  }
}
