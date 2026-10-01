export { WebTerminal } from "./web-terminal.js";
export { createWebSocketTransport } from "./websocket-transport.js";
export { defaultLightPalette, defaultDarkPalette } from "./terminal-palette.js";
export { InputRoute, TerminalAction, defaultInputBindings } from "./input-policy.js";
export { MIN_FONT_SIZE, MAX_FONT_SIZE } from "./terminal-sizing.js";
export { parseCommandMarkParameters, getCmdlineUrl } from "./command-mark.js";
export { linkAction } from "./link-options.js";
export { createDefaultScrollbarRenderer, renderDefaultScrollbar } from "./scrollbar-renderer.js";
export { renderDefaultScrollbarTooltip } from "./scrollbar-tooltip.js";
export type * from "./types.js";

// Keep worker globals out of the public declarations' ambient requirements.
declare const DedicatedWorkerGlobalScope: { new(): object };

if (typeof DedicatedWorkerGlobalScope !== "undefined" && globalThis instanceof DedicatedWorkerGlobalScope) {
  switch (new URL(import.meta.url).hash) {
    case "#hex1b-terminal-worker":
      await import("./terminal-worker.js");
      break;
    case "#hex1b-link-detection-worker":
      await import("./link-detection-worker.js");
      break;
  }
}
