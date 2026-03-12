using Hex1b.Events;
using Hex1b.Layout;
using Hex1b.Markdown;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Render node for <see cref="MarkdownTextBlockWidget"/>. Measures and renders
/// inline markdown content as ANSI-styled, word-wrapped text. When
/// <see cref="FocusableLinks"/> is <c>true</c>, creates child
/// <see cref="MarkdownLinkRegionNode"/> instances that participate in focus
/// navigation and link activation.
/// </summary>
internal sealed class MarkdownTextBlockNode : Hex1bNode
{
    private List<string>? _wrappedLines;
    private WrapResult? _cachedWrapResult;
    private int _lastWrapWidth = -1;
    private IReadOnlyList<MarkdownInline>? _lastInlines;
    private Hex1bColor? _lastBaseForeground;
    private CellAttributes _lastBaseAttributes;
    private int _lastFocusedLinkId = -1;
    private int _lastHangingIndent;
    private string? _lastContinuationPrefix;
    private MarkdownColors _lastColors = MarkdownColors.Default;

    // Resolved theme colors (set during Render from context.Theme)
    private MarkdownColors _resolvedColors = MarkdownColors.Default;

    // Link region child nodes (created when FocusableLinks is true)
    private List<MarkdownLinkRegionNode> _linkRegionNodes = [];

    /// <summary>
    /// The inline AST elements to render.
    /// </summary>
    public IReadOnlyList<MarkdownInline> Inlines { get; set; } = [];

    /// <summary>
    /// Optional base foreground color.
    /// </summary>
    public Hex1bColor? BaseForeground { get; set; }

    /// <summary>
    /// Optional base attributes.
    /// </summary>
    public CellAttributes BaseAttributes { get; set; }

    /// <summary>
    /// When <c>true</c>, links become focusable child nodes.
    /// </summary>
    public bool FocusableLinks { get; set; }

    /// <summary>
    /// Handler invoked when a link is activated.
    /// </summary>
    public Func<MarkdownLinkActivatedEventArgs, Task>? LinkActivatedHandler { get; set; }

    /// <summary>
    /// The source markdown widget (for building event args).
    /// </summary>
    public MarkdownWidget? SourceWidget { get; set; }

    /// <summary>
    /// Number of columns to indent continuation lines (for list items).
    /// </summary>
    public int HangingIndent { get; set; }

    /// <summary>
    /// Optional prefix string to prepend on continuation lines instead of spaces.
    /// When set, continuation lines use this prefix (e.g., "│ " for block quotes)
    /// instead of <c>new string(' ', HangingIndent)</c>.
    /// </summary>
    public string? ContinuationPrefix { get; set; }

    /// <summary>
    /// Optional anchor identifier for this node (set on heading nodes for
    /// intra-document link navigation).
    /// </summary>
    public string? AnchorId { get; set; }

    public override bool IsFocusable => false;

    protected override Size MeasureCore(Constraints constraints)
    {
        var maxWidth = constraints.MaxWidth;
        if (maxWidth <= 0)
            return constraints.Constrain(new Size(0, 0));

        var wrapResult = GetWrapResult(maxWidth);
        var lines = wrapResult.Lines;
        var height = lines.Count;

        // Width is the max display width of any line
        var width = 0;
        foreach (var line in lines)
        {
            var lineWidth = DisplayWidth.GetStringWidth(line);
            if (lineWidth > width)
                width = lineWidth;
        }

        // Update link region positions from wrap result
        if (FocusableLinks && _linkRegionNodes.Count > 0)
        {
            UpdateLinkRegionPositions(wrapResult.LinkRegions);
        }

        return constraints.Constrain(new Size(width, height));
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);

