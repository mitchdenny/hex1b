using System.CommandLine;
using System.Diagnostics;
using Hex1b.Diagnostics;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Terminal;

/// <summary>
/// Starts a hosted terminal by spawning a detached host process.
/// </summary>
internal sealed class TerminalStartCommand : BaseCommand
{
    private static readonly Option<int> s_widthOption = new("--width") { DefaultValueFactory = _ => 120, Description = "Terminal width" };
    private static readonly Option<int> s_heightOption = new("--height") { DefaultValueFactory = _ => 30, Description = "Terminal height" };
    private static readonly Option<string?> s_cwdOption = new("--cwd") { Description = "Working directory" };
    private static readonly Option<string?> s_recordOption = new("--record") { Description = "Record to asciinema file" };
    private static readonly Argument<string[]> s_commandArgument = new("command") { Description = "Command and arguments to run (after --)" };

    private readonly TerminalClient _client;

    public TerminalStartCommand(
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<TerminalStartCommand> logger)
        : base("start", "Start a hosted terminal", formatter, logger)
    {
        _client = client;

        Options.Add(s_widthOption);
        Options.Add(s_heightOption);
        Options.Add(s_cwdOption);
        Options.Add(s_recordOption);
        Arguments.Add(s_commandArgument);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var width = parseResult.GetValue(s_widthOption);
        var height = parseResult.GetValue(s_heightOption);
        var cwd = parseResult.GetValue(s_cwdOption);
        var record = parseResult.GetValue(s_recordOption);
        var command = parseResult.GetValue(s_commandArgument) ?? ["/bin/bash"];

        // Build args for the host process
        var hostArgs = new List<string> { "terminal", "host", "--width", width.ToString(), "--height", height.ToString() };
        if (cwd != null)
        {
            hostArgs.AddRange(["--cwd", cwd]);
        }
        if (record != null)
        {
            hostArgs.AddRange(["--record", record]);
        }
        hostArgs.AddRange(command);

        // Find our own executable
        var selfExe = Environment.ProcessPath ?? "dotnet";
        var isSelfContained = !string.IsNullOrEmpty(Environment.ProcessPath) &&
                              !Environment.ProcessPath.EndsWith("dotnet", StringComparison.OrdinalIgnoreCase);

        var psi = new ProcessStartInfo
        {
            FileName = isSelfContained ? selfExe : "dotnet",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (!isSelfContained)
        {
            // Running via `dotnet run` or `dotnet hex1b` — need to pass the DLL
            var assemblyLocation = typeof(TerminalStartCommand).Assembly.Location;
            psi.ArgumentList.Add(assemblyLocation);
        }

        foreach (var arg in hostArgs)
        {
            psi.ArgumentList.Add(arg);
        }

        Logger.LogDebug("Spawning host: {FileName} {Args}", psi.FileName, string.Join(" ", psi.ArgumentList));

        var process = Process.Start(psi);
        if (process == null)
        {
            Formatter.WriteError("Failed to start host process");
            return 1;
        }

        // Wait for the socket to appear
        var socketDir = McpDiagnosticsPresentationFilter.GetSocketDirectory();
        var expectedSocket = Path.Combine(socketDir, $"{process.Id}.diagnostics.socket");

        var timeout = TimeSpan.FromSeconds(10);
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            if (process.HasExited)
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                Formatter.WriteError($"Host process exited with code {process.ExitCode}: {stderr.Trim()}");
                return 1;
            }

            if (File.Exists(expectedSocket))
            {
                // Probe to confirm it's ready
                var info = await _client.TryProbeAsync(expectedSocket, cancellationToken);
                if (info is { Success: true })
                {
                    var id = process.Id.ToString();

                    if (parseResult.GetValue(RootCommand.JsonOption))
                    {
                        Formatter.WriteJson(new
                        {
                            id,
                            pid = process.Id,
                            name = info.AppName,
                            width = info.Width,
                            height = info.Height,
                            socketPath = expectedSocket
                        });
                    }
                    else
                    {
                        Formatter.WriteLine($"Terminal started: {id} ({info.AppName}, {info.Width}x{info.Height})");
                    }

                    return 0;
                }
            }

            await Task.Delay(100, cancellationToken);
        }

        Formatter.WriteError("Timeout waiting for host process to start");
        try { process.Kill(); } catch { /* best effort */ }
        return 1;
    }
}
