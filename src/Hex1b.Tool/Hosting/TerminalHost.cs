namespace Hex1b.Tool.Hosting;

/// <summary>
/// Runs a headless HexlbTerminal with a PTY child process and MCP diagnostics.
/// This is the core of the "terminal host" command.
/// </summary>
internal sealed class TerminalHost
{
    /// <summary>
    /// Runs the host, blocking until the child process exits or cancellation is requested.
    /// After exit, keeps diagnostics alive for capture until shutdown is requested.
    /// </summary>
    public static async Task<int> RunAsync(TerminalHostConfig config, CancellationToken cancellationToken)
    {
        var builder = Hex1bTerminal.CreateBuilder()
            .WithDimensions(config.Width, config.Height)
            .WithHeadless()
            .WithDiagnostics(appName: config.Command, forceEnable: true);

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

        if (config.RecordPath != null)
        {
            builder.WithAsciinemaRecording(config.RecordPath);
        }

        await using var terminal = builder.Build();

        int exitCode = 0;
        try
        {
            exitCode = await terminal.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Explicit shutdown requested — exit immediately
            return 0;
        }

        // Child process exited but no explicit shutdown — keep diagnostics alive
        // so the final terminal state can still be captured.
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested
        }

        return exitCode;
    }
}
