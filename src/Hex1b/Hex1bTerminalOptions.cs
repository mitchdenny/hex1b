namespace Hex1b;

/// <summary>
/// Options for configuring a <see cref="Hex1bTerminal"/>.
/// </summary>
/// <remarks>
/// <para>
/// This class provides a structured way to configure all aspects of a terminal,
/// including dimensions, adapters, and filters for both workload and presentation sides.
/// </para>
/// <example>
/// <code>
/// var options = new Hex1bTerminalOptions
/// {
///     Width = 80,
///     Height = 24,
///     WorkloadAdapter = new Hex1bAppWorkloadAdapter(),
///     PresentationAdapter = new ConsolePresentationAdapter()
/// };
/// options.WorkloadFilters.Add(new AsciinemaRecorder());
/// var terminal = new Hex1bTerminal(options);
/// </code>
/// </example>
/// </remarks>
public sealed class Hex1bTerminalOptions
{
    /// <summary>
    /// Terminal width in columns. Used when no presentation adapter is provided.
    /// Default is 80.
    /// </summary>
    public int Width { get; set; } = 80;

    /// <summary>
    /// Terminal height in rows. Used when no presentation adapter is provided.
    /// Default is 24.
    /// </summary>
    public int Height { get; set; } = 24;

    /// <summary>
    /// The workload adapter that connects to the application generating output.
    /// Required.
    /// </summary>
    public IHex1bTerminalWorkloadAdapter? WorkloadAdapter { get; set; }

    /// <summary>
    /// The presentation adapter for actual I/O (console, WebSocket, etc.).
    /// Pass null for headless/test mode.
    /// </summary>
    public IHex1bTerminalPresentationAdapter? PresentationAdapter { get; set; }

    /// <summary>
    /// Filters applied on the workload side of the terminal.
    /// These see raw output from the workload and input going to the workload.
    /// </summary>
    public IList<IHex1bTerminalWorkloadFilter> WorkloadFilters { get; } = new List<IHex1bTerminalWorkloadFilter>();

    /// <summary>
    /// Filters applied on the presentation side of the terminal.
    /// These see output going to the presentation layer and input from the user.
    /// </summary>
    public IList<IHex1bTerminalPresentationFilter> PresentationFilters { get; } = new List<IHex1bTerminalPresentationFilter>();

    /// <summary>
    /// The time provider to use for all time-related operations.
    /// Defaults to <see cref="TimeProvider.System"/> if not specified.
    /// </summary>
    /// <remarks>
    /// This can be overridden with a fake time provider for testing purposes,
    /// allowing deterministic control over timestamps and elapsed time calculations.
    /// </remarks>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>
    /// Optional callback that runs the workload and returns an exit code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, the terminal will call this callback from <see cref="Hex1bTerminal.RunAsync"/>
    /// after starting the I/O pumps. This is used by the builder pattern where the workload
    /// lifecycle is managed internally (e.g., PTY processes, Hex1bApp).
    /// </para>
    /// <para>
    /// When null, the terminal operates in "external workload" mode where the workload
    /// is started and managed by external code.
    /// </para>
    /// </remarks>
    public Func<CancellationToken, Task<int>>? RunCallback { get; set; }

    /// <summary>
    /// Maximum number of scrollback lines to retain. When null, no scrollback buffer
    /// is created (zero overhead). Default is null.
    /// </summary>
    public int? ScrollbackCapacity { get; set; }

    /// <summary>
    /// Optional callback invoked each time a row is scrolled off the top of the terminal
    /// into the scrollback buffer. Can be used for persistence or logging.
    /// </summary>
    public Action<ScrollbackRowEventArgs>? ScrollbackCallback { get; set; }

    /// <summary>
    /// Metrics instance for OpenTelemetry instrumentation.
    /// If null, <see cref="Diagnostics.Hex1bMetrics.Default"/> is used.
    /// </summary>
    public Diagnostics.Hex1bMetrics? Metrics { get; set; }

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

    /// <summary>
    /// Validates the options and throws if invalid.
    /// </summary>
    internal void Validate()
    {
        if (WorkloadAdapter is null)
        {
            throw new InvalidOperationException("WorkloadAdapter is required.");
        }

        if (Width <= 0)
        {
            throw new InvalidOperationException("Width must be greater than zero.");
        }

        if (Height <= 0)
        {
            throw new InvalidOperationException("Height must be greater than zero.");
        }
    }
}
