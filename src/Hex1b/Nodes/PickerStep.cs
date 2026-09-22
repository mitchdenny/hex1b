using Hex1b.Events;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Hex1b.Nodes;

/// <summary>
/// The current step in the date picker drill-down flow.
/// </summary>
public enum PickerStep
{
    Year,
    Month,
    Calendar,
}
