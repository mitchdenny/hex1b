using Hex1b.Theming;

namespace Hex1b.Documents;

/// <summary>
/// Internal decoration provider that converts <see cref="RangeHighlight"/> entries
/// into <see cref="TextDecorationSpan"/> entries for the rendering pipeline.
/// Uses a low priority so that syntax highlighting and diagnostics take precedence.
/// </summary>
internal sealed class RangeHighlightDecorationProvider : ITextDecorationProvider
{
    private IReadOnlyList<RangeHighlight> _highlights = [];
    private Hex1bTheme? _theme;

    /// <summary>
    /// Updates the current set of range highlights.
    /// </summary>
    public void SetHighlights(IReadOnlyList<RangeHighlight> highlights, Hex1bTheme theme)
    {
        _highlights = highlights;
        _theme = theme;
    }

    /// <summary>
    /// Clears all range highlights.
    /// </summary>
    public void Clear()
    {
        _highlights = [];
    }

    /// <inheritdoc />
    public IReadOnlyList<TextDecorationSpan> GetDecorations(int startLine, int endLine, IHex1bDocument document)
    {
        if (_highlights.Count == 0 || _theme == null) return [];

        var result = new List<TextDecorationSpan>();
        foreach (var highlight in _highlights)
        {
            if (highlight.End.Line < startLine || highlight.Start.Line > endLine)
                continue;

            var bg = highlight.Background ?? ResolveKindBackground(highlight.Kind, _theme);
            result.Add(new TextDecorationSpan(
                highlight.Start,
                highlight.End,
                new TextDecoration { Background = bg },
                Priority: -100)); // Low priority so other decorations take precedence
        }

        return result;
    }

    private static Hex1bColor ResolveKindBackground(RangeHighlightKind kind, Hex1bTheme theme) => kind switch
    {
        RangeHighlightKind.ReadAccess => theme.Get(RangeHighlightTheme.ReadAccessBackground),
        RangeHighlightKind.WriteAccess => theme.Get(RangeHighlightTheme.WriteAccessBackground),
        _ => theme.Get(RangeHighlightTheme.DefaultBackground)
    };
}
