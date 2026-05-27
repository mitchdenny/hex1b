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
    /// Build the page's widget tree. Called every frame <b>only while this
    /// page is the active one</b>. Inactive pages never have <c>Build</c>
    /// invoked, so their simulations are naturally paused.
    /// </summary>
    Hex1bWidget Build(StressContext ctx);

    /// <summary>
    /// True when this page has fully settled and has nothing to animate.
    /// The root checks this <i>after</i> calling <see cref="Build"/> to
    /// decide whether to schedule another redraw — when the active page is
    /// idle, the framework sleeps until a real input event (mouse move,
    /// key press) arrives, dropping CPU to ~zero.
    /// </summary>
    bool IsIdle => false;
}

/// <summary>
/// Ambient information passed to <see cref="IStressPage.Build"/> on every
/// frame. Kept tiny — pages should pull their own state from their own fields.
/// </summary>
internal readonly record struct StressContext(
    RootContext Root,
    double ElapsedSeconds,
    int RedrawIntervalMs);
