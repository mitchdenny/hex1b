using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;

namespace Hex1b;

/// <summary>
/// Gutter provider that renders folding indicators (▼/▶) for collapsible regions.
/// Clicking a folding indicator toggles the collapsed state of that region.
/// </summary>
public sealed class FoldingGutterProvider : IGutterProvider
{
    private IReadOnlyList<FoldingRegion> _regions = [];
    private Action<int>? _toggleCallback;

    /// <summary>
    /// Updates the folding regions to display. Called by EditorNode when regions change.
    /// </summary>
    internal void SetRegions(IReadOnlyList<FoldingRegion> regions, Action<int> toggleCallback)
    {
        _regions = regions;
        _toggleCallback = toggleCallback;
    }

    /// <inheritdoc />
    public int GetWidth(IHex1bDocument document) => _regions.Count > 0 ? 2 : 0;

    /// <inheritdoc />
    public void RenderLine(Hex1bRenderContext context, Hex1bTheme theme, int screenX, int screenY, int docLine, int width)
    {
        if (width == 0 || docLine <= 0) return;

        var fg = theme.Get(GutterTheme.LineNumberForegroundColor);
        var bg = theme.Get(GutterTheme.BackgroundColor);
        if (bg.IsDefault) bg = theme.Get(EditorTheme.BackgroundColor);

        // Check if this line starts a folding region
        for (var i = 0; i < _regions.Count; i++)
        {
            var region = _regions[i];
            if (region.StartLine == docLine)
            {
                var indicator = region.IsCollapsed
                    ? theme.Get(FoldingTheme.CollapsedIndicator)
                    : theme.Get(FoldingTheme.ExpandedIndicator);
                var indicatorFg = theme.Get(FoldingTheme.PlaceholderForegroundColor);
                context.WriteClipped(screenX, screenY,
                    $"{indicatorFg.ToForegroundAnsi()}{bg.ToBackgroundAnsi()}{indicator} ");
                return;
            }
        }

        // No region starts here — render blank
        context.WriteClipped(screenX, screenY,
            $"{bg.ToBackgroundAnsi()}  ");
    }

    /// <inheritdoc />
    public bool HandleClick(int docLine)
    {
        for (var i = 0; i < _regions.Count; i++)
        {
            if (_regions[i].StartLine == docLine)
            {
                _toggleCallback?.Invoke(i);
                return true;
            }
        }
        return false;
    }
}
