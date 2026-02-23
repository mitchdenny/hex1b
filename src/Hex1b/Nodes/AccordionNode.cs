using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Node representing a collapsible accordion with multiple sections.
/// Each section has a header row and optional content area.
/// </summary>
public sealed class AccordionNode : Hex1bNode, ILayoutProvider
{
    /// <summary>
    /// Information about a single accordion section.
    /// </summary>
    public sealed record SectionInfo(
        string Title,
        bool IsExpanded,
        IReadOnlyList<IconWidget> LeftActions,
        IReadOnlyList<IconWidget> RightActions);

    private List<SectionInfo> _sections = [];
    private List<Hex1bNode?> _contentNodes = [];
    private List<Rect> _headerBounds = [];
    private List<Rect> _contentBounds = [];
    private List<(int X, int Width, int SectionIndex, int IconIndex, bool IsLeft, IconWidget Icon)> _iconHitRegions = [];
    private int _focusedSectionIndex;

    /// <summary>
    /// Whether multiple sections can be expanded simultaneously.
    /// </summary>
    public bool AllowMultipleExpanded { get; set; } = true;

    /// <summary>
    /// Handler for section expand/collapse changes.
    /// </summary>
    internal Func<AccordionSectionExpandedEventArgs, Task>? SectionExpandedHandler { get; set; }

    /// <summary>
    /// Gets the number of sections.
    /// </summary>
    public int SectionCount => _sections.Count;

    private bool _isFocused;
    public override bool IsFocused
    {
        get => _isFocused;
        set
        {
            if (_isFocused != value)
            {
                _isFocused = value;
                MarkDirty();
            }
        }
    }

    public override bool IsFocusable => true;
    public override bool ManagesChildFocus => true;

    #region ILayoutProvider Implementation

    private Rect _currentClipRect;

    public Rect ClipRect => _currentClipRect;

    public ClipMode ClipMode { get; set; } = ClipMode.Clip;

    public ILayoutProvider? ParentLayoutProvider { get; set; }

    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        => LayoutProviderHelper.ClipString(this, x, y, text);

    #endregion

    /// <summary>
    /// Sets the section list. Called during reconciliation.
    /// </summary>
    internal void SetSections(List<SectionInfo> sections)
    {
        _sections = sections;
        // Ensure content nodes list matches section count
        while (_contentNodes.Count < sections.Count)
            _contentNodes.Add(null);
        while (_contentNodes.Count > sections.Count)
            _contentNodes.RemoveAt(_contentNodes.Count - 1);
        MarkDirty();
    }

    /// <summary>
    /// Gets the content node for a section.
    /// </summary>
    internal Hex1bNode? GetSectionContent(int index) =>
        index < _contentNodes.Count ? _contentNodes[index] : null;

    /// <summary>
    /// Sets the content node for a section.
    /// </summary>
    internal void SetSectionContent(int index, Hex1bNode? content)
    {
        if (index < _contentNodes.Count)
            _contentNodes[index] = content;
    }

    /// <summary>
    /// Gets whether a section is expanded.
    /// </summary>
    public bool IsSectionExpanded(int index) =>
        index >= 0 && index < _sections.Count && _sections[index].IsExpanded;

    /// <summary>
    /// Sets the expanded state of a section.
    /// </summary>
    public void SetSectionExpanded(int index, bool expanded)
    {
        if (index < 0 || index >= _sections.Count)
            return;

        if (_sections[index].IsExpanded == expanded)
            return;

        if (!AllowMultipleExpanded && expanded)
        {
            // Collapse all other sections
            for (int i = 0; i < _sections.Count; i++)
            {
                if (i != index && _sections[i].IsExpanded)
                {
                    _sections[i] = _sections[i] with { IsExpanded = false };
                }
            }
        }

        _sections[index] = _sections[index] with { IsExpanded = expanded };
        MarkDirty();
    }

    /// <summary>
    /// Toggles the expand/collapse state of a section.
    /// </summary>
    public void ToggleSection(int index)
    {
        if (index >= 0 && index < _sections.Count)
        {
            SetSectionExpanded(index, !_sections[index].IsExpanded);
        }
    }

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        yield return this;

        for (int i = 0; i < _sections.Count; i++)
        {
            if (_sections[i].IsExpanded && _contentNodes[i] != null)
            {
                foreach (var focusable in _contentNodes[i]!.GetFocusableNodes())
                {
                    yield return focusable;
                }
            }
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        for (int i = 0; i < _contentNodes.Count; i++)
        {
            if (_contentNodes[i] != null)
                yield return _contentNodes[i]!;
        }
    }

