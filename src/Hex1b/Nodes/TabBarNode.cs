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
    public record TabInfo(string Title, string? Icon, bool IsDisabled);

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

    private const int ArrowButtonWidth = 3; // " < " or " > "
    private const int DropdownButtonWidth = 3; // " ▼ "

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

        // Available width for tabs (excluding overflow controls if needed)
        var overflowWidth = NeedsOverflow ? (ArrowButtonWidth * 2 + DropdownButtonWidth) : 0;
        _availableTabsWidth = Math.Max(0, constraints.MaxWidth - overflowWidth);

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
        var theme = context.Theme;
        var resetToGlobal = theme.GetResetToGlobalCodes();

        // In Full mode, render top separator row, then tabs, then bottom separator
        // In Compact mode, just render tabs
        var tabRowY = RenderMode == TabBarRenderMode.Full ? Bounds.Y + 1 : Bounds.Y;

        // Render top separator row in Full mode
        if (RenderMode == TabBarRenderMode.Full)
        {
            var separatorChar = '▄'; // Upper half block for top border
            var separatorLine = new string(separatorChar, Bounds.Width);
            context.WriteClipped(Bounds.X, Bounds.Y, separatorLine);
        }

        var x = Bounds.X;

        // Render left arrow if needed
        if (NeedsOverflow)
        {
            var arrowFg = CanScrollLeft 
                ? theme.Get(TabBarTheme.ArrowForegroundColor)
                : theme.Get(TabBarTheme.ArrowDisabledColor);
            var arrowText = CanScrollLeft ? " ◀ " : "   ";
            context.WriteClipped(x, tabRowY, $"{arrowFg.ToForegroundAnsi()}{arrowText}{resetToGlobal}");
            x += ArrowButtonWidth;
        }

        // Render visible tabs
        for (int i = ScrollOffset; i < ScrollOffset + _visibleTabCount && i < Tabs.Count; i++)
        {
            var tab = Tabs[i];
            var isSelected = i == SelectedIndex;
            var tabWidth = _tabWidths[i];

            var fg = isSelected
                ? theme.Get(TabBarTheme.SelectedForegroundColor)
                : theme.Get(TabBarTheme.ForegroundColor);
            var bg = isSelected
                ? theme.Get(TabBarTheme.SelectedBackgroundColor)
                : theme.Get(TabBarTheme.BackgroundColor);

            // Use global colors if theme colors are default
            var fgCode = fg.IsDefault ? theme.GetGlobalForeground().ToForegroundAnsi() : fg.ToForegroundAnsi();
            var bgCode = bg.IsDefault ? theme.GetGlobalBackground().ToBackgroundAnsi() : bg.ToBackgroundAnsi();

            var tabText = !string.IsNullOrEmpty(tab.Icon)
                ? $" {tab.Icon} {tab.Title} "
                : $" {tab.Title} ";

            // Pad to tab width
            tabText = tabText.PadRight(tabWidth);

            context.WriteClipped(x, tabRowY, $"{fgCode}{bgCode}{tabText}{resetToGlobal}");
            x += tabWidth;
        }

        // Fill remaining space
        var remainingWidth = Bounds.Width - (x - Bounds.X) - (NeedsOverflow ? ArrowButtonWidth + DropdownButtonWidth : 0);
        if (remainingWidth > 0)
        {
            context.WriteClipped(x, tabRowY, new string(' ', remainingWidth));
            x += remainingWidth;
        }

        // Render right arrow and dropdown if needed
        if (NeedsOverflow)
        {
            var arrowFg = CanScrollRight
                ? theme.Get(TabBarTheme.ArrowForegroundColor)
                : theme.Get(TabBarTheme.ArrowDisabledColor);
            var arrowText = CanScrollRight ? " ▶ " : "   ";
            context.WriteClipped(x, tabRowY, $"{arrowFg.ToForegroundAnsi()}{arrowText}{resetToGlobal}");
            x += ArrowButtonWidth;

            // Dropdown button
            var dropdownFg = theme.Get(TabBarTheme.DropdownForegroundColor);
            context.WriteClipped(x, tabRowY, $"{dropdownFg.ToForegroundAnsi()} ▼ {resetToGlobal}");
        }

        // Render bottom separator row in Full mode
        if (RenderMode == TabBarRenderMode.Full)
        {
            var separatorChar = '▔'; // Upper one eighth block (U+2594)
            var separatorLine = new string(separatorChar, Bounds.Width);
            context.WriteClipped(Bounds.X, Bounds.Y + 2, separatorLine);
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
