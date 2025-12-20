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