    protected override Size MeasureCore(Constraints constraints)
    {
        var totalHeaderHeight = _sections.Count; // 1 row per header
        var expandedCount = _sections.Count(s => s.IsExpanded);

        if (expandedCount == 0 || constraints.MaxHeight <= totalHeaderHeight)
        {
            // All collapsed or no room for content
            return constraints.Constrain(new Size(constraints.MaxWidth, totalHeaderHeight));
        }

        // Distribute remaining height equally among expanded sections
        var availableContentHeight = constraints.MaxHeight - totalHeaderHeight;
        var perSectionHeight = availableContentHeight / expandedCount;
        var remainder = availableContentHeight % expandedCount;

        var totalHeight = totalHeaderHeight;
        var expandedIdx = 0;
        for (int i = 0; i < _sections.Count; i++)
        {
            if (_sections[i].IsExpanded && _contentNodes[i] != null)
            {
                var sectionContentHeight = perSectionHeight + (expandedIdx < remainder ? 1 : 0);
                var contentConstraints = new Constraints(0, constraints.MaxWidth, 0, sectionContentHeight);
                _contentNodes[i]!.Measure(contentConstraints);
                totalHeight += sectionContentHeight;
                expandedIdx++;
            }
        }

        return constraints.Constrain(new Size(constraints.MaxWidth, constraints.MaxHeight));
    }

