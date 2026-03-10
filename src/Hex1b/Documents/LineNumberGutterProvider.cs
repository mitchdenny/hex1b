using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b.Documents;

/// <summary>
/// Built-in gutter provider that renders 1-based line numbers.
/// Uses <see cref="GutterTheme"/> for colors and separator character.
/// </summary>
public sealed class LineNumberGutterProvider : IGutterProvider
{
    /// <summary>Shared singleton instance.</summary>
    public static LineNumberGutterProvider Instance { get; } = new();

    /// <inheritdoc />
    public int GetWidth(IHex1bDocument document)
    {
        var totalLines = document.LineCount;
        var digitCount = Math.Max(2, (int)Math.Floor(Math.Log10(Math.Max(1, totalLines))) + 1);
        return digitCount + 1; // digits + 1 char separator
    }

    /// <inheritdoc />
    public void RenderLine(Hex1bRenderContext context, Hex1bTheme theme, int screenX, int screenY, int docLine, int width)
    {
        var fg = theme.Get(GutterTheme.LineNumberForegroundColor);
        var bg = theme.Get(GutterTheme.BackgroundColor);
        if (bg.IsDefault) bg = theme.Get(EditorTheme.BackgroundColor);
        var sep = theme.Get(GutterTheme.SeparatorCharacter);

        var digitCount = width - 1;
        string gutterText;

        if (docLine > 0)
            gutterText = docLine.ToString().PadLeft(digitCount) + sep;
        else
            gutterText = new string(' ', width);

        context.WriteClipped(screenX, screenY,
            $"{fg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{gutterText}");
    }
}