        // Position link region nodes based on text block position + offsets
        if (FocusableLinks)
        {
            foreach (var linkRegion in _linkRegionNodes)
            {
                var linkBounds = new Rect(
                    bounds.X + linkRegion.ColumnOffset,
                    bounds.Y + linkRegion.LineIndex,
                    Math.Min(linkRegion.LinkDisplayWidth, bounds.Width - linkRegion.ColumnOffset),
                    1);
                linkRegion.Arrange(linkBounds);
            }
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        _resolvedColors = new MarkdownColors(
            LinkForeground: context.Theme.Get(MarkdownTheme.LinkForegroundColor),
            InlineCodeForeground: context.Theme.Get(MarkdownTheme.InlineCodeForegroundColor),
            InlineCodeBackground: context.Theme.Get(MarkdownTheme.InlineCodeBackgroundColor),
            FocusedLinkForeground: context.Theme.Get(MarkdownTheme.FocusedLinkForegroundColor),
            FocusedLinkBackground: context.Theme.Get(MarkdownTheme.FocusedLinkBackgroundColor));

        var wrapResult = GetWrapResult(Bounds.Width);
        var lines = wrapResult.Lines;

        for (var i = 0; i < lines.Count && i < Bounds.Height; i++)
        {
            context.WriteClipped(Bounds.X, Bounds.Y + i, lines[i]);
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (FocusableLinks)
        {
            foreach (var linkRegion in _linkRegionNodes)
                yield return linkRegion;
        }
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        if (FocusableLinks)
        {
            foreach (var linkRegion in _linkRegionNodes)
            {
                foreach (var focusable in linkRegion.GetFocusableNodes())
                    yield return focusable;
            }
        }
    }

    /// <summary>
    /// Find the currently focused link ID, or -1 if none.
    /// </summary>
    private int GetFocusedLinkId()
    {
        if (!FocusableLinks) return -1;
        foreach (var region in _linkRegionNodes)
        {
            if (region.IsFocused)
                return region.LinkId;
        }
        return -1;
    }

    private WrapResult GetWrapResult(int maxWidth)
    {
        var focusedLinkId = GetFocusedLinkId();

        // Return cached result if nothing changed
        if (_cachedWrapResult != null
            && _lastWrapWidth == maxWidth
            && ReferenceEquals(_lastInlines, Inlines)
            && ColorsEqual(_lastBaseForeground, BaseForeground)
            && _lastBaseAttributes == BaseAttributes
            && _lastFocusedLinkId == focusedLinkId
            && _lastHangingIndent == HangingIndent
            && _lastContinuationPrefix == ContinuationPrefix
            && _lastColors == _resolvedColors)
        {
            return _cachedWrapResult.Value;
        }

        var result = MarkdownInlineRenderer.RenderLinesWithLinks(
            Inlines, maxWidth, BaseForeground, BaseAttributes, focusedLinkId, HangingIndent,
            ContinuationPrefix, _resolvedColors);

        _cachedWrapResult = result;
        _wrappedLines = result.Lines.ToList();
        _lastWrapWidth = maxWidth;
        _lastInlines = Inlines;
        _lastBaseForeground = BaseForeground;
        _lastBaseAttributes = BaseAttributes;
        _lastFocusedLinkId = focusedLinkId;
        _lastHangingIndent = HangingIndent;
        _lastContinuationPrefix = ContinuationPrefix;
        _lastColors = _resolvedColors;

        return result;
    }

    /// <summary>
    /// Create or reconcile link region child nodes from the inline AST.
    /// Called during reconciliation so nodes exist before FocusRing builds.
    /// </summary>
    internal void ReconcileLinkRegions(IReadOnlyList<MarkdownInline> inlines)
    {
        var linkInfos = MarkdownInlineRenderer.ExtractLinks(inlines);

        // Reconcile: reuse existing nodes where possible
        while (_linkRegionNodes.Count > linkInfos.Count)
        {
            _linkRegionNodes.RemoveAt(_linkRegionNodes.Count - 1);
        }

        while (_linkRegionNodes.Count < linkInfos.Count)
        {
            var newNode = new MarkdownLinkRegionNode();
            newNode.Parent = this;
            _linkRegionNodes.Add(newNode);
        }

        for (int i = 0; i < linkInfos.Count; i++)
        {
            var (linkId, url, text) = linkInfos[i];
            var node = _linkRegionNodes[i];

            node.LinkId = linkId;
            node.Url = url;
            node.LinkText = text;

            // Wire activation callback
            var capturedUrl = url;
            var capturedText = text;
            node.ActivateCallback = async ctx =>
            {
                await ActivateLinkAsync(new LinkRegionInfo(linkId, capturedUrl, capturedText, 0, 0, 0));
            };
        }
    }

    /// <summary>
    /// Remove all link region nodes (when FocusableLinks is disabled).
    /// </summary>
    internal void ClearLinkRegions()
    {
        _linkRegionNodes.Clear();
    }

    /// <summary>
    /// Update link region positions from wrap result (called during MeasureCore).
    /// </summary>
    private void UpdateLinkRegionPositions(IReadOnlyList<LinkRegionInfo> linkRegions)
    {
        // Match link regions by LinkId to update positions
        var regionMap = new Dictionary<int, LinkRegionInfo>();
        foreach (var region in linkRegions)
        {
            regionMap[region.LinkId] = region;
        }

        foreach (var node in _linkRegionNodes)
        {
            if (regionMap.TryGetValue(node.LinkId, out var info))
            {
                node.LineIndex = info.LineIndex;
                node.ColumnOffset = info.ColumnOffset;
                node.LinkDisplayWidth = info.DisplayWidth;

                // Update activation callback with correct position info
                var capturedInfo = info;
                node.ActivateCallback = async ctx =>
                {
                    await ActivateLinkAsync(capturedInfo);
                };
            }
        }
    }

    private async Task ActivateLinkAsync(LinkRegionInfo linkInfo)
    {
        var kind = MarkdownLinkActivatedEventArgs.ClassifyUrl(linkInfo.Url);

        if (LinkActivatedHandler != null && SourceWidget != null)
        {
            var args = new MarkdownLinkActivatedEventArgs(
                linkInfo.Url, linkInfo.Text, kind, SourceWidget);
            await LinkActivatedHandler(args);
            if (args.Handled)
                return;
        }

        // Default behavior
        switch (kind)
        {
            case MarkdownLinkKind.External:
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(linkInfo.Url)
                    {
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // Silently ignore if browser launch fails
                }
                break;

            case MarkdownLinkKind.IntraDocument:
                ScrollToHeading(linkInfo.Url);
                break;

            case MarkdownLinkKind.Custom:
                // No default action for custom schemes
                break;
        }
    }

    private static bool ColorsEqual(Hex1bColor? a, Hex1bColor? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.Value.IsDefault == b.Value.IsDefault
            && a.Value.R == b.Value.R
            && a.Value.G == b.Value.G
            && a.Value.B == b.Value.B;
    }

    /// <summary>
    /// Finds the heading node for the given #slug URL and scrolls the nearest
    /// ancestor <see cref="ScrollPanelNode"/> to bring the heading into view.
    /// </summary>
    private void ScrollToHeading(string url)
    {
        var slug = url.TrimStart('#');
        if (string.IsNullOrEmpty(slug))
            return;

        // Walk up to find the MarkdownNode ancestor
        MarkdownNode? markdownNode = null;
        for (var ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
        {
            if (ancestor is MarkdownNode md)
            {
                markdownNode = md;
                break;
            }
        }

        if (markdownNode == null)
            return;

        if (!markdownNode.HeadingAnchors.TryGetValue(slug, out var headingNode))
            return;

        // Walk up from the heading node to find the nearest ScrollPanelNode
        for (var ancestor = headingNode.Parent; ancestor != null; ancestor = ancestor.Parent)
        {
            if (ancestor is ScrollPanelNode scrollPanel)
            {
                // Unfocus any currently focused descendant so EnsureFocusedVisible
                // in the next ArrangeCore won't scroll back to the old position.
                foreach (var focusable in scrollPanel.GetFocusableNodes())
                {
                    if (focusable != scrollPanel && focusable.IsFocused)
                    {
                        focusable.IsFocused = false;
                        break;
                    }
                }

                // Bounds.Y is in viewport coordinates (scroll offset already
                // subtracted during ArrangeCore). Convert back to content-space
                // by adding the current offset.
                var contentY = headingNode.Bounds.Y - scrollPanel.Bounds.Y + scrollPanel.Offset;
                scrollPanel.SetOffset(contentY);
                return;
            }
        }
    }
}
