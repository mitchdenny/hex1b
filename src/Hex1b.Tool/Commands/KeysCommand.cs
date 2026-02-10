using System.CommandLine;
using Hex1b.Diagnostics;
using Hex1b.Tool.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Hex1b.Tool.Commands;

/// <summary>
/// Sends keystrokes to a terminal.
/// </summary>
internal sealed class KeysCommand : BaseCommand
{
    private readonly TerminalIdResolver _resolver;
    private readonly TerminalClient _client;

    private static readonly Argument<string> s_idArgument = new("id") { Description = "Terminal ID (or prefix)" };
    private static readonly Option<string?> s_keyOption = new("--key") { Description = "Named key (Enter, Tab, Escape, F1, UpArrow, etc.)" };
    private static readonly Option<string?> s_textOption = new("--text") { Description = "Type text as keystrokes" };
    private static readonly Option<bool> s_ctrlOption = new("--ctrl") { Description = "Ctrl modifier" };
    private static readonly Option<bool> s_shiftOption = new("--shift") { Description = "Shift modifier" };
    private static readonly Option<bool> s_altOption = new("--alt") { Description = "Alt modifier" };

    public KeysCommand(
        TerminalIdResolver resolver,
        TerminalClient client,
        OutputFormatter formatter,
        ILogger<KeysCommand> logger)
        : base("keys", "Send keystrokes to a terminal", formatter, logger)
    {
        _resolver = resolver;
        _client = client;

        Arguments.Add(s_idArgument);
        Options.Add(s_keyOption);
        Options.Add(s_textOption);
        Options.Add(s_ctrlOption);
        Options.Add(s_shiftOption);
        Options.Add(s_altOption);
    }

    protected override async Task<int> ExecuteAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var id = parseResult.GetValue(s_idArgument)!;
        var key = parseResult.GetValue(s_keyOption);
        var text = parseResult.GetValue(s_textOption);
        var ctrl = parseResult.GetValue(s_ctrlOption);
        var shift = parseResult.GetValue(s_shiftOption);
        var alt = parseResult.GetValue(s_altOption);

        if (key == null && text == null)
        {
            Formatter.WriteError("Specify --key or --text");
            return 1;
        }

        var resolved = _resolver.Resolve(id);
        if (!resolved.Success)
        {
            Formatter.WriteError(resolved.Error!);
            return 1;
        }

        if (text != null)
        {
            var response = await _client.SendAsync(resolved.SocketPath!,
                new DiagnosticsRequest { Method = "input", Data = text }, cancellationToken);

            if (!response.Success)
            {
                Formatter.WriteError(response.Error ?? "Failed to send text");
                return 1;
            }
        }

        if (key != null)
        {
            var modifiers = new List<string>();
            if (ctrl) modifiers.Add("Ctrl");
            if (shift) modifiers.Add("Shift");
            if (alt) modifiers.Add("Alt");

            var response = await _client.SendAsync(resolved.SocketPath!,
                new DiagnosticsRequest
                {
                    Method = "key",
                    Key = key,
                    Modifiers = modifiers.Count > 0 ? modifiers.ToArray() : null
                }, cancellationToken);

            if (!response.Success)
            {
                Formatter.WriteError(response.Error ?? "Failed to send key");
                return 1;
            }
        }

        return 0;
    }
}
