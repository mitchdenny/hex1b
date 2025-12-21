using Hex1b;
using Hex1b.Terminal;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Website;

/// <summary>
/// Represents a gallery example that can be displayed in the terminal gallery.
/// </summary>
public interface IGalleryExample
{
    /// <summary>
    /// Unique identifier for this example (used in URLs).
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display title for this example.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Brief description of what this example demonstrates.
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Whether this example requires mouse support.
    /// </summary>
    bool EnableMouse => true;

    /// <summary>
    /// Creates the Hex1b widget builder for this example.
    /// Returns null if this example uses RunAsync instead.
    /// </summary>
    Func<Hex1bWidget>? CreateWidgetBuilder() => null;

    /// <summary>
    /// Creates a dynamic theme provider for this example.
    /// If null, the default theme is used.
    /// </summary>
    Func<Hex1bTheme>? CreateThemeProvider() => null;
    
    /// <summary>
    /// Runs the example with full control over the Hex1bApp lifecycle.
    /// Override this for examples that need to manage their own app (e.g., reactive/timer-based).
    /// Returns null if CreateWidgetBuilder should be used instead.
    /// </summary>
    Task? RunAsync(IHex1bAppTerminalWorkloadAdapter workloadAdapter, CancellationToken cancellationToken) => null;
}

/// <summary>
/// Base class for examples that use the Hex1b widget system with a simple widget builder.
/// </summary>
public abstract class Hex1bExample : IGalleryExample
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string Description { get; }

    public abstract Func<Hex1bWidget> CreateWidgetBuilder();

    public virtual Func<Hex1bTheme>? CreateThemeProvider() => null;
}

/// <summary>
/// Base class for examples that manage their own Hex1bApp lifecycle.
/// Use this for reactive examples that need timers, external events, etc.
/// </summary>
public abstract class ReactiveExample : IGalleryExample
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string Description { get; }

    public abstract Task RunAsync(IHex1bAppTerminalWorkloadAdapter workloadAdapter, CancellationToken cancellationToken);
}
