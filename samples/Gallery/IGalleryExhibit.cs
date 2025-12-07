using Hex1b.Theming;
using Hex1b.Widgets;

namespace Gallery;

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
    /// Source code snippet to display for this exhibit.
    /// </summary>
    string SourceCode { get; }

    /// <summary>
    /// Creates the Hex1b widget builder for this exhibit.
    /// </summary>
    Func<CancellationToken, Task<Hex1bWidget>> CreateWidgetBuilder();

    /// <summary>
    /// Creates a dynamic theme provider for this exhibit.
    /// If null, the default theme is used.
    /// </summary>
    Func<Hex1bTheme>? CreateThemeProvider() => null;
}

/// <summary>
/// Base class for exhibits that use the Hex1b widget system.
/// </summary>
public abstract class Hex1bExhibit : IGalleryExhibit
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string Description { get; }
    public abstract string SourceCode { get; }

    public abstract Func<CancellationToken, Task<Hex1bWidget>> CreateWidgetBuilder();

    public virtual Func<Hex1bTheme>? CreateThemeProvider() => null;
}
