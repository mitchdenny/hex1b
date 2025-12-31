namespace Hex1b;

using Hex1b.Widgets;

/// <summary>
/// Marker record for the root context (no parent widget constraint).
/// This widget should never be reconciled - it's purely a type marker.
/// </summary>
public sealed record RootWidget : Hex1bWidget
{
    internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
        => throw new NotSupportedException("RootWidget is a type marker and should not be reconciled.");

    internal override Type GetExpectedNodeType()
        => throw new NotSupportedException("RootWidget is a type marker and should not be reconciled.");
}
