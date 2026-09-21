import type { WebTerminal, WebTerminalOptions } from "@hex1b/web-terminal";
import { palettePresets } from "./palettes.js";

type ColorMode = NonNullable<WebTerminalOptions["colorMode"]>;
const systemTheme = window.matchMedia("(prefers-color-scheme: dark)");
const requestedTheme = new URLSearchParams(location.search).get("scoutTheme");
let colorMode: ColorMode = requestedTheme === "light" || requestedTheme === "dark" ? requestedTheme : "system";
const selectedPalettes = { light: "default-light", dark: "default-dark" };
const terminals = new Set<WebTerminal>();

export function terminalAppearance() {
  return {
    colorMode,
    lightModePalette: palettePresets[selectedPalettes.light].palette,
    darkModePalette: palettePresets[selectedPalettes.dark].palette
  };
}

export function forgetTerminalAppearance(terminal: WebTerminal | undefined) {
  if (terminal) terminals.delete(terminal);
}

export function followTerminalAppearance(terminal: WebTerminal, signal: AbortSignal) {
  if (signal.aborted) return;
  // A mode/preset may have changed while this terminal was mounting.
  terminal.setPalette("light", palettePresets[selectedPalettes.light].palette);
  terminal.setPalette("dark", palettePresets[selectedPalettes.dark].palette);
  terminal.setColorMode(colorMode);
  terminals.add(terminal);
  signal.addEventListener("abort", () => terminals.delete(terminal), { once: true });
}

export function initializeAppearanceControls() {
  const mode = document.querySelector<HTMLSelectElement>("#color-mode")!;
  const swatches = document.querySelector<HTMLElement>("#palette-swatches")!;
  const updateChrome = () => {
    const resolved = colorMode === "system" ? systemTheme.matches ? "dark" : "light" : colorMode;
    document.documentElement.dataset.theme = resolved;
    const preset = palettePresets[selectedPalettes[resolved]];
    swatches.style.background = preset.palette.background;
    swatches.style.color = preset.palette.foreground;
    swatches.replaceChildren();
    const label = document.createElement("span");
    label.textContent = `${resolved === "light" ? "Light" : "Dark"} ANSI 0–15`;
    swatches.append(label);
    preset.palette.ansi.forEach((color, index) => {
      const chip = document.createElement("span");
      chip.className = "palette-swatch";
      chip.style.background = color;
      chip.title = `ANSI ${index}: ${color}`;
      chip.setAttribute("aria-label", chip.title);
      swatches.append(chip);
    });
  };
  mode.value = colorMode;
  mode.addEventListener("change", () => {
    const next = mode.value;
    if (next !== "light" && next !== "dark" && next !== "system") return;
    colorMode = next;
    for (const terminal of terminals) terminal.setColorMode(colorMode);
    updateChrome();
  });
  for (const paletteMode of ["light", "dark"] as const) {
    const picker = document.querySelector<HTMLSelectElement>(`#${paletteMode}-palette`)!;
    picker.replaceChildren(...Object.entries(palettePresets).map(([id, preset]) => new Option(preset.label, id)));
    picker.value = selectedPalettes[paletteMode];
    picker.addEventListener("change", () => {
      const preset = palettePresets[picker.value];
      if (!preset) return;
      selectedPalettes[paletteMode] = picker.value;
      for (const terminal of terminals) terminal.setPalette(paletteMode, preset.palette);
      updateChrome();
    });
  }
  systemTheme.addEventListener("change", updateChrome);
  updateChrome();
}
