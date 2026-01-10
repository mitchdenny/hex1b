using Hex1b.Widgets;

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
    private bool _enableMouse;
    private bool _presentationExplicitlyConfigured;

    /// <summary>
    /// Creates a new terminal builder.
    /// </summary>
    public Hex1bTerminalBuilder()
    {
    }

    /// <summary>
    /// Configures the terminal to run a Hex1bApp with the specified widget builder.
    /// </summary>
    /// <param name="builder">
    /// A function that builds the UI widget tree. The function receives a <see cref="RootContext"/>
    /// providing access to application state and cancellation.
    /// </param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This is the simplest way to create a terminal-based UI application. The builder
    /// function is called on each render cycle to produce the current widget tree.
    /// </para>
    /// <para>
    /// If no presentation adapter is configured, the builder will automatically use
    /// a console presentation adapter.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await Hex1bTerminal.CreateBuilder()
    ///     .WithHex1bApp(ctx => ctx.Text("Hello, World!"))
    ///     .RunAsync();
    /// </code>
    /// </example>
    public Hex1bTerminalBuilder WithHex1bApp(Func<RootContext, Hex1bWidget> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return WithHex1bApp(ctx => Task.FromResult(builder(ctx)));
    }

    /// <summary>
    /// Configures the terminal to run a Hex1bApp with the specified async widget builder.
    /// </summary>
    /// <param name="builder">
    /// An async function that builds the UI widget tree. The function receives a <see cref="RootContext"/>
    /// providing access to application state and cancellation.
    /// </param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when your widget building requires async operations, such as
    /// loading initial data or checking external state.
    /// </para>
    /// <para>
    /// If no presentation adapter is configured, the builder will automatically use
    /// a console presentation adapter.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await Hex1bTerminal.CreateBuilder()
    ///     .WithHex1bApp(async ctx =>
    ///     {
    ///         var data = await LoadDataAsync();
    ///         return ctx.Text(data);
    ///     })
    ///     .RunAsync();
    /// </code>
    /// </example>
    public Hex1bTerminalBuilder WithHex1bApp(Func<RootContext, Task<Hex1bWidget>> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        SetWorkloadFactory(presentation =>
        {
            // Get capabilities from presentation adapter
            var capabilities = presentation?.Capabilities ?? TerminalCapabilities.Modern;

            // Create workload adapter for Hex1bApp
            var workloadAdapter = new Hex1bAppWorkloadAdapter(capabilities);

            // Create the run callback that will run the Hex1bApp
            Func<CancellationToken, Task<int>> runCallback = async ct =>
            {
                var options = new Hex1bAppOptions
                {
                    WorkloadAdapter = workloadAdapter,
                    EnableMouse = _enableMouse
                };

                await using var app = new Hex1bApp(builder, options);
                await app.RunAsync(ct);
                return 0; // Success exit code
            };

            return new Hex1bTerminalBuildContext(workloadAdapter, runCallback);
        });

        return this;
    }

    /// <summary>
    /// Configures the terminal to run a Hex1bApp with full control over options and app capture.
    /// </summary>
    /// <param name="configure">
    /// A configuration function that receives the <see cref="Hex1bApp"/> instance and 
    /// <see cref="Hex1bAppOptions"/> for customization, and returns the widget builder function.
    /// </param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This overload provides full control over the Hex1bApp configuration and allows
    /// capturing the app instance for external control (e.g., calling <c>RequestStop()</c>
    /// or <c>Invalidate()</c> from outside the render loop).
    /// </para>
    /// <para>
    /// The <see cref="Hex1bAppOptions.WorkloadAdapter"/> and <see cref="Hex1bAppOptions.EnableMouse"/>
    /// properties are managed by the builder and will throw if set in the callback.
    /// Use <see cref="WithMouse"/> to enable mouse support.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// Hex1bApp? capturedApp = null;
    /// 
    /// await Hex1bTerminal.CreateBuilder()
    ///     .WithHex1bApp((app, options) =>
    ///     {
    ///         capturedApp = app;
    ///         options.Theme = MyCustomTheme;
    ///         return ctx => ctx.Text("Hello");
    ///     })
    ///     .RunAsync();
    /// </code>
    /// </example>
    public Hex1bTerminalBuilder WithHex1bApp(
        Func<Hex1bApp, Hex1bAppOptions, Func<RootContext, Hex1bWidget>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        SetWorkloadFactory(presentation =>
        {
            // Get capabilities from presentation adapter
            var capabilities = presentation?.Capabilities ?? TerminalCapabilities.Modern;

            // Create workload adapter for Hex1bApp
            var workloadAdapter = new Hex1bAppWorkloadAdapter(capabilities);
            var enableMouse = _enableMouse;

            // Create options with managed properties already set
            // Note: WorkloadAdapter and EnableMouse are managed by the builder.
            // If user modifies these in configure, behavior is undefined.
            var options = new Hex1bAppOptions
            {
                WorkloadAdapter = workloadAdapter,
                EnableMouse = enableMouse
            };

            // Create the run callback - app is created here so user can capture it
            Func<CancellationToken, Task<int>> runCallback = async ct =>
            {
                Hex1bApp? app = null;
                Func<RootContext, Hex1bWidget>? widgetBuilder = null;
                bool configureInvoked = false;

                // Widget builder that wraps the user's builder
                Func<RootContext, Hex1bWidget> wrappedBuilder = ctx =>
                {
                    // On first call, invoke configure to get the real builder
                    if (!configureInvoked)
                    {
                        configureInvoked = true;
                        widgetBuilder = configure(app!, options);
                    }
                    return widgetBuilder!(ctx);
                };

                app = new Hex1bApp(wrappedBuilder, options);
                await using (app)
                {
                    await app.RunAsync(ct);
                }
                return 0;
            };

            return new Hex1bTerminalBuildContext(workloadAdapter, runCallback);
        });

        return this;
    }

    /// <summary>
    /// Configures the terminal to run a Hex1bApp with full control over options, app capture,
    /// and async widget building.
    /// </summary>
    /// <param name="configure">
    /// A configuration function that receives the <see cref="Hex1bApp"/> instance and 
    /// <see cref="Hex1bAppOptions"/> for customization, and returns an async widget builder function.
    /// </param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This overload provides full control over the Hex1bApp configuration and allows
    /// capturing the app instance for external control, with async widget building support.
    /// </para>
    /// </remarks>
    public Hex1bTerminalBuilder WithHex1bApp(
        Func<Hex1bApp, Hex1bAppOptions, Func<RootContext, Task<Hex1bWidget>>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        SetWorkloadFactory(presentation =>
        {
            var capabilities = presentation?.Capabilities ?? TerminalCapabilities.Modern;
            var workloadAdapter = new Hex1bAppWorkloadAdapter(capabilities);
            var enableMouse = _enableMouse;
            
            // Create options with managed properties already set
            var options = new Hex1bAppOptions
            {
                WorkloadAdapter = workloadAdapter,
                EnableMouse = enableMouse
            };

            Func<CancellationToken, Task<int>> runCallback = async ct =>
            {
                Hex1bApp? app = null;
                Func<RootContext, Task<Hex1bWidget>>? widgetBuilder = null;
                bool configureInvoked = false;

                Func<RootContext, Task<Hex1bWidget>> wrappedBuilder = async ctx =>
                {
                    if (!configureInvoked)
                    {
                        configureInvoked = true;
                        widgetBuilder = configure(app!, options);
                    }
                    return await widgetBuilder!(ctx);
                };

                app = new Hex1bApp(wrappedBuilder, options);
                await using (app)
                {
                    await app.RunAsync(ct);
                }
                return 0;
            };

            return new Hex1bTerminalBuildContext(workloadAdapter, runCallback);
        });

        return this;
    }

    /// <summary>
    /// Enables mouse input for the Hex1bApp.
    /// </summary>
    /// <param name="enable">Whether to enable mouse input. Defaults to true.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// This setting only applies when using <see cref="WithHex1bApp(Func{RootContext, Hex1bWidget})"/>
    /// or its async variant. Mouse support requires a compatible terminal emulator.
    /// </remarks>
    public Hex1bTerminalBuilder WithMouse(bool enable = true)
    {
        _enableMouse = enable;
        return this;
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
        _presentationExplicitlyConfigured = true;
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
    /// Configures the terminal to run in headless mode without a real terminal.
    /// This is useful for testing where no actual terminal I/O is needed.
    /// </summary>
    /// <returns>This builder for chaining.</returns>
    /// <remarks>
    /// When running headless, the terminal will use an in-memory buffer for output
    /// and can be controlled via the <see cref="Automation.Hex1bTerminalInputSequenceBuilder"/>.
    /// Use <see cref="WithDimensions"/> to set the terminal size.
    /// </remarks>
    public Hex1bTerminalBuilder WithHeadless()
    {
        _presentationAdapter = null;
        _presentationExplicitlyConfigured = true;
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
        // Default to console presentation when using a factory workload (e.g., WithHex1bApp)
        // and no presentation was explicitly configured
        IHex1bTerminalPresentationAdapter? presentation = _presentationAdapter;
        if (_workloadFactory != null && !_presentationExplicitlyConfigured)
        {
            presentation = new ConsolePresentationAdapter(enableMouse: _enableMouse);
        }

        // Resolve workload
        Func<CancellationToken, Task<int>>? runCallback = null;
        IHex1bTerminalWorkloadAdapter workload;

        if (_workloadFactory != null)
        {
            // Workload is created by factory (e.g., WithHex1bApp, WithShellProcess)
            var context = _workloadFactory(presentation);
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
            presentation: presentation,
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
internal sealed class Hex1bTerminalBuildContext(
    IHex1bTerminalWorkloadAdapter workloadAdapter,
    Func<CancellationToken, Task<int>>? runCallback)
{
    /// <summary>
    /// The workload adapter to use.
    /// </summary>
    public IHex1bTerminalWorkloadAdapter WorkloadAdapter { get; } = workloadAdapter;

    /// <summary>
    /// Optional callback that runs the workload and returns an exit code.
    /// If null, the terminal will wait for the workload to disconnect.
    /// </summary>
    public Func<CancellationToken, Task<int>>? RunCallback { get; } = runCallback;
}
