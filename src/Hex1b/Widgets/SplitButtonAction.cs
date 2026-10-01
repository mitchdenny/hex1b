using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// Represents a secondary action in a split button dropdown menu.
/// </summary>
/// <param name="Label">The action label displayed in the dropdown menu.</param>
/// <param name="Handler">The async handler invoked when the action is selected.</param>
/// <seealso cref="SplitButtonWidget"/>
public sealed record SplitButtonAction(
    string Label,
    Func<SplitButtonClickedEventArgs, Task> Handler);
