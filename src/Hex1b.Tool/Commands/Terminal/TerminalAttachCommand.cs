using System.CommandLine;
using System.Runtime.Versioning;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands.Terminal;

/// <summary>
/// Attaches to a terminal, streaming its output to the local terminal
/// and forwarding local input to it. Ctrl+] to enter command mode.
/// </summary>
internal sealed class TerminalAttachCommand : BaseCommand
{
    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };
    private static readonly Option<bool> s_resizeOption = new("--resize") { Description = "Resize remote terminal to match local terminal dimensions" };
    private static readonly Option<bool> s_leadOption = new("--lead") { Description = "Claim resize leadership (only the leader's resize events control the remote terminal)" };

    private readonly TerminalIdResolver _resolver;
    private readonly TerminalClient _client;

    public TerminalAttachCommand(
        TerminalIdResolver resolver,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<TerminalAttachCommand> logger)
        : base("attach", "Attach to a terminal (Ctrl+] for commands)", formatter, logger)
    {
        _resolver = resolver;
        _client = client;

        Arguments.Add(s_idArgument);
        Options.Add(s_resizeOption);
        Options.Add(s_leadOption);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Formatter.WriteError("Attach is only supported on Linux and macOS");
            return 1;
        }

        var id = parseResult.GetValue(s_idArgument)!;
        var resize = parseResult.GetValue(s_resizeOption);
        var lead = parseResult.GetValue(s_leadOption);

        var resolved = _resolver.Resolve(id);
        if (!resolved.Success)
        {
            Formatter.WriteError(resolved.Error!);
            return 1;
        }

        return await RunAttachAsync(resolved.SocketPath!, resolved.Id!, _client, resize, lead, cancellationToken);
    }

    /// <summary>
    /// Core attach logic, usable from both the attach command and terminal start --attach.
    /// </summary>
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    internal static async Task<int> RunAttachAsync(
        string socketPath, string displayId, TerminalClient client,
        bool resize, bool lead, CancellationToken cancellationToken)
    {
        await using var app = new AttachTuiApp(socketPath, displayId, client, resize, lead);
        return await app.RunAsync(cancellationToken);
    }
}
