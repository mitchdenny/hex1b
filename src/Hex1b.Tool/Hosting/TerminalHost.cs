namespace Hex1b.Tool.Hosting;

/// <summary>
/// Runs a headless HexlbTerminal with a PTY child process and MCP diagnostics.
/// This is the core of the "terminal host" command.
/// </summary>
internal sealed class TerminalHost
{
    /// <summary>
    /// Routes to <see cref="RunPassthruAsync"/> or <see cref="RunAsync"/> based on config.
    /// </summary>
    public static Task<int> RunConfiguredAsync(TerminalHostConfig config, CancellationToken cancellationToken)
        => config.Passthru
            ? RunPassthruAsync(config, cancellationToken)
            : RunAsync(config, cancellationToken);

    /// <summary>
    /// Runs in passthru mode: the PTY child's I/O bridges directly to the current terminal's
    /// stdin/stdout with no TUI chrome. The outer terminal defines resolution. Diagnostics
    /// are still available for remote attach.
    /// </summary>
    public static async Task<int> RunPassthruAsync(TerminalHostConfig config, CancellationToken cancellationToken)
    {
        var diagnosticsFilter = CreateDiagnosticsFilter(config);

        var builder = Hex1bTerminal.CreateBuilder();

        if (diagnosticsFilter != null)
            builder.AddPresentationFilter(diagnosticsFilter);
        else
            builder.WithDiagnostics(appName: config.Command, forceEnable: true);

        ConfigurePtyProcess(builder, config);

        if (config.RecordPath != null)
        {
            builder.WithAsciinemaRecording(config.RecordPath);
        }

        await using var terminal = builder.Build();

        WebSocketDiagnosticsListener? wsListener = null;
        try
        {
            wsListener = await StartWebSocketListenerAsync(config, diagnosticsFilter, cancellationToken);

            return await terminal.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        finally
        {
            if (wsListener != null)
                await wsListener.DisposeAsync();
        }
    }

    /// <summary>
    /// Runs the host, blocking until the child process exits or cancellation is requested.
    /// After exit, keeps diagnostics alive for capture until shutdown is requested.
    /// </summary>
    public static async Task<int> RunAsync(TerminalHostConfig config, CancellationToken cancellationToken)
    {
        var diagnosticsFilter = CreateDiagnosticsFilter(config);

        var builder = Hex1bTerminal.CreateBuilder()
            .WithDimensions(config.Width, config.Height)
            .WithHeadless();

        if (diagnosticsFilter != null)
            builder.AddPresentationFilter(diagnosticsFilter);
        else
            builder.WithDiagnostics(appName: config.Command, forceEnable: true);

        ConfigurePtyProcess(builder, config);

        if (config.RecordPath != null)
        {
            builder.WithAsciinemaRecording(config.RecordPath);
        }

        await using var terminal = builder.Build();

        WebSocketDiagnosticsListener? wsListener = null;
        int exitCode = 0;
        try
        {
            wsListener = await StartWebSocketListenerAsync(config, diagnosticsFilter, cancellationToken);

            exitCode = await terminal.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Explicit shutdown requested — exit immediately
            return 0;
        }

        // Child process exited but no explicit shutdown — keep diagnostics alive
        // (both Unix socket AND WebSocket) so the final terminal state can still be captured.
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested
        }
        finally
        {
            if (wsListener != null)
                await wsListener.DisposeAsync();
        }

        return exitCode;
    }

    /// <summary>
    /// Creates the diagnostics filter manually when a WebSocket port is configured,
    /// so we retain a reference to pass to the WebSocket listener.
    /// Returns null when no port is configured (caller should use WithDiagnostics instead).
    /// </summary>
    private static Hex1b.Diagnostics.McpDiagnosticsPresentationFilter? CreateDiagnosticsFilter(TerminalHostConfig config)
    {
        if (!config.Port.HasValue)
            return null;

        return new Hex1b.Diagnostics.McpDiagnosticsPresentationFilter(config.Command);
    }

    /// <summary>
    /// Starts the WebSocket listener if port is configured and filter is available.
    /// </summary>
    private static async Task<WebSocketDiagnosticsListener?> StartWebSocketListenerAsync(
        TerminalHostConfig config,
        Hex1b.Diagnostics.McpDiagnosticsPresentationFilter? filter,
        CancellationToken cancellationToken)
    {
        if (!config.Port.HasValue || filter == null)
            return null;

        var listener = new WebSocketDiagnosticsListener(config.Port.Value, filter);
        await listener.StartAsync(cancellationToken);
        return listener;
    }

    private static void ConfigurePtyProcess(Hex1bTerminalBuilder builder, TerminalHostConfig config)
    {
        if (config.WorkingDirectory != null)
        {
            builder.WithPtyProcess(options =>
            {
                options.FileName = config.Command;
                options.Arguments = config.Arguments;
                options.WorkingDirectory = config.WorkingDirectory;
            });
        }
        else
        {
            builder.WithPtyProcess(config.Command, config.Arguments);
        }
    }
}
