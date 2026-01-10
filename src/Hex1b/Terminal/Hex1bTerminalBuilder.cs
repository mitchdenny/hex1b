namespace Hex1b.Terminal;

/// <summary>
/// A builder for creating and configuring <see cref="Hex1bTerminal"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// The builder provides a fluent API for configuring terminal workloads, presentation
/// adapters, and filters. It supports multiple workload types including Hex1bApp,
/// shell processes, and custom adapters.
/// </para>
/// <example>
/// <para>Simple Hex1bApp:</para>
/// <code>
/// await Hex1bTerminal.CreateBuilder()
///     .WithHex1bApp(ctx => ctx.Text("Hello, World!"))
///     .RunAsync();
/// </code>
/// <para>Shell with recording:</para>
/// <code>
/// await Hex1bTerminal.CreateBuilder()
///     .WithShellProcess("/bin/bash")
///     .WithAsciinemaRecording("session.cast")
///     .RunAsync();
/// </code>
/// </example>
/// </remarks>
public sealed class Hex1bTerminalBuilder
{
    private IHex1bTerminalWorkloadAdapter? _workloadAdapter;
    private IHex1bTerminalPresentationAdapter? _presentationAdapter;
    private Func<IHex1bTerminalPresentationAdapter?, Hex1bTerminalBuildContext>? _workloadFactory;
    private readonly List<IHex1bTerminalWorkloadFilter> _workloadFilters = [];
    private readonly List<IHex1bTerminalPresentationFilter> _presentationFilters = [];
    private int _width = 80;
    private int _height = 24;
    private TimeProvider? _timeProvider;

    /// <summary>
    /// Creates a new terminal builder.
    /// </summary>
    public Hex1bTerminalBuilder()
    {
    }

    /// <summary>
    /// Configures the terminal with a custom workload adapter.
    /// </summary>
    /// <param name="adapter">The workload adapter to use.</param>
    /// <returns>This builder for chaining.</returns>
    public Hex1bTerminalBuilder WithWorkload(IHex1bTerminalWorkloadAdapter adapter)
    {
        _workloadAdapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _workloadFactory = null;
        return this;
    }

    /// <summary>
    /// Configures the terminal with a custom presentation adapter.
    /// </summary>
    /// <param name="adapter">The presentation adapter to use.</param>
    /// <returns>This builder for chaining.</returns>
    public Hex1bTerminalBuilder WithPresentation(IHex1bTerminalPresentationAdapter adapter)
    {
        _presentationAdapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        return this;
    }

    /// <summary>
    /// Adds a workload filter to the terminal.
    /// </summary>
    /// <param name="filter">The filter to add.</param>
    /// <returns>This builder for chaining.</returns>
    public Hex1bTerminalBuilder AddWorkloadFilter(IHex1bTerminalWorkloadFilter filter)
    {
        _workloadFilters.Add(filter ?? throw new ArgumentNullException(nameof(filter)));
        return this;
    }

    /// <summary>
    /// Adds a presentation filter to the terminal.
    /// </summary>
    /// <param name="filter">The filter to add.</param>
    /// <returns>This builder for chaining.</returns>
    public Hex1bTerminalBuilder AddPresentationFilter(IHex1bTerminalPresentationFilter filter)
    {
        _presentationFilters.Add(filter ?? throw new ArgumentNullException(nameof(filter)));
        return this;
    }

    /// <summary>
    /// Sets the time provider for the terminal. Used for testing.
    /// </summary>
    /// <param name="timeProvider">The time provider to use.</param>
    /// <returns>This builder for chaining.</returns>
    public Hex1bTerminalBuilder WithTimeProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        return this;
    }

    /// <summary>
    /// Sets the dimensions for headless terminals.
    /// </summary>
    /// <param name="width">Terminal width in columns.</param>
    /// <param name="height">Terminal height in rows.</param>
    /// <returns>This builder for chaining.</returns>
    public Hex1bTerminalBuilder WithDimensions(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
        _width = width;
        _height = height;
        return this;
    }

    /// <summary>
    /// Builds the terminal with the configured options.
    /// </summary>
    /// <returns>A configured <see cref="Hex1bTerminal"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no workload has been configured.</exception>
    public Hex1bTerminal Build()
    {
        // Resolve workload
        Func<CancellationToken, Task<int>>? runCallback = null;
        IHex1bTerminalWorkloadAdapter workload;

        if (_workloadFactory != null)
        {
            // Workload is created by factory (e.g., WithHex1bApp, WithShellProcess)
            var context = _workloadFactory(_presentationAdapter);
            workload = context.WorkloadAdapter;
            runCallback = context.RunCallback;
        }
        else if (_workloadAdapter != null)
        {
            // Explicit workload adapter provided
            workload = _workloadAdapter;
        }
        else
        {
            throw new InvalidOperationException(
                "No workload configured. Call WithWorkload(), WithHex1bApp(), WithShellProcess(), or WithProcess() before Build().");
        }

        // Build terminal
        return new Hex1bTerminal(
            presentation: _presentationAdapter,
            workload: workload,
            width: _width,
            height: _height,
            workloadFilters: _workloadFilters,
            presentationFilters: _presentationFilters,
            timeProvider: _timeProvider,
            runCallback: runCallback);
    }

    /// <summary>
    /// Builds and runs the terminal in one step.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The exit code from the workload.</returns>
    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        await using var terminal = Build();
        return await terminal.RunAsync(ct);
    }

    // === Internal for factory pattern ===

    internal void SetWorkloadFactory(Func<IHex1bTerminalPresentationAdapter?, Hex1bTerminalBuildContext> factory)
    {
        _workloadFactory = factory;
        _workloadAdapter = null;
    }
}

/// <summary>
/// Context returned by workload factories during terminal build.
/// </summary>
internal sealed class Hex1bTerminalBuildContext
{
    /// <summary>
    /// The workload adapter to use.
    /// </summary>
    public required IHex1bTerminalWorkloadAdapter WorkloadAdapter { get; init; }

    /// <summary>
    /// Optional callback that runs the workload and returns an exit code.
    /// If null, the terminal will wait for the workload to disconnect.
    /// </summary>
    public Func<CancellationToken, Task<int>>? RunCallback { get; init; }
}
