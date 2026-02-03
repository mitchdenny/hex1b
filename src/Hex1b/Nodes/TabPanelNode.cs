using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Node representing a complete tabbed panel with tab bar and content area.
/// </summary>
public sealed class TabPanelNode : Hex1bNode, ILayoutProvider
{
    /// <summary>
    /// The tabs to display in the tab bar.
    /// </summary>
    public IReadOnlyList<TabBarNode.TabInfo> Tabs { get; set; } = [];

    /// <summary>
    /// The currently selected tab index.
    /// </summary>
    private int _selectedIndex;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex != value)
            {
                _selectedIndex = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Handler for selection changes.
    /// </summary>
    public Func<TabSelectionChangedEventArgs, Task>? SelectionChangedHandler { get; set; }

    /// <summary>
    /// The position of tabs (top or bottom).
    /// </summary>
    public TabPosition Position { get; set; } = TabPosition.Top;

    /// <summary>
    /// The total number of tabs.
    /// </summary>
    public int TabCount { get; set; }

    /// <summary>
    /// The content node for the selected tab.
    /// </summary>
    public Hex1bNode? Content { get; set; }

    /// <summary>
    /// Height of the tab bar (1 row).
    /// </summary>
    private const int TabBarHeight = 1;

    /// <summary>
    /// Scroll offset for the tab bar.
    /// </summary>
    private int _scrollOffset;

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

    public Rect ClipRect => _contentBounds;

    public ClipMode ClipMode { get; set; } = ClipMode.Clip;

    public ILayoutProvider? ParentLayoutProvider { get; set; }

    public bool ShouldRenderAt(int x, int y) => LayoutProviderHelper.ShouldRenderAt(this, x, y);

    public (int adjustedX, string clippedText) ClipString(int x, int y, string text)
        => LayoutProviderHelper.ClipString(this, x, y, text);

    #endregion

    private Rect _tabBarBounds;
    private Rect _contentBounds;

    public override IEnumerable<Hex1bNode> GetFocusableNodes()
    {
        // TabPanel itself is focusable (for tab navigation)
        yield return this;

        // Then delegate to content
        if (Content != null)
        {
            foreach (var focusable in Content.GetFocusableNodes())
            {
                yield return focusable;
            }
        }
    }

    public override IEnumerable<Hex1bNode> GetChildren()
    {
        if (Content != null)
            yield return Content;
    }

    public override Size Measure(Constraints constraints)
    {
        // Tab bar is 1 row, content fills the rest
        var contentHeight = Math.Max(0, constraints.MaxHeight - TabBarHeight);
        var contentConstraints = new Constraints(0, constraints.MaxWidth, 0, contentHeight);

        var contentSize = Content?.Measure(contentConstraints) ?? new Size(0, 0);

        var width = Math.Max(contentSize.Width, constraints.MaxWidth);
        var height = TabBarHeight + contentSize.Height;

        return constraints.Constrain(new Size(width, height));
    }

    public override void Arrange(Rect bounds)
    {
        base.Arrange(bounds);

        var tabBarY = Position == TabPosition.Bottom
            ? bounds.Y + bounds.Height - TabBarHeight
            : bounds.Y;

        var contentY = Position == TabPosition.Bottom
            ? bounds.Y
            : bounds.Y + TabBarHeight;

        var contentHeight = Math.Max(0, bounds.Height - TabBarHeight);

        _tabBarBounds = new Rect(bounds.X, tabBarY, bounds.Width, TabBarHeight);
        _contentBounds = new Rect(bounds.X, contentY, bounds.Width, contentHeight);

        Content?.Arrange(_contentBounds);
    }

    public override void Render(Hex1bRenderContext context)
    {
        var theme = context.Theme;
        var resetToGlobal = theme.GetResetToGlobalCodes();

        // Render tab bar
        RenderTabBar(context, theme, resetToGlobal);

        // Render content with clipping
        if (Content != null)
        {
            var previousLayout = context.CurrentLayoutProvider;
            ParentLayoutProvider = previousLayout;
            context.CurrentLayoutProvider = this;

            context.RenderChild(Content);

            context.CurrentLayoutProvider = previousLayout;
            ParentLayoutProvider = null;
        }
    }

    private void RenderTabBar(Hex1bRenderContext context, Hex1bTheme theme, string resetToGlobal)
    {
        var x = _tabBarBounds.X;
        var y = _tabBarBounds.Y;

        // Calculate tab widths and overflow
        var tabWidths = new List<int>();
        var totalWidth = 0;
        foreach (var tab in Tabs)
        {
            var width = CalculateTabWidth(tab);
            tabWidths.Add(width);
            totalWidth += width;
        }

        var needsOverflow = totalWidth > _tabBarBounds.Width;
        var arrowWidth = needsOverflow ? 3 : 0;
        var dropdownWidth = needsOverflow ? 3 : 0;
        var availableWidth = _tabBarBounds.Width - arrowWidth * 2 - dropdownWidth;

        // Calculate visible range
        var visibleCount = CalculateVisibleCount(tabWidths, availableWidth, _scrollOffset);
        var canScrollLeft = _scrollOffset > 0;
        var canScrollRight = _scrollOffset + visibleCount < Tabs.Count;

        // Render left arrow
        if (needsOverflow)
        {
            var arrowFg = canScrollLeft
                ? theme.Get(TabBarTheme.ArrowForegroundColor)
                : theme.Get(TabBarTheme.ArrowDisabledColor);
            var arrowText = canScrollLeft ? " ◀ " : "   ";
            context.WriteClipped(x, y, $"{arrowFg.ToForegroundAnsi()}{arrowText}{resetToGlobal}");
            x += arrowWidth;
        }

        // Render visible tabs
        for (int i = _scrollOffset; i < _scrollOffset + visibleCount && i < Tabs.Count; i++)
        {
            var tab = Tabs[i];
            var isSelected = i == SelectedIndex;
            var tabWidth = tabWidths[i];

            Hex1bColor fg, bg;
            if (isSelected)
            {
                fg = theme.Get(TabBarTheme.SelectedForegroundColor);
                bg = theme.Get(TabBarTheme.SelectedBackgroundColor);
            }
            else
            {
                fg = theme.Get(TabBarTheme.ForegroundColor);
                bg = theme.Get(TabBarTheme.BackgroundColor);
            }

            var fgCode = fg.IsDefault ? theme.GetGlobalForeground().ToForegroundAnsi() : fg.ToForegroundAnsi();
            var bgCode = bg.IsDefault ? theme.GetGlobalBackground().ToBackgroundAnsi() : bg.ToBackgroundAnsi();

            var tabText = !string.IsNullOrEmpty(tab.Icon)
                ? $" {tab.Icon} {tab.Title} "
                : $" {tab.Title} ";

            tabText = tabText.PadRight(tabWidth);

            context.WriteClipped(x, y, $"{fgCode}{bgCode}{tabText}{resetToGlobal}");
            x += tabWidth;
        }

        // Fill remaining space
        var remainingWidth = _tabBarBounds.Width - (x - _tabBarBounds.X) - (needsOverflow ? arrowWidth + dropdownWidth : 0);
        if (remainingWidth > 0)
        {
            context.WriteClipped(x, y, new string(' ', remainingWidth));
            x += remainingWidth;
        }

        // Render right arrow and dropdown
        if (needsOverflow)
        {
            var arrowFg = canScrollRight
                ? theme.Get(TabBarTheme.ArrowForegroundColor)
                : theme.Get(TabBarTheme.ArrowDisabledColor);
            var arrowText = canScrollRight ? " ▶ " : "   ";
            context.WriteClipped(x, y, $"{arrowFg.ToForegroundAnsi()}{arrowText}{resetToGlobal}");
            x += arrowWidth;

            var dropdownFg = theme.Get(TabBarTheme.DropdownForegroundColor);
            context.WriteClipped(x, y, $"{dropdownFg.ToForegroundAnsi()} ▼ {resetToGlobal}");
        }
    }

    private static int CalculateTabWidth(TabBarNode.TabInfo tab)
    {
        var textWidth = tab.Title.Length;
        if (!string.IsNullOrEmpty(tab.Icon))
        {
            textWidth += tab.Icon.Length + 1;
        }
        return textWidth + 2;
    }

    private static int CalculateVisibleCount(List<int> tabWidths, int availableWidth, int scrollOffset)
    {
        var usedWidth = 0;
        var count = 0;
        for (int i = scrollOffset; i < tabWidths.Count; i++)
        {
            if (usedWidth + tabWidths[i] > availableWidth)
                break;
            usedWidth += tabWidths[i];
            count++;
        }
        return Math.Max(1, count);
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Alt+Right to go to next tab
        bindings.Alt().Key(Hex1bKey.RightArrow).Action(async ctx =>
        {
            await SelectNextTabAsync();
        }, "Next tab");

        // Alt+Left to go to previous tab
        bindings.Alt().Key(Hex1bKey.LeftArrow).Action(async ctx =>
        {
            await SelectPreviousTabAsync();
        }, "Previous tab");

        // Tab/Shift+Tab for focus navigation within content
        bindings.Key(Hex1bKey.Tab).Action(ctx => ctx.FocusNext(), "Next focusable");
        bindings.Shift().Key(Hex1bKey.Tab).Action(ctx => ctx.FocusPrevious(), "Previous focusable");
    }

    /// <summary>
    /// Selects the next tab (wraps around).
    /// </summary>
    public async Task SelectNextTabAsync()
    {
        if (TabCount == 0) return;

        var nextIndex = (SelectedIndex + 1) % TabCount;

        // Skip disabled tabs
        var startIndex = nextIndex;
        while (Tabs[nextIndex].IsDisabled)
        {
            nextIndex = (nextIndex + 1) % TabCount;
            if (nextIndex == startIndex) return; // All tabs disabled
        }

        await SelectTabAsync(nextIndex);
    }

    /// <summary>
    /// Selects the previous tab (wraps around).
    /// </summary>
    public async Task SelectPreviousTabAsync()
    {
        if (TabCount == 0) return;

        var prevIndex = (SelectedIndex - 1 + TabCount) % TabCount;

        // Skip disabled tabs
        var startIndex = prevIndex;
        while (Tabs[prevIndex].IsDisabled)
        {
            prevIndex = (prevIndex - 1 + TabCount) % TabCount;
            if (prevIndex == startIndex) return; // All tabs disabled
        }

        await SelectTabAsync(prevIndex);
    }

    /// <summary>
    /// Selects a tab by index and fires the selection changed event.
    /// </summary>
    public async Task SelectTabAsync(int index)
    {
        if (index < 0 || index >= TabCount || Tabs[index].IsDisabled)
            return;

        var previousIndex = SelectedIndex;
        if (previousIndex == index)
            return;

        SelectedIndex = index;
        EnsureSelectedTabVisible();

        if (SelectionChangedHandler != null)
        {
            await SelectionChangedHandler(new TabSelectionChangedEventArgs
            {
                SelectedIndex = index,
                PreviousIndex = previousIndex,
                SelectedTitle = Tabs[index].Title
            });
        }
    }

    private void EnsureSelectedTabVisible()
    {
        // Calculate visible count with current scroll offset
        var tabWidths = Tabs.Select(CalculateTabWidth).ToList();
        var needsOverflow = tabWidths.Sum() > _tabBarBounds.Width;
        var arrowWidth = needsOverflow ? 3 : 0;
        var dropdownWidth = needsOverflow ? 3 : 0;
        var availableWidth = _tabBarBounds.Width - arrowWidth * 2 - dropdownWidth;
        var visibleCount = CalculateVisibleCount(tabWidths, availableWidth, _scrollOffset);

        if (SelectedIndex < _scrollOffset)
        {
            _scrollOffset = SelectedIndex;
            MarkDirty();
        }
        else if (SelectedIndex >= _scrollOffset + visibleCount)
        {
            _scrollOffset = SelectedIndex - visibleCount + 1;
            MarkDirty();
        }
    }
}
