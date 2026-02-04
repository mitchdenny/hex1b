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
    private IReadOnlyList<TabBarNode.TabInfo> _tabs = [];
    private int _previousTabCount;
    
    public IReadOnlyList<TabBarNode.TabInfo> Tabs 
    { 
        get => _tabs;
        set
        {
            var tabCountChanged = _tabs.Count != value.Count;
            _tabs = value;
            // When tabs are added, scroll to show the selected tab
            if (tabCountChanged && value.Count > _previousTabCount)
            {
                _needsScrollToSelection = true;
            }
            _previousTabCount = value.Count;
            MarkDirty();
        }
    }

    /// <summary>
    /// The currently selected tab index.
    /// </summary>
    private int _selectedIndex;
    private bool _needsScrollToSelection;
    
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex != value)
            {
                _selectedIndex = value;
                _needsScrollToSelection = true;
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
    /// The rendering mode (Full or Compact).
    /// </summary>
    public TabBarRenderMode RenderMode { get; set; } = TabBarRenderMode.Full;

    /// <summary>
    /// Whether to show paging arrows when tabs overflow.
    /// </summary>
    public bool ShowPaging { get; set; } = true;

    /// <summary>
    /// Whether to show the dropdown selector for quick tab navigation.
    /// </summary>
    public bool ShowSelector { get; set; }

    /// <summary>
    /// The total number of tabs.
    /// </summary>
    public int TabCount { get; set; }

    /// <summary>
    /// The content node for the selected tab.
    /// </summary>
    private Hex1bNode? _content;
    public Hex1bNode? Content 
    { 
        get => _content;
        set
        {
            if (_content != value)
            {
                _content = value;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Height of the tab bar: 3 rows in Full mode, 1 row in Compact mode.
    /// </summary>
    private int TabBarHeight => RenderMode == TabBarRenderMode.Full ? 3 : 1;

    /// <summary>
    /// Scroll offset for the tab bar.
    /// </summary>
    private int _scrollOffset;

    /// <summary>
    /// Width of each arrow button.
    /// </summary>
    private const int ArrowButtonWidth = 3;

    /// <summary>
    /// Width of the dropdown selector button.
    /// </summary>
    private const int SelectorButtonWidth = 3;

    /// <summary>
    /// Width reserved for navigation controls (paging arrows and/or selector).
    /// </summary>
    private int ReservedControlsWidth => 
        (ShowPaging ? ArrowButtonWidth * 2 : 0) + (ShowSelector ? SelectorButtonWidth : 0);

    /// <summary>
    /// Tab hit regions for mouse click handling (x position, width, tab index).
    /// </summary>
    private List<(int X, int Width, int TabIndex)> _tabHitRegions = new();

    /// <summary>
    /// Icon hit regions for mouse click handling (x position, width, tab index, icon index, isLeft).
    /// </summary>
    private List<(int X, int Width, int TabIndex, int IconIndex, bool IsLeft, IconWidget Icon)> _iconHitRegions = new();

    /// <summary>
    /// X position of the left arrow button.
    /// </summary>
    private int _leftArrowX = -1;

    /// <summary>
    /// X position of the right arrow button.
    /// </summary>
    private int _rightArrowX = -1;

    /// <summary>
    /// X position of the dropdown selector button.
    /// </summary>
    private int _selectorX = -1;

    /// <summary>
    /// Y position of the tab row (for mouse hit testing).
    /// </summary>
    private int _tabRowY;

    /// <summary>
    /// Cached overflow state.
    /// </summary>
    private bool _canScrollLeft;
    private bool _canScrollRight;

    /// <summary>
    /// Synthetic anchor node for dropdown positioning.
    /// This is a minimal node that only provides bounds for the selector button.
    /// </summary>
    private SelectorAnchorNode? _selectorAnchor;

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
        // Only scroll to selected tab when selection changes, not on every render
        // This allows mouse wheel scrolling to work without being reset
        if (_needsScrollToSelection)
        {
            _needsScrollToSelection = false;
            EnsureSelectedTabVisible();
        }
        
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
        // Clear hit regions for this render
        _tabHitRegions.Clear();
        _iconHitRegions.Clear();

        var x = _tabBarBounds.X;
        var tabsStartX = x;

        // Track selected tab position for top separator
        int selectedTabStartX = -1;
        int selectedTabEndX = -1;

        // In Full mode: row 0 = top separator, row 1 = tabs, row 2 = bottom separator
        // In Compact mode: row 0 = tabs
        _tabRowY = RenderMode == TabBarRenderMode.Full ? _tabBarBounds.Y + 1 : _tabBarBounds.Y;

        // Calculate tab widths and overflow
        var tabWidths = new List<int>();
        var totalWidth = 0;
        foreach (var tab in Tabs)
        {
            var width = CalculateTabWidth(tab);
            tabWidths.Add(width);
            totalWidth += width;
        }

        // Reserve space for navigation controls at the end
        var availableWidth = _tabBarBounds.Width - ReservedControlsWidth;

        // Calculate visible range
        var visibleCount = CalculateVisibleCount(tabWidths, availableWidth, _scrollOffset);
        _canScrollLeft = _scrollOffset > 0;
        _canScrollRight = _scrollOffset + visibleCount < Tabs.Count;

        // Reset control positions
        _leftArrowX = -1;
        _rightArrowX = -1;
        _selectorX = -1;

        // Render visible tabs
        for (int i = _scrollOffset; i < _scrollOffset + visibleCount && i < Tabs.Count; i++)
        {
            var tab = Tabs[i];
            var isSelected = i == SelectedIndex;
            var tabWidth = tabWidths[i];
            var tabStartX = x;

            if (isSelected)
            {
                selectedTabStartX = tabStartX;
            }

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

            // Build tab content: [padding] [left icons] [icon] [title] [right icons] [padding]
            context.WriteClipped(x, _tabRowY, $"{fgCode}{bgCode} {resetToGlobal}");
            x += 1; // left padding

            // Render left icons
            for (int iconIdx = 0; iconIdx < tab.LeftIcons.Count; iconIdx++)
            {
                var icon = tab.LeftIcons[iconIdx];
                var iconWidth = icon.Icon.Length;
                _iconHitRegions.Add((x, iconWidth, i, iconIdx, true, icon));
                context.WriteClipped(x, _tabRowY, $"{fgCode}{bgCode}{icon.Icon} {resetToGlobal}");
                x += iconWidth + 1;
            }

            // Render tab icon (if any)
            if (!string.IsNullOrEmpty(tab.Icon))
            {
                context.WriteClipped(x, _tabRowY, $"{fgCode}{bgCode}{tab.Icon} {resetToGlobal}");
                x += tab.Icon.Length + 1;
            }

            // Render title
            context.WriteClipped(x, _tabRowY, $"{fgCode}{bgCode}{tab.Title}{resetToGlobal}");
            x += tab.Title.Length;

            // Render right icons
            for (int iconIdx = 0; iconIdx < tab.RightIcons.Count; iconIdx++)
            {
                var icon = tab.RightIcons[iconIdx];
                var iconWidth = icon.Icon.Length;
                context.WriteClipped(x, _tabRowY, $"{fgCode}{bgCode} {icon.Icon}{resetToGlobal}");
                x += 1; // space before icon
                _iconHitRegions.Add((x, iconWidth, i, iconIdx, false, icon));
                x += iconWidth;
            }

            // Right padding
            context.WriteClipped(x, _tabRowY, $"{fgCode}{bgCode} {resetToGlobal}");
            x += 1;

            if (isSelected)
            {
                selectedTabEndX = x;
            }

            // Track hit region for this tab (full width)
            _tabHitRegions.Add((tabStartX, tabWidth, i));
        }

        var tabsEndX = x; // Track where tabs end

        // Fill remaining space before controls
        var remainingWidth = _tabBarBounds.Width - (x - _tabBarBounds.X) - ReservedControlsWidth;
        if (remainingWidth > 0)
        {
            context.WriteClipped(x, _tabRowY, new string(' ', remainingWidth));
            x += remainingWidth;
        }

        // Render paging arrows if enabled
        if (ShowPaging)
        {
            _leftArrowX = x;
            var leftArrowFg = _canScrollLeft
                ? theme.Get(TabBarTheme.ArrowForegroundColor)
                : theme.Get(TabBarTheme.ArrowDisabledColor);
            context.WriteClipped(x, _tabRowY, $"{leftArrowFg.ToForegroundAnsi()} ◀ {resetToGlobal}");
            x += ArrowButtonWidth;

            _rightArrowX = x;
            var rightArrowFg = _canScrollRight
                ? theme.Get(TabBarTheme.ArrowForegroundColor)
                : theme.Get(TabBarTheme.ArrowDisabledColor);
            context.WriteClipped(x, _tabRowY, $"{rightArrowFg.ToForegroundAnsi()} ▶ {resetToGlobal}");
            x += ArrowButtonWidth;
        }

        // Render dropdown selector if enabled
        if (ShowSelector)
        {
            _selectorX = x;
            var selectorFg = theme.Get(TabBarTheme.ArrowForegroundColor);
            context.WriteClipped(x, _tabRowY, $"{selectorFg.ToForegroundAnsi()} ▼ {resetToGlobal}");
        }

        // Render separators in Full mode
        if (RenderMode == TabBarRenderMode.Full)
        {
            // Top separator: thin line (▁) only above the selected tab
            if (selectedTabStartX >= 0 && selectedTabEndX > selectedTabStartX)
            {
                var topSeparatorWidth = selectedTabEndX - selectedTabStartX;
                var topSeparatorLine = new string('▁', topSeparatorWidth);
                context.WriteClipped(selectedTabStartX, _tabBarBounds.Y, topSeparatorLine);
            }

            // Bottom separator: thin line (▔) across full width
            var bottomSeparatorLine = new string('▔', _tabBarBounds.Width);
            context.WriteClipped(_tabBarBounds.X, _tabBarBounds.Y + 2, bottomSeparatorLine);
        }
    }

    private static int CalculateTabWidth(TabBarNode.TabInfo tab)
    {
        var textWidth = tab.Title.Length;
        if (!string.IsNullOrEmpty(tab.Icon))
        {
            textWidth += tab.Icon.Length + 1;
        }
        // Add space for left icons (each icon + space)
        foreach (var icon in tab.LeftIcons)
        {
            textWidth += icon.Icon.Length + 1;
        }
        // Add space for right icons (space + each icon)
        foreach (var icon in tab.RightIcons)
        {
            textWidth += icon.Icon.Length + 1;
        }
        return textWidth + 2; // padding
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

        // Mouse click on tabs and arrows
        bindings.Mouse(MouseButton.Left).Action(HandleMouseClick, "Select tab or scroll");
        
        // Mouse wheel on tab bar scrolls through tabs (doesn't switch selection)
        bindings.Mouse(MouseButton.ScrollUp).Action(HandleMouseWheelUp, "Scroll tabs left");
        bindings.Mouse(MouseButton.ScrollDown).Action(HandleMouseWheelDown, "Scroll tabs right");
    }

    private Task HandleMouseWheelUp(InputBindingActionContext ctx)
    {
        // Only scroll if mouse is on the tab bar row
        if (ctx.MouseY == _tabRowY && _canScrollLeft)
        {
            _scrollOffset--;
            MarkDirty();
        }
        return Task.CompletedTask;
    }

    private Task HandleMouseWheelDown(InputBindingActionContext ctx)
    {
        // Only scroll if mouse is on the tab bar row
        if (ctx.MouseY == _tabRowY && _canScrollRight)
        {
            _scrollOffset++;
            MarkDirty();
        }
        return Task.CompletedTask;
    }

    private async Task HandleMouseClick(InputBindingActionContext ctx)
    {
        var mouseX = ctx.MouseX;
        var mouseY = ctx.MouseY;

        // Check if click is on the tab row
        if (mouseY != _tabRowY)
            return;

        // Check if click is on left arrow (only if paging is enabled)
        if (ShowPaging && _leftArrowX >= 0 && mouseX >= _leftArrowX && mouseX < _leftArrowX + ArrowButtonWidth)
        {
            if (_canScrollLeft)
            {
                _scrollOffset--;
                MarkDirty();
            }
            return;
        }

        // Check if click is on right arrow (only if paging is enabled)
        if (ShowPaging && _rightArrowX >= 0 && mouseX >= _rightArrowX && mouseX < _rightArrowX + ArrowButtonWidth)
        {
            if (_canScrollRight)
            {
                _scrollOffset++;
                MarkDirty();
            }
            return;
        }

        // Check if click is on selector dropdown (only if enabled)
        if (ShowSelector && _selectorX >= 0 && mouseX >= _selectorX && mouseX < _selectorX + SelectorButtonWidth)
        {
            ShowTabSelectorDropdown(ctx);
            return;
        }

        // Check if click is on an icon first (icons have priority over tab selection)
        foreach (var (iconX, iconWidth, tabIndex, iconIndex, isLeft, icon) in _iconHitRegions)
        {
            if (mouseX >= iconX && mouseX < iconX + iconWidth)
            {
                // Invoke the icon's click handler if it has one
                if (icon.ClickHandler != null)
                {
                    var args = new Events.IconClickedEventArgs(icon, null!, ctx);
                    await icon.ClickHandler(args);
                }
                return;
            }
        }

        // Check if click is on a tab
        foreach (var (tabX, tabWidth, tabIndex) in _tabHitRegions)
        {
            if (mouseX >= tabX && mouseX < tabX + tabWidth)
            {
                await SelectTabAsync(tabIndex);
                return;
            }
        }
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
        // Reserve space for navigation controls at the end
        var tabWidths = Tabs.Select(CalculateTabWidth).ToList();
        var availableWidth = _tabBarBounds.Width - ReservedControlsWidth;
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

    /// <summary>
    /// Shows the tab selector dropdown menu.
    /// </summary>
    private void ShowTabSelectorDropdown(InputBindingActionContext ctx)
    {
        if (Tabs.Count == 0) return;

        // Build list of tab titles
        var tabTitles = Tabs.Select(t => t.Title).ToList();

        // Create a list widget with current selection
        var list = new ListWidget(tabTitles) { InitialSelectedIndex = SelectedIndex }
            .OnItemActivated(async e =>
            {
                // Dismiss the popup first
                if (ctx.Popups.Pop(out var focusRestoreNode) && focusRestoreNode != null)
                {
                    var currentlyFocused = e.Context.FocusedNode;
                    if (currentlyFocused != null)
                    {
                        currentlyFocused.IsFocused = false;
                    }
                    focusRestoreNode.IsFocused = true;
                }

                // Select the tab
                await SelectTabAsync(e.ActivatedIndex);
            });

        // Wrap in a border for visual distinction
        var popupContent = new BorderWidget(list);

        // Create or update the synthetic anchor positioned at the selector button
        _selectorAnchor ??= new SelectorAnchorNode();
        _selectorAnchor.UpdateBounds(_selectorX, _tabRowY, SelectorButtonWidth, 1);

        // Push the popup anchored to the selector button position
        ctx.Popups.PushAnchored(_selectorAnchor, AnchorPosition.Below, popupContent, this);
    }

    /// <summary>
    /// A minimal synthetic node used as an anchor for the selector dropdown.
    /// Only provides bounds - not part of the normal node tree.
    /// </summary>
    private sealed class SelectorAnchorNode : Hex1bNode
    {
        public void UpdateBounds(int x, int y, int width, int height)
        {
            Bounds = new Rect(x, y, width, height);
        }

        public override Size Measure(Constraints constraints) => new(Bounds.Width, Bounds.Height);
        public override void Render(Hex1bRenderContext context) { } // Not rendered
    }
}
