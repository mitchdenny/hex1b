using Hex1b.Theming;

namespace Hex1b.Documents;

/// <summary>
/// Describes visual styling that can be applied to a range of text in the editor.
/// All properties are nullable — only non-null values override the editor's default styling.
/// </summary>
public record TextDecoration
{
    /// <summary>Foreground color for the decorated text.</summary>
    public Hex1bColor? Foreground { get; init; }

    /// <summary>Background color for the decorated text.</summary>
    public Hex1bColor? Background { get; init; }

    /// <summary>Underline style (Single, Double, Curly, Dotted, Dashed).</summary>
    public UnderlineStyle? UnderlineStyle { get; init; }

    /// <summary>Underline color (independent of foreground).</summary>
    public Hex1bColor? UnderlineColor { get; init; }

    /// <summary>Whether the text should be bold.</summary>
    public bool? Bold { get; init; }

    /// <summary>Whether the text should be italic.</summary>
    public bool? Italic { get; init; }

    /// <summary>
    /// Theme element to resolve foreground color from. Takes precedence over <see cref="Foreground"/>
    /// when both are specified, allowing theme-aware decoration.
    /// </summary>
    public Hex1bThemeElement<Hex1bColor>? ForegroundThemeElement { get; init; }

    /// <summary>
    /// Theme element to resolve background color from. Takes precedence over <see cref="Background"/>
    /// when both are specified.
    /// </summary>
    public Hex1bThemeElement<Hex1bColor>? BackgroundThemeElement { get; init; }

    /// <summary>
    /// Theme element to resolve underline color from. Takes precedence over <see cref="UnderlineColor"/>
    /// when both are specified.
    /// </summary>
    public Hex1bThemeElement<Hex1bColor>? UnderlineColorThemeElement { get; init; }

    /// <summary>
    /// Resolves the effective foreground color, preferring the theme element if set.
    /// </summary>
    public Hex1bColor? ResolveForeground(Hex1bTheme theme) =>
        ForegroundThemeElement is not null ? theme.Get(ForegroundThemeElement) : Foreground;

    /// <summary>
    /// Resolves the effective background color, preferring the theme element if set.
    /// </summary>
    public Hex1bColor? ResolveBackground(Hex1bTheme theme) =>
        BackgroundThemeElement is not null ? theme.Get(BackgroundThemeElement) : Background;

    /// <summary>
    /// Resolves the effective underline color, preferring the theme element if set.
    /// </summary>
    public Hex1bColor? ResolveUnderlineColor(Hex1bTheme theme) =>
        UnderlineColorThemeElement is not null ? theme.Get(UnderlineColorThemeElement) : UnderlineColor;
}