    protected override void ArrangeCore(Rect bounds)
    {
        base.ArrangeCore(bounds);

        _headerBounds.Clear();
        _contentBounds.Clear();

        var totalHeaderHeight = _sections.Count;
        var expandedCount = _sections.Count(s => s.IsExpanded);

        var availableContentHeight = Math.Max(0, bounds.Height - totalHeaderHeight);
        var perSectionHeight = expandedCount > 0 ? availableContentHeight / expandedCount : 0;
        var remainder = expandedCount > 0 ? availableContentHeight % expandedCount : 0;

        var currentY = bounds.Y;
        var expandedIdx = 0;
        for (int i = 0; i < _sections.Count; i++)
        {
            // Header
            var headerRect = new Rect(bounds.X, currentY, bounds.Width, 1);
            _headerBounds.Add(headerRect);
            currentY++;

            // Content (if expanded)
            if (_sections[i].IsExpanded && _contentNodes[i] != null)
            {
                var sectionContentHeight = perSectionHeight + (expandedIdx < remainder ? 1 : 0);
                var contentRect = new Rect(bounds.X, currentY, bounds.Width, sectionContentHeight);
                _contentBounds.Add(contentRect);
                _contentNodes[i]!.Arrange(contentRect);
                currentY += sectionContentHeight;
                expandedIdx++;
            }
            else
            {
                _contentBounds.Add(new Rect(bounds.X, currentY, bounds.Width, 0));
            }
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var resetToGlobal = theme.GetResetToGlobalCodes();
        var expandedChevron = theme.Get(AccordionTheme.ExpandedChevron);
        var collapsedChevron = theme.Get(AccordionTheme.CollapsedChevron);

        _iconHitRegions.Clear();

        for (int i = 0; i < _sections.Count; i++)
        {
            RenderHeader(context, theme, resetToGlobal, i, expandedChevron, collapsedChevron);

            if (_sections[i].IsExpanded && _contentNodes[i] != null && i < _contentBounds.Count)
            {
                var contentRect = _contentBounds[i];
                if (contentRect.Height > 0)
                {
                    var previousLayout = context.CurrentLayoutProvider;
                    _currentClipRect = contentRect;
                    ParentLayoutProvider = previousLayout;
                    context.CurrentLayoutProvider = this;

                    context.RenderChild(_contentNodes[i]!);

                    context.CurrentLayoutProvider = previousLayout;
                    ParentLayoutProvider = null;
                }
            }
        }
    }

    private void RenderHeader(
        Hex1bRenderContext context,
        Hex1bTheme theme,
        string resetToGlobal,
        int sectionIndex,
        char expandedChevron,
        char collapsedChevron)
    {
        if (sectionIndex >= _headerBounds.Count)
            return;

        var header = _headerBounds[sectionIndex];
        var section = _sections[sectionIndex];
        var isFocusedHeader = IsFocused && _focusedSectionIndex == sectionIndex;

        Hex1bColor fg, bg;
        if (isFocusedHeader)
        {
            fg = theme.Get(AccordionTheme.FocusedHeaderForegroundColor);
            bg = theme.Get(AccordionTheme.FocusedHeaderBackgroundColor);
        }
        else
        {
            fg = theme.Get(AccordionTheme.HeaderForegroundColor);
            bg = theme.Get(AccordionTheme.HeaderBackgroundColor);
        }

        var fgCode = fg.ToForegroundAnsi();
        var bgCode = bg.ToBackgroundAnsi();

        var x = header.X;
        var y = header.Y;

        // Render chevron
        var chevron = section.IsExpanded ? expandedChevron : collapsedChevron;
        context.WriteClipped(x, y, $"{fgCode}{bgCode}{chevron} {resetToGlobal}");
        x += 2;

        // Render left icons
        for (int iconIdx = 0; iconIdx < section.LeftActions.Count; iconIdx++)
        {
            var icon = section.LeftActions[iconIdx];
            var iconWidth = icon.Icon.Length;
            _iconHitRegions.Add((x, iconWidth, sectionIndex, iconIdx, true, icon));
            context.WriteClipped(x, y, $"{fgCode}{bgCode}{icon.Icon} {resetToGlobal}");
            x += iconWidth + 1;
        }

        // Render title
        var maxTitleWidth = header.Width - (x - header.X);
        // Reserve space for right icons
        var rightIconsWidth = 0;
        foreach (var icon in section.RightActions)
        {
            rightIconsWidth += icon.Icon.Length + 1;
        }
        maxTitleWidth -= rightIconsWidth;

        var title = section.Title;
        if (title.Length > maxTitleWidth && maxTitleWidth > 0)
        {
            title = title[..(maxTitleWidth - 1)] + "…";
        }

        context.WriteClipped(x, y, $"{fgCode}{bgCode}{title}{resetToGlobal}");
        x += title.Length;

        // Fill remaining space before right icons
        var rightIconsStartX = header.X + header.Width - rightIconsWidth;
        if (x < rightIconsStartX)
        {
            var fillLen = rightIconsStartX - x;
            context.WriteClipped(x, y, $"{fgCode}{bgCode}{new string(' ', fillLen)}{resetToGlobal}");
            x = rightIconsStartX;
        }

        // Render right icons
        for (int iconIdx = 0; iconIdx < section.RightActions.Count; iconIdx++)
        {
            var icon = section.RightActions[iconIdx];
            var iconWidth = icon.Icon.Length;
            context.WriteClipped(x, y, $"{fgCode}{bgCode} {icon.Icon}{resetToGlobal}");
            x += 1; // space before icon
            _iconHitRegions.Add((x, iconWidth, sectionIndex, iconIdx, false, icon));
            x += iconWidth;
        }
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Enter/Space to toggle section
        bindings.Key(Hex1bKey.Enter).Action(async ctx =>
        {
            await ToggleSectionAsync(_focusedSectionIndex);
        }, "Toggle section");

        bindings.Key(Hex1bKey.Spacebar).Action(async ctx =>
        {
            await ToggleSectionAsync(_focusedSectionIndex);
        }, "Toggle section");

        // Up/Down to navigate between section headers
        bindings.Key(Hex1bKey.UpArrow).Action(ctx =>
        {
            if (_focusedSectionIndex > 0)
            {
                _focusedSectionIndex--;
                MarkDirty();
            }
            return Task.CompletedTask;
        }, "Previous section");

        bindings.Key(Hex1bKey.DownArrow).Action(ctx =>
        {
            if (_focusedSectionIndex < _sections.Count - 1)
            {
                _focusedSectionIndex++;
                MarkDirty();
            }
            return Task.CompletedTask;
        }, "Next section");

        // Tab/Shift+Tab for focus navigation into content
        bindings.Key(Hex1bKey.Tab).Action(ctx => ctx.FocusNext(), "Next focusable");
        bindings.Shift().Key(Hex1bKey.Tab).Action(ctx => ctx.FocusPrevious(), "Previous focusable");

        // Mouse click on headers
        bindings.Mouse(MouseButton.Left).Action(HandleMouseClick, "Toggle section or click icon");
    }

    private async Task HandleMouseClick(InputBindingActionContext ctx)
    {
        var mouseX = ctx.MouseX;
        var mouseY = ctx.MouseY;

        // Check icon hit regions first
        foreach (var (iconX, iconWidth, sectionIndex, iconIndex, isLeft, icon) in _iconHitRegions)
        {
            if (mouseY == _headerBounds[sectionIndex].Y && mouseX >= iconX && mouseX < iconX + iconWidth)
            {
                if (icon.ClickHandler != null)
                {
                    var args = new Events.IconClickedEventArgs(icon, null!, ctx);
                    await icon.ClickHandler(args);
                }
                return;
            }
        }

        // Check header hit regions
        for (int i = 0; i < _headerBounds.Count; i++)
        {
            var header = _headerBounds[i];
            if (mouseY == header.Y && mouseX >= header.X && mouseX < header.X + header.Width)
            {
                _focusedSectionIndex = i;
                await ToggleSectionAsync(i);
                return;
            }
        }
    }

    private async Task ToggleSectionAsync(int index)
    {
        if (index < 0 || index >= _sections.Count)
            return;

        var newExpanded = !_sections[index].IsExpanded;

        if (!AllowMultipleExpanded && newExpanded)
        {
            for (int i = 0; i < _sections.Count; i++)
            {
                if (i != index && _sections[i].IsExpanded)
                {
                    _sections[i] = _sections[i] with { IsExpanded = false };
                }
            }
        }

        _sections[index] = _sections[index] with { IsExpanded = newExpanded };
        MarkDirty();

        if (SectionExpandedHandler != null)
        {
            var args = new AccordionSectionExpandedEventArgs
            {
                SectionIndex = index,
                IsExpanded = newExpanded,
                SectionTitle = _sections[index].Title
            };
            await SectionExpandedHandler(args);
        }
    }
}
