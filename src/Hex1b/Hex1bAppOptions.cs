using Hex1b.Theming;
using Hex1b.Widgets;

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
    
    /// <summary>
    /// Whether to wrap the root widget in a RescueWidget for error recovery.
    /// When enabled, exceptions during build/reconcile/measure/arrange/render
    /// will be caught and a fallback UI will be displayed instead of crashing.
    /// Default is true.
    /// </summary>
    public bool EnableRescue { get; set; } = true;
    
    /// <summary>
    /// Custom fallback widget builder for the rescue widget.
    /// If null, a default fallback will be used that shows exception details
    /// in Debug mode and a friendly message in Release mode.
    /// </summary>
    public Func<RescueState, Hex1bWidget>? RescueFallbackBuilder { get; set; }
    
    /// <summary>
    /// Actions to display on the rescue fallback screen.
    /// These appear as buttons at the bottom of the error display.
    /// </summary>
    public IReadOnlyList<RescueAction>? RescueActions { get; set; }
    
    /// <summary>
    /// Whether to inject a default CTRL-C binding that calls RequestStop() to exit the app.
    /// When enabled, pressing CTRL-C will gracefully exit the application.
    /// This can be disabled if you need custom CTRL-C handling.
    /// Default is true.
    /// </summary>
    public bool EnableDefaultCtrlCExit { get; set; } = true;
}
