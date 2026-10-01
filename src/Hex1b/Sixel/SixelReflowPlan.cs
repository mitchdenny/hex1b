using Hex1b.Reflow;
using Hex1b.Sixel;

namespace Hex1b;

/// <summary>
/// The anchors and tracked placements built by <see cref="SixelGraphicsState.PrepareActiveReflow"/>,
/// ready to be merged with another subsystem's anchors for a single combined
/// <c>ReflowHelper.PerformReflowWithAnchors</c> call. Mirrors
/// <c>KgpTerminalGraphicsState.KgpReflowPlan</c>.
/// </summary>
internal sealed class SixelReflowPlan
{
    internal SixelReflowPlan(
        IReadOnlyList<TerminalReflowAnchor> anchors,
        IReadOnlyList<SixelReflowPlacement> placements)
    {
        Anchors = anchors;
        Placements = placements;
    }

    internal IReadOnlyList<TerminalReflowAnchor> Anchors { get; }

    internal IReadOnlyList<SixelReflowPlacement> Placements { get; }
}
