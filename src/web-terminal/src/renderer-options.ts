import type { TerminalRendererPreference } from "./types.js";

export function normalizeRenderer(value: unknown = "auto"): TerminalRendererPreference {
  if (value === "auto" || value === "webgpu" || value === "webgl2") return value;
  throw new TypeError('renderer must be "auto", "webgpu", or "webgl2"');
}
