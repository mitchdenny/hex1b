using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// Node representing a horizontal tab bar with overflow support.
/// Renders tabs, arrow buttons for scrolling, and a dropdown for quick navigation.
/// </summary>
public sealed class TabBarNode : Hex1bNode
{
    /// <summary>
    /// Information about a tab for rendering purposes.
    /// </summary>
    public record TabInfo(
        string Title, 
        string? Icon, 
        bool IsDisabled,
        IReadOnlyList<IconWidget> LeftIcons,
        IReadOnlyList<IconWidget> RightIcons);

    /// <summary>
    /// The tabs to display.
    /// </summary>
    public IReadOnlyList<TabInfo> Tabs { get; set; } = [];

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
                EnsureSelectedTabVisible();
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// The scroll offset (index of first visible tab) for overflow handling.
    /// </summary>
    private int _scrollOffset;
    public int ScrollOffset
    {
        get => _scrollOffset;
        set
        {
            var clamped = Math.Clamp(value, 0, MaxScrollOffset);
            if (_scrollOffset != clamped)
            {
                _scrollOffset = clamped;
                MarkDirty();
            }
        }
    }

    /// <summary>
    /// Handler for selection changes.
    /// </summary>
    public Func<TabSelectionChangedEventArgs, Task>? SelectionChangedHandler { get; set; }

    /// <summary>
    /// The position of tabs (affects rendering style).
    /// </summary>
    public TabPosition Position { get; set; } = TabPosition.Top;

    /// <summary>
    /// The rendering mode (Full or Compact).
    /// </summary>
    public TabBarRenderMode RenderMode { get; set; } = TabBarRenderMode.Full;

    /// <summary>
    /// Whether the tab bar needs overflow controls (arrows and dropdown).
    /// </summary>
    public bool NeedsOverflow => _totalTabsWidth > _availableTabsWidth;

    /// <summary>
    /// Whether the left arrow should be shown (can scroll left).
    /// </summary>
    public bool CanScrollLeft => ScrollOffset > 0;

    /// <summary>
    /// Whether the right arrow should be shown (can scroll right).
    /// </summary>
    public bool CanScrollRight => ScrollOffset < MaxScrollOffset;

    private int MaxScrollOffset => Math.Max(0, Tabs.Count - _visibleTabCount);

    private int _totalTabsWidth;
    private int _availableTabsWidth;
    private int _visibleTabCount;
    private List<int> _tabWidths = new();

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
    private int _leftArrowX;

    /// <summary>
    /// X position of the right arrow button.
    /// </summary>
    private int _rightArrowX;

    /// <summary>
    /// Y position of the tab row (for mouse hit testing).
    /// </summary>
    private int _tabRowY;

    /// <summary>
    /// Cached scroll state for mouse handling.
    /// </summary>
    private bool _canScrollLeftCached;
    private bool _canScrollRightCached;

    private const int ArrowButtonWidth = 3; // " < " or " > "

    public override Size Measure(Constraints constraints)
    {
        // Calculate width of each tab
        _tabWidths.Clear();
        _totalTabsWidth = 0;
        foreach (var tab in Tabs)
        {
            var tabWidth = CalculateTabWidth(tab);
            _tabWidths.Add(tabWidth);
            _totalTabsWidth += tabWidth;
        }

        // Always reserve space for arrow buttons at the end
        _availableTabsWidth = Math.Max(0, constraints.MaxWidth - (ArrowButtonWidth * 2));

        // Calculate how many tabs are visible
        _visibleTabCount = CalculateVisibleTabCount();

        // Height depends on render mode: Full = 3 rows, Compact = 1 row
        var width = constraints.MaxWidth;
        var height = RenderMode == TabBarRenderMode.Full ? 3 : 1;
        return constraints.Constrain(new Size(width, height));
    }

    private int CalculateTabWidth(TabInfo tab)
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

    private int CalculateVisibleTabCount()
    {
        var usedWidth = 0;
        var count = 0;
        for (int i = ScrollOffset; i < _tabWidths.Count; i++)
        {
            if (usedWidth + _tabWidths[i] > _availableTabsWidth)
                break;
            usedWidth += _tabWidths[i];
            count++;
        }
        return Math.Max(1, count);
    }

    private void EnsureSelectedTabVisible()
    {
        if (SelectedIndex < ScrollOffset)
        {
            ScrollOffset = SelectedIndex;
        }
        else if (SelectedIndex >= ScrollOffset + _visibleTabCount)
        {
            ScrollOffset = SelectedIndex - _visibleTabCount + 1;
        }
    }

