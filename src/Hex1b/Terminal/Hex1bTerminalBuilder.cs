using System.Diagnostics;
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

    // === Process Workloads ===

    /// <summary>
    /// Configures the terminal to run an arbitrary process using standard .NET process primitives.
    /// </summary>
    /// <param name="fileName">The executable to run.</param>
    /// <param name="arguments">Command-line arguments for the process.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method uses standard .NET <see cref="Process"/> with stream redirection.
    /// Programs won't detect a TTY (isatty() returns false), so this is suitable for:
    /// </para>
    /// <list type="bullet">
    ///   <item>Build tools (dotnet, make, cargo)</item>
    ///   <item>Command-line utilities (grep, find, curl)</item>
    ///   <item>Scripts and batch processing</item>
    /// </list>
    /// <para>
    /// For programs requiring a real terminal (vim, tmux, interactive shells),
    /// use <see cref="WithPtyProcess(string, string[])"/> instead.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await Hex1bTerminal.CreateBuilder()
    ///     .WithProcess("dotnet", "build")
    ///     .WithHeadless()
    ///     .RunAsync();
    /// </code>
    /// </example>
    public Hex1bTerminalBuilder WithProcess(string fileName, params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        return WithProcess(startInfo);
    }

    /// <summary>
    /// Configures the terminal to run a shell process with full PTY (pseudo-terminal) support.
    /// </summary>
    /// <param name="shell">
    /// The shell to run. Examples: "/bin/bash", "/bin/zsh", "pwsh", "cmd.exe".
    /// If null, uses the default shell for the platform.
    /// </param>
    /// <param name="arguments">Optional arguments to pass to the shell.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method uses PTY (pseudo-terminal) emulation for full shell support:
    /// </para>
    /// <list type="bullet">
    ///   <item>Job control (Ctrl+C, Ctrl+Z, background processes)</item>
    ///   <item>Terminal resize events (SIGWINCH)</item>
    ///   <item>TTY detection (isatty() returns true)</item>
    ///   <item>Interactive features (history, line editing)</item>
    /// </list>
    /// <para>
    /// Requires native library support on Unix platforms.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await Hex1bTerminal.CreateBuilder()
    ///     .WithPtyShell("/bin/bash", "-l")
    ///     .RunAsync();
    /// </code>
    /// </example>
    public Hex1bTerminalBuilder WithPtyShell(string? shell = null, params string[] arguments)
    {
        var shellPath = shell ?? GetDefaultShell();
        return WithPtyProcess(shellPath, arguments);
    }

    /// <summary>
    /// Configures the terminal to run an arbitrary process with a PTY (pseudo-terminal) attached.
    /// </summary>
    /// <param name="fileName">The executable to run.</param>
    /// <param name="arguments">Command-line arguments for the process.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Use this for programs that require a real terminal:
    /// </para>
    /// <list type="bullet">
    ///   <item>Interactive editors (vim, nano, emacs)</item>
    ///   <item>Terminal multiplexers (tmux, screen)</item>
    ///   <item>Programs using ncurses or similar TUI libraries</item>
    ///   <item>Programs that detect TTY for colorized output</item>
    /// </list>
    /// <para>
    /// Requires native library support on Unix platforms.
    /// For simple command-line tools, use <see cref="WithProcess(string, string[])"/> instead.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await Hex1bTerminal.CreateBuilder()
    ///     .WithPtyProcess("htop")
    ///     .RunAsync();
    /// </code>
    /// </example>
    public Hex1bTerminalBuilder WithPtyProcess(string fileName, params string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        SetWorkloadFactory(presentation =>
        {
            var width = presentation?.Width ?? _width;
            var height = presentation?.Height ?? _height;

            var process = new Hex1bTerminalChildProcess(
                fileName,
                arguments,
                workingDirectory: null,
                environment: null,
                inheritEnvironment: true,
                initialWidth: width,
                initialHeight: height);

            Func<CancellationToken, Task<int>> runCallback = async ct =>
            {
                await process.StartAsync(ct);
                return await process.WaitForExitAsync(ct);
            };

            return new Hex1bTerminalBuildContext(process, runCallback);
        });

        return this;
    }

    /// <summary>
    /// Configures the terminal to run a PTY process with full options.
    /// </summary>
    /// <param name="configure">Action to configure the process options.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method provides full control over PTY process configuration including
    /// working directory, environment variables, and whether to inherit the parent
    /// environment. Use this for advanced scenarios requiring custom process setup.
    /// </para>
    /// <para>
    /// Requires native library support on Unix platforms.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await Hex1bTerminal.CreateBuilder()
    ///     .WithPtyProcess(options =>
    ///     {
    ///         options.FileName = "/bin/bash";
    ///         options.Arguments = ["-l"];
    ///         options.WorkingDirectory = "/home/user";
    ///         options.Environment["TERM"] = "xterm-256color";
    ///     })
    ///     .RunAsync();
    /// </code>
    /// </example>
    public Hex1bTerminalBuilder WithPtyProcess(Action<Hex1bTerminalProcessOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new Hex1bTerminalProcessOptions();
        configure(options);

        if (string.IsNullOrEmpty(options.FileName))
            throw new InvalidOperationException("FileName must be set in process options.");

        SetWorkloadFactory(presentation =>
        {
            var width = presentation?.Width ?? _width;
            var height = presentation?.Height ?? _height;

            var process = new Hex1bTerminalChildProcess(
                options.FileName,
                options.Arguments?.ToArray() ?? [],
                workingDirectory: options.WorkingDirectory,
                environment: options.Environment?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                inheritEnvironment: options.InheritEnvironment,
                initialWidth: width,
                initialHeight: height);

            Func<CancellationToken, Task<int>> runCallback = async ct =>
            {
                await process.StartAsync(ct);
                return await process.WaitForExitAsync(ct);
            };

            return new Hex1bTerminalBuildContext(process, runCallback);
        });

        return this;
    }

    /// <summary>
    /// Configures the terminal to run a process using standard .NET process primitives.
    /// </summary>
    /// <param name="startInfo">The process start info.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This overload uses standard .NET <see cref="ProcessStartInfo"/> and process redirection
    /// rather than PTY (pseudo-terminal) emulation. This means:
    /// </para>
    /// <list type="bullet">
    ///   <item>No native library dependencies - works on all .NET platforms</item>
    ///   <item>Programs won't detect a TTY (isatty() returns false)</item>
    ///   <item>Terminal resize (SIGWINCH) is not supported</item>
    ///   <item>Programs like vim, tmux, and screen won't work correctly</item>
    /// </list>
    /// <para>
    /// Use this for simple command-line programs, build tools, and non-interactive applications.
    /// For full terminal emulation, use <see cref="WithPtyProcess(string, string[])"/> or 
    /// <see cref="WithPtyShell(string?, string[])"/> which use PTY.
    /// </para>
    /// <para>
    /// The following properties will be set automatically:
    /// <see cref="ProcessStartInfo.RedirectStandardInput"/>,
    /// <see cref="ProcessStartInfo.RedirectStandardOutput"/>,
    /// <see cref="ProcessStartInfo.RedirectStandardError"/>,
    /// <see cref="ProcessStartInfo.UseShellExecute"/> (set to false),
    /// <see cref="ProcessStartInfo.CreateNoWindow"/> (set to true).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var startInfo = new ProcessStartInfo("dotnet", "build")
    /// {
    ///     WorkingDirectory = "/path/to/project"
    /// };
    /// 
    /// await Hex1bTerminal.CreateBuilder()
    ///     .WithProcess(startInfo)
    ///     .WithHeadless()
    ///     .RunAsync();
    /// </code>
    /// </example>
    public Hex1bTerminalBuilder WithProcess(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        SetWorkloadFactory(presentation =>
        {
            var adapter = new StandardProcessWorkloadAdapter(startInfo);

            Func<CancellationToken, Task<int>> runCallback = async ct =>
            {
                await adapter.StartAsync(ct);
                return await adapter.WaitForExitAsync(ct);
            };

            return new Hex1bTerminalBuildContext(adapter, runCallback);
        });

        return this;
    }

    private static string GetDefaultShell()
    {
        if (OperatingSystem.IsWindows())
            return Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe";
        
        // Unix: try SHELL env var, fallback to /bin/sh
        return Environment.GetEnvironmentVariable("SHELL") ?? "/bin/sh";
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

    // === Recording and Optimization ===

    /// <summary>
    /// Adds Asciinema recording to capture the terminal session.
    /// </summary>
    /// <param name="filePath">Path to the output file (typically with .cast extension).</param>
    /// <param name="options">Optional recording options.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Asciinema recordings capture terminal output (and optionally input) in the
    /// asciicast v2 format, compatible with the Asciinema player and ecosystem.
    /// </para>
    /// <para>
    /// The recording is automatically flushed when the terminal is disposed.
    /// Use the overload with a capture callback if you need access to the recorder
    /// instance for adding markers or manual flush control.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await Hex1bTerminal.CreateBuilder()
    ///     .WithPtyShell("/bin/bash")
    ///     .WithAsciinemaRecording("session.cast")
    ///     .RunAsync();
    /// </code>
    /// </example>
    /// <seealso href="https://docs.asciinema.org/manual/asciicast/v2/"/>
    public Hex1bTerminalBuilder WithAsciinemaRecording(string filePath, AsciinemaRecorderOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var recorder = new AsciinemaRecorder(filePath, options);
        _workloadFilters.Add(recorder);
        return this;
    }

    /// <summary>
    /// Adds Asciinema recording with access to the recorder instance.
    /// </summary>
    /// <param name="filePath">Path to the output file (typically with .cast extension).</param>
    /// <param name="capture">Callback that receives the recorder instance for external control.</param>
    /// <param name="options">Optional recording options.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need access to the recorder instance for:
    /// </para>
    /// <list type="bullet">
    ///   <item>Adding markers at specific points: <c>recorder.AddMarker("step1")</c></item>
    ///   <item>Manual flush control: <c>await recorder.FlushAsync()</c></item>
    ///   <item>Checking pending event count: <c>recorder.PendingEventCount</c></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// AsciinemaRecorder? recorder = null;
    /// 
    /// await Hex1bTerminal.CreateBuilder()
    ///     .WithPtyShell("/bin/bash")
    ///     .WithAsciinemaRecording("session.cast", r => recorder = r)
    ///     .RunAsync();
    /// 
    /// // Add markers during execution
    /// recorder?.AddMarker("command-executed");
    /// </code>
    /// </example>
    public Hex1bTerminalBuilder WithAsciinemaRecording(
        string filePath,
        Action<AsciinemaRecorder> capture,
        AsciinemaRecorderOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(capture);

        var recorder = new AsciinemaRecorder(filePath, options);
        capture(recorder);
        _workloadFilters.Add(recorder);
        return this;
    }

    /// <summary>
    /// Adds render optimization for Hex1bApp workloads.
    /// </summary>
    /// <returns>This builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This filter optimizes rendering by only transmitting cells that have changed
    /// since the last frame. It's specifically designed for Hex1bApp workloads and
    /// understands the frame boundary tokens emitted by Hex1bApp.
    /// </para>
    /// <para>
    /// Benefits:
    /// </para>
    /// <list type="bullet">
    ///   <item>Reduces bandwidth for remote terminal connections</item>
    ///   <item>Improves rendering performance by avoiding redundant updates</item>
    ///   <item>Eliminates flicker from intermediate render states</item>
    /// </list>
    /// <para>
    /// This filter is automatically added when using <see cref="WithHex1bApp(Func{RootContext, Hex1bWidget})"/>
    /// with a real presentation adapter.
    /// </para>
    /// </remarks>
    public Hex1bTerminalBuilder WithRenderOptimization()
    {
        _presentationFilters.Add(new Hex1bAppRenderOptimizationFilter());
        return this;
    }

    /// <summary>
    /// Adds render optimization for Hex1bApp workloads with access to the filter instance.
    /// </summary>
    /// <param name="capture">Callback that receives the filter instance.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    public Hex1bTerminalBuilder WithRenderOptimization(Action<Hex1bAppRenderOptimizationFilter> capture)
    {
        ArgumentNullException.ThrowIfNull(capture);
        var filter = new Hex1bAppRenderOptimizationFilter();
        capture(filter);
        _presentationFilters.Add(filter);
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

/// <summary>
/// Options for configuring a child process workload.
/// </summary>
public sealed class Hex1bTerminalProcessOptions
{
    /// <summary>
    /// Gets or sets the executable to run.
    /// </summary>
    public string FileName { get; set; } = "";

    /// <summary>
    /// Gets or sets the command-line arguments for the process.
    /// </summary>
    public IList<string>? Arguments { get; set; }

    /// <summary>
    /// Gets or sets the working directory for the process.
    /// If null, uses the current directory.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Gets or sets additional environment variables for the process.
    /// </summary>
    public IDictionary<string, string>? Environment { get; set; }

    /// <summary>
    /// Gets or sets whether to inherit the parent's environment variables.
    /// Defaults to true.
    /// </summary>
    public bool InheritEnvironment { get; set; } = true;
}
