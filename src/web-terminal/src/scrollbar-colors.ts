import type { TerminalMarker } from "./scrollbar-types.js";

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
