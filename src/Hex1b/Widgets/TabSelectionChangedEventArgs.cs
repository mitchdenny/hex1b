using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Event arguments for tab selection changes.
/// </summary>
public sealed class TabSelectionChangedEventArgs : EventArgs
{
    /// <summary>
    /// The index of the newly selected tab.
    /// </summary>
    public int SelectedIndex { get; init; }

    /// <summary>
    /// The index of the previously selected tab, or -1 if none.
    /// </summary>
    public int PreviousIndex { get; init; }

    /// <summary>
    /// The title of the newly selected tab.
    /// </summary>
    public string SelectedTitle { get; init; } = "";
}
