using Hex1b.Composition;
using Hex1b.Input;
using Hex1b.Theming;

namespace Hex1b.Widgets;

/// <summary>
/// Per-instance state for <see cref="SelectionPromptWidget{T}"/>: the current
/// filter text and the highlighted-row index within the filtered view.
/// </summary>
internal sealed class SelectionPromptState
{
    public string Filter = string.Empty;
    public int SelectedIndex = 0;
}
