using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Root context for starting widget tree construction.
/// </summary>
public class RootContext : WidgetContext<RootWidget>
{
    /// <summary>
    /// The cancellation token for the application lifecycle.
    /// Use this to observe when the application is shutting down.
    /// </summary>
    public CancellationToken CancellationToken { get; internal set; }
}
