namespace Hex1b.Widgets;

/// <summary>
/// An action that can be taken from the rescue fallback screen.
/// </summary>
/// <param name="Label">The button label displayed to the user.</param>
/// <param name="Action">The callback to execute when the action is triggered.</param>
public sealed record RescueAction(string Label, Action Action);
