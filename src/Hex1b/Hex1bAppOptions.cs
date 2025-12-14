using Hex1b.Theming;

namespace Hex1b;

/// <summary>
/// Options for configuring a Hex1bApp.
/// </summary>
public class Hex1bAppOptions
{
    /// <summary>
    /// The terminal implementation to use. If null, a ConsoleHex1bTerminal will be created.
    /// </summary>
    public IHex1bTerminal? Terminal { get; set; }

    /// <summary>
    /// The theme to use for rendering. If null, the default theme will be used.
    /// </summary>
    public Hex1bTheme? Theme { get; set; }

    /// <summary>
    /// A dynamic theme provider that is called each frame. Takes precedence over Theme if set.
    /// </summary>
    public Func<Hex1bTheme>? ThemeProvider { get; set; }

    /// <summary>
    /// Whether the app owns the terminal and should dispose it when done.
    /// Defaults to true if Terminal is null (app creates its own terminal).
    /// </summary>
    public bool? OwnsTerminal { get; set; }
    
    /// <summary>
    /// Whether to enable mouse support. When enabled, the terminal will track mouse
    /// movement and clicks, rendering a visible cursor at the mouse position.
    /// Default is false.
    /// </summary>
    public bool EnableMouse { get; set; }
}
