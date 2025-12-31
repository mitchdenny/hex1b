using Hex1b.Nodes;

namespace Hex1b.Widgets;

/// <summary>
/// A widget that wraps content with a condition that determines whether it should be displayed.
/// Used as part of a ResponsiveWidget to create conditional UI layouts.
/// </summary>
/// <param name="Condition">A function that receives (availableWidth, availableHeight) and returns true if this content should be displayed.</param>
/// <param name="Content">The content to display when the condition is met.</param>
public sealed record ConditionalWidget(Func<int, int, bool> Condition, Hex1bWidget Content) : Hex1bWidget
{
    // ConditionalWidget is never directly reconciled - it's used as configuration for ResponsiveWidget
    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
        => throw new NotSupportedException("ConditionalWidget should not be reconciled directly. Use ResponsiveWidget instead.");

    internal override Type GetExpectedNodeType()
        => throw new NotSupportedException("ConditionalWidget should not be reconciled directly. Use ResponsiveWidget instead.");
}