    public override void Render(Hex1bRenderContext context)
    {
        // Clear hit regions for this render
        _tabHitRegions.Clear();
        _iconHitRegions.Clear();

        var theme = context.Theme;
        var resetToGlobal = theme.GetResetToGlobalCodes();

        // In Full mode, render top separator row, then tabs, then bottom separator
        // In Compact mode, just render tabs
        _tabRowY = RenderMode == TabBarRenderMode.Full ? Bounds.Y + 1 : Bounds.Y;

        var x = Bounds.X;
        var tabsStartX = x;

        // Cache scroll state for mouse handling
        _canScrollLeftCached = CanScrollLeft;
        _canScrollRightCached = CanScrollRight;

        // Render visible tabs
        for (int i = ScrollOffset; i < ScrollOffset + _visibleTabCount && i < Tabs.Count; i++)
        {
            var tab = Tabs[i];
            var isSelected = i == SelectedIndex;
            var tabWidth = _tabWidths[i];
            var tabStartX = x;

            var fg = isSelected
                ? theme.Get(TabBarTheme.SelectedForegroundColor)
                : theme.Get(TabBarTheme.ForegroundColor);
            var bg = isSelected
                ? theme.Get(TabBarTheme.SelectedBackgroundColor)
                : theme.Get(TabBarTheme.BackgroundColor);

            // Use global colors if theme colors are default
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

            // Track hit region for this tab (full width)
            _tabHitRegions.Add((tabStartX, tabWidth, i));
        }

        var tabsEndX = x; // Track where tabs end for top separator

        // Fill remaining space before arrows
        var remainingWidth = Bounds.Width - (x - Bounds.X) - (ArrowButtonWidth * 2);
        if (remainingWidth > 0)
        {
            context.WriteClipped(x, _tabRowY, new string(' ', remainingWidth));
            x += remainingWidth;
        }

        // Render left arrow (always visible, grayed if can't scroll left)
        _leftArrowX = x;
        var leftArrowFg = CanScrollLeft
            ? theme.Get(TabBarTheme.ArrowForegroundColor)
            : theme.Get(TabBarTheme.ArrowDisabledColor);
        context.WriteClipped(x, _tabRowY, $"{leftArrowFg.ToForegroundAnsi()} ◀ {resetToGlobal}");
        x += ArrowButtonWidth;

        // Render right arrow (always visible, grayed if can't scroll right)
        _rightArrowX = x;
        var rightArrowFg = CanScrollRight
            ? theme.Get(TabBarTheme.ArrowForegroundColor)
            : theme.Get(TabBarTheme.ArrowDisabledColor);
        context.WriteClipped(x, _tabRowY, $"{rightArrowFg.ToForegroundAnsi()} ▶ {resetToGlobal}");

        // Render separators in Full mode
        if (RenderMode == TabBarRenderMode.Full)
        {
            // Top separator: thin line (▁) only above the tabs area
            var topSeparatorWidth = tabsEndX - tabsStartX;
            if (topSeparatorWidth > 0)
            {
                var topSeparatorLine = new string('▁', topSeparatorWidth);
                context.WriteClipped(tabsStartX, Bounds.Y, topSeparatorLine);
            }

            // Bottom separator: thin line (▔) across full width
            var bottomSeparatorLine = new string('▔', Bounds.Width);
            context.WriteClipped(Bounds.X, Bounds.Y + 2, bottomSeparatorLine);
        }
    }

    public override void ConfigureDefaultBindings(InputBindingsBuilder bindings)
    {
        // Left/Right arrow keys to scroll through tabs when overflow
        bindings.Key(Hex1bKey.LeftArrow).Action(_ =>
        {
            if (CanScrollLeft)
                ScrollOffset--;
            return Task.CompletedTask;
        }, "Scroll tabs left");

        bindings.Key(Hex1bKey.RightArrow).Action(_ =>
        {
            if (CanScrollRight)
                ScrollOffset++;
            return Task.CompletedTask;
        }, "Scroll tabs right");

        // Mouse click on tabs and arrows
        bindings.Mouse(MouseButton.Left).Action(HandleMouseClick, "Select tab or scroll");
    }

    private async Task HandleMouseClick(InputBindingActionContext ctx)
    {
        var mouseX = ctx.MouseX;
        var mouseY = ctx.MouseY;

        // Check if click is on the tab row
        if (mouseY != _tabRowY)
            return;

        // Check if click is on left arrow
        if (mouseX >= _leftArrowX && mouseX < _leftArrowX + ArrowButtonWidth)
        {
            if (_canScrollLeftCached)
            {
                ScrollOffset--;
            }
            return;
        }

        // Check if click is on right arrow
        if (mouseX >= _rightArrowX && mouseX < _rightArrowX + ArrowButtonWidth)
        {
            if (_canScrollRightCached)
            {
                ScrollOffset++;
            }
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
    /// Selects a tab by index and fires the selection changed event.
    /// </summary>
    public async Task SelectTabAsync(int index)
    {
        if (index < 0 || index >= Tabs.Count || Tabs[index].IsDisabled)
            return;

        var previousIndex = SelectedIndex;
        if (previousIndex == index)
            return;

        SelectedIndex = index;

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
}
