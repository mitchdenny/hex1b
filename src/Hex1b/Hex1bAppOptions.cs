using Hex1b.Diagnostics;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Options for configuring a Hex1bApp.
/// </summary>
public class Hex1bAppOptions
{
    // === New way (preferred) ===
    
    /// <summary>
    /// Custom workload adapter. When set, the app uses this directly.
    /// This is the preferred way to provide custom terminal infrastructure.
    /// </summary>
    public IHex1bAppTerminalWorkloadAdapter? WorkloadAdapter { get; set; }
    
    /// <summary>
    /// The theme to use for rendering. If null, the default theme will be used.
    /// </summary>
    public Hex1bTheme? Theme { get; set; }

    /// <summary>
    /// A dynamic theme provider that is called each frame. Takes precedence over Theme if set.
    /// </summary>
    public Func<Hex1bTheme>? ThemeProvider { get; set; }

    /// <summary>
    /// Whether to enable mouse support. When enabled, the terminal will track mouse
    /// movement and clicks, rendering a visible cursor at the mouse position.
    /// Default is false.
    /// </summary>
    public bool EnableMouse { get; set; }
    
    /// <summary>
    /// Metrics instance for OpenTelemetry instrumentation.
    /// If null, <see cref="Diagnostics.Hex1bMetrics.Default"/> is used.
    /// </summary>
    public Hex1bMetrics? Metrics { get; set; }
    
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
    public Func<RescueContext, Hex1bWidget>? RescueFallbackBuilder { get; set; }

    /// <summary>
    /// Handler called when the rescue widget catches an exception.
    /// Use this for logging or other error tracking.
    /// </summary>
    public Action<Events.RescueEventArgs>? OnRescue { get; set; }

    /// <summary>
    /// Handler called when the rescue widget is reset (e.g., user clicks Retry).
    /// Use this to clear any cached state that may have caused the error.
    /// </summary>
    public Action<Events.RescueResetEventArgs>? OnRescueReset { get; set; }
    
    /// <summary>
    /// Whether to inject a default CTRL-C binding that calls RequestStop() to exit the app.
    /// When enabled, pressing CTRL-C will gracefully exit the application.
    /// This can be disabled if you need custom CTRL-C handling.
    /// Default is true.
    /// </summary>
    public bool EnableDefaultCtrlCExit { get; set; } = true;
    
    /// <summary>
    /// Whether to enable input coalescing. When enabled, multiple rapid inputs are
    /// batched together before rendering, improving performance under back pressure.
    /// Disable for testing to ensure each input triggers a separate frame.
    /// Default is true.
    /// </summary>
    public bool EnableInputCoalescing { get; set; } = true;

    /// <summary>
    /// Whether to enable widget-tree render caching when Surface rendering is used.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is currently disabled by default due to known correctness issues with cached
    /// sixel content. It can still be useful in server-side rendering scenarios where
    /// sixels are not used.
    /// </para>
    /// </remarks>
    public bool EnableRenderCaching { get; set; }
    
    /// <summary>
    /// Initial delay in milliseconds for input coalescing. After processing an input,
    /// the app waits this long to allow additional inputs to arrive before rendering.
    /// Only applies when <see cref="EnableInputCoalescing"/> is true.
    /// Default is 5ms.
    /// </summary>
    public int InputCoalescingInitialDelayMs { get; set; } = 5;
    
    /// <summary>
    /// Maximum delay in milliseconds for input coalescing. The delay adaptively scales
    /// based on output queue depth but will not exceed this value.
    /// Only applies when <see cref="EnableInputCoalescing"/> is true.
    /// Default is 100ms.
    /// </summary>
    public int InputCoalescingMaxDelayMs { get; set; } = 100;
    
    /// <summary>
    /// Minimum frame interval in milliseconds. This sets the floor for animation timers
    /// and effectively caps the maximum frame rate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default of 16ms corresponds to approximately 60 FPS, which is suitable
    /// for most terminal UIs. Lower values allow higher frame rates for smooth
    /// animations in scenarios like games or data visualizations using <see cref="Widgets.SurfaceWidget"/>.
    /// </para>
    /// <para>
    /// Setting this too low may cause excessive CPU usage. Values below 1ms are
    /// clamped to 1ms to prevent CPU spin.
    /// </para>
    /// </remarks>
    public int FrameRateLimitMs { get; set; } = 16;

    /// <summary>
    /// Whether to enable pooling of temporary <see cref="Surfaces.Surface"/> instances during rendering.
    /// This primarily affects <see cref="Widgets.SurfaceWidget"/> layers and nodes like EffectPanel.
    /// Default is false.
    /// </summary>
    public bool EnableSurfacePooling { get; set; }

    /// <summary>
    /// Maximum number of pooled surfaces kept per (width, height, cell metrics) bucket.
    /// Only applies when <see cref="EnableSurfacePooling"/> is true.
    /// Default is 8.
    /// </summary>
    public int SurfacePoolMaxSurfacesPerBucket { get; set; } = 8;

    /// <summary>
    /// Number of render cycles after which an unused pool bucket is discarded.
    /// ("Unused" means "not rented" after X render cycles.)
    /// Only applies when <see cref="EnableSurfacePooling"/> is true.
    /// Default is 120.
    /// </summary>
    public int SurfacePoolMaxIdleFrames { get; set; } = 120;

    /// <summary>
    /// How long to wait for additional bytes after a bare ESC (0x1B) before treating
    /// it as a standalone Escape key press. Traditional terminals use ESC as both the
    /// Escape key and the first byte of multi-byte ANSI sequences; this timeout
    /// disambiguates the two cases.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When null (the default), uses a 50 ms threshold — the standard value used by
    /// ncurses, vim, crossterm and other TUI frameworks. Real terminal emulators
    /// deliver multi-byte sequences within a few milliseconds, while a human pressing
    /// the Escape key produces a single 0x1B with no follow-up.
    /// </para>
    /// <para>
    /// Set to <see cref="TimeSpan.Zero"/> to disable the timeout entirely. This is
    /// appropriate when the Kitty keyboard protocol is active, since it sends
    /// unambiguous CSI u sequences for every key including Escape.
    /// </para>
    /// </remarks>
    public TimeSpan? EscapeSequenceTimeout { get; set; }
}
