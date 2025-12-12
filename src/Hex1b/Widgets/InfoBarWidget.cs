using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// A section within an InfoBar, with text and optional styling.
/// </summary>
/// <param name="Text">The text content of the section.</param>
/// <param name="Foreground">Optional foreground color override.</param>
/// <param name="Background">Optional background color override.</param>
public sealed record InfoBarSection(
    string Text,
    Hex1bColor? Foreground = null,
    Hex1bColor? Background = null);

/// <summary>
/// A one-line status bar widget, typically placed at the bottom of the screen.
/// By default, renders with inverted colors from the theme.
/// </summary>
/// <param name="Sections">The sections to display in the info bar.</param>
/// <param name="InvertColors">Whether to invert foreground/background colors (default: true).</param>
public sealed record InfoBarWidget(
    IReadOnlyList<InfoBarSection> Sections,
    bool InvertColors = true) : Hex1bWidget;
