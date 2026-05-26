using Hex1b;
using Hex1b.Widgets;

namespace PerfStressDemo;

/// <summary>
/// A single stress-test "page" in the demo. Each page returns a full-screen
/// widget tree designed to exercise some specific part of the render pipeline.
/// </summary>
internal interface IStressPage
{
    /// <summary>Short name shown in the status bar.</summary>
    string Name { get; }

    /// <summary>One-line description of what this page is stressing.</summary>
    string Description { get; }

    /// <summary>
    /// Build the page's widget tree. Called every frame. The implementation
    /// should call <c>RedrawAfter</c> (or otherwise schedule continuous
    /// invalidation) on any animated subtree so the frame rate is driven by
    /// the page rather than by external input.
    /// </summary>
    Hex1bWidget Build(StressContext ctx);
}

/// <summary>
/// Ambient information passed to <see cref="IStressPage.Build"/> on every
/// frame. Kept tiny — pages should pull their own state from their own fields.
/// </summary>
internal readonly record struct StressContext(
    RootContext Root,
    double ElapsedSeconds,
    int RedrawIntervalMs);
