using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Website;

/// <summary>
/// Represents a gallery exhibit that can be displayed in the terminal gallery.
/// </summary>
public interface IGalleryExhibit
{
    /// <summary>
    /// Unique identifier for this exhibit (used in URLs).
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display title for this exhibit.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Brief description of what this exhibit demonstrates.
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Whether this exhibit requires mouse support.
    /// </summary>
    bool EnableMouse => true;

    /// <summary>
    /// Creates the Hex1b widget builder for this exhibit.
    /// Returns null if this exhibit uses RunAsync instead.
    /// </summary>
    Func<CancellationToken, Task<Hex1bWidget>>? CreateWidgetBuilder() => null;

    /// <summary>
    /// Creates a dynamic theme provider for this exhibit.
    /// If null, the default theme is used.
    /// </summary>
    Func<Hex1bTheme>? CreateThemeProvider() => null;
    
    /// <summary>
    /// Runs the exhibit with full control over the Hex1bApp lifecycle.
    /// Override this for exhibits that need to manage their own app (e.g., reactive/timer-based).
    /// Returns null if CreateWidgetBuilder should be used instead.
    /// </summary>
    Task? RunAsync(IHex1bTerminal terminal, CancellationToken cancellationToken) => null;
}

/// <summary>
/// Base class for exhibits that use the Hex1b widget system with a simple widget builder.
/// </summary>
public abstract class Hex1bExhibit : IGalleryExhibit
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string Description { get; }

    public abstract Func<CancellationToken, Task<Hex1bWidget>> CreateWidgetBuilder();

    public virtual Func<Hex1bTheme>? CreateThemeProvider() => null;
}

/// <summary>
/// Base class for exhibits that manage their own Hex1bApp lifecycle.
/// Use this for reactive exhibits that need timers, external events, etc.
/// </summary>
public abstract class ReactiveExhibit : IGalleryExhibit
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string Description { get; }

    public abstract Task RunAsync(IHex1bTerminal terminal, CancellationToken cancellationToken);
}
