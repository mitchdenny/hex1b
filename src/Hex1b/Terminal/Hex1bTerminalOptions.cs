namespace Hex1b.Terminal;

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
