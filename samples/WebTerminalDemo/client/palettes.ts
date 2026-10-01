import { defaultDarkPalette, defaultLightPalette, type TerminalPalette } from "@hex1b/web-terminal";

// Consumer-owned, JSON-compatible data; these preset names are not library API.
export const palettePresets: Readonly<Record<string, { label: string; palette: TerminalPalette }>> = {
  "default-light": { label: "Hex1b Light (default)", palette: defaultLightPalette },
  "default-dark": { label: "Hex1b Dark (default)", palette: defaultDarkPalette },
  "ghostty-dark": {
    label: "Ghostty-compatible dark",
    palette: {
      foreground: "#ffffff", background: "#282c34", cursor: "#ffffff", selectionBackground: "#3e4451",
      selectionForeground: "#ffffff",
      ansi: ["#1d1f21", "#cc6666", "#b5bd68", "#f0c674", "#81a2be", "#b294bb", "#8abeb7", "#c5c8c6",
        "#666666", "#d54e53", "#b9ca4a", "#e7c547", "#7aa6da", "#c397d8", "#70c0b1", "#eaeaea"]
    }
  },
  "campbell-dark": {
    label: "Campbell dark",
    palette: {
      foreground: "#cccccc", background: "#0c0c0c", cursor: "#ffffff", selectionBackground: "#3b3b3b",
      selectionForeground: "#cccccc",
      ansi: ["#0c0c0c", "#c50f1f", "#13a10e", "#c19c00", "#0037da", "#881798", "#3a96dd", "#cccccc",
        "#767676", "#e74856", "#16c60c", "#f9f1a5", "#3b78ff", "#b4009e", "#61d6d6", "#f2f2f2"]
    }
  },
  "fluent-light": {
    label: "Fluent-inspired light (experimental)",
    palette: {
      foreground: "#242424", background: "#ffffff", cursor: "#0f6cbd", selectionBackground: "#cfe4fa",
      selectionForeground: "#242424",
      ansi: ["#242424", "#b10e1c", "#107c10", "#835c00", "#0f6cbd", "#881798", "#006b73", "#d1d1d1",
        "#616161", "#c50f1f", "#0b6a0b", "#986f0b", "#115ea3", "#5c2d91", "#038387", "#fafafa"]
    }
  },
  "fluent-dark": {
    label: "Fluent-inspired dark (experimental)",
    palette: {
      foreground: "#f5f5f5", background: "#1f1f1f", cursor: "#479ef5", selectionBackground: "#294b70",
      selectionForeground: "#f5f5f5",
      ansi: ["#292929", "#f1707b", "#54b054", "#eaa300", "#479ef5", "#c586c0", "#56c5c9", "#d1d1d1",
        "#808080", "#f7a0a8", "#92c353", "#fce100", "#96c6fa", "#d7a9e3", "#9adbdc", "#ffffff"]
    }
  },
};
