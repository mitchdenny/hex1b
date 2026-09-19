/** Paint-only overrides; scrollbar geometry and hit targets are unchanged. */
export interface TerminalScrollbarPartAppearance {
  /**
   * A concrete CSS color supported by the browser's canvas, at most 256 characters.
   * Context-dependent colors, CSS variables, escapes and comments are not supported.
   * Omitted colors follow the current frame's theme.
   */
  readonly color?: string;
  /** Finite multiplier from 0 through 1, applied to frame and canvas opacity. */
  readonly opacity?: number;
}

/** Marker defaults; an individual marker's color takes precedence. */
export interface TerminalScrollbarMarkerAppearance extends TerminalScrollbarPartAppearance {
  /** Concrete CSS color for nonzero exit codes; otherwise the frame's error color. */
  readonly errorColor?: string;
}

/** Snapshotted when creating a default scrollbar renderer. */
export interface TerminalScrollbarAppearance {
  /** Track color and opacity. Default opacity: 0.35. */
  readonly track?: TerminalScrollbarPartAppearance;
  /** Capsule thumb color and opacity. Default opacity: 1. */
  readonly thumb?: TerminalScrollbarPartAppearance;
  /** Marker colors and opacity. Default opacity: 1. */
  readonly markers?: TerminalScrollbarMarkerAppearance;
}
