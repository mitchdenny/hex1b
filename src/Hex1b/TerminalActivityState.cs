using System.Globalization;
using Hex1b.Tokens;

namespace Hex1b;

/// <summary>Immutable activity pair and shared reducer for authoritative and standalone terminals.</summary>
internal sealed record TerminalActivityState(
    TerminalProgress Progress,
    TerminalShellIntegration ShellIntegration,
    TerminalWorkingDirectory WorkingDirectory)
{
    internal static TerminalActivityState Default { get; } =
        new(TerminalProgress.Default, TerminalShellIntegration.Default, TerminalWorkingDirectory.Default);

    internal TerminalActivityState Apply(AnsiToken token) => token switch
    {
        OscToken osc => ApplyOsc(osc.Command, osc.Parameters, osc.Payload),
        RisToken => Default,
        _ => this
    };

    internal TerminalActivityState ApplyOsc(string command, string parameters, string payload)
    {
        if (command == "7")
        {
            var directory = TerminalWorkingDirectory.TryCreate(payload);
            return directory is null ? this : this with { WorkingDirectory = directory };
        }

        if (command == "9" && parameters == "4")
        {
            var separator = payload.IndexOf(';');
            var stateText = separator < 0 ? payload.AsSpan() : payload.AsSpan(0, separator);
            var percentageText = separator < 0 ? default : payload.AsSpan(separator + 1);
            if (stateText.Length != 1 || stateText[0] is < '0' or > '4' ||
                percentageText.Contains(';'))
                return this;

            var state = (TerminalProgressState)(stateText[0] - '0');
            int? percentage = null;
            if (state is TerminalProgressState.Normal or TerminalProgressState.Error or TerminalProgressState.Warning)
            {
                if (!IsAsciiDecimal(percentageText, allowSign: false) ||
                    !int.TryParse(percentageText, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ||
                    value > 100)
                    return this;
                percentage = value;
            }

            return this with { Progress = new TerminalProgress(state, percentage) };
        }

        if (command == "133")
        {
            if (!TryParseMarker(parameters, payload, out var phase, out var exitCode, out _))
                return this;

            if (phase == TerminalShellIntegrationPhase.Finished)
                return this with { ShellIntegration = new TerminalShellIntegration(phase, exitCode) };

            return this with
            {
                ShellIntegration = new TerminalShellIntegration(phase, ShellIntegration.LastExitCode)
            };
        }

        return this;
    }

    /// <summary>
    /// Parses an OSC 133 marker and its optional trailing raw parameter string (e.g.
    /// <c>cmdline_url=...</c>), which is captured verbatim and never interpreted here.
    /// </summary>
    internal static bool TryParseMarker(string parameters, string payload,
        out TerminalShellIntegrationPhase phase, out int? exitCode, out string? rawParameters)
    {
        phase = TerminalShellIntegrationPhase.Unknown;
        exitCode = null;
        rawParameters = null;

        // The tokenizer places a bare marker in Payload, but a marker with an
        // argument in Parameters (including D with an explicitly empty argument).
        var marker = parameters.Length == 0 ? payload : parameters;
        var argument = parameters.Length == 0 ? "" : payload;

        if (marker == "D")
        {
            phase = TerminalShellIntegrationPhase.Finished;
            if (argument.Length == 0)
                return true;

            // Unlike A/B/C, D's argument is strictly an exit code — no trailing raw
            // parameters are recognized here (no known extension uses them on D).
            if (!IsAsciiDecimal(argument, allowSign: true) ||
                !int.TryParse(argument, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
                return false;
            exitCode = value;
            return true;
        }

        phase = marker switch
        {
            "A" => TerminalShellIntegrationPhase.Prompt,
            "B" => TerminalShellIntegrationPhase.CommandLine,
            "C" => TerminalShellIntegrationPhase.Executing,
            _ => TerminalShellIntegrationPhase.Unknown
        };
        if (phase == TerminalShellIntegrationPhase.Unknown)
            return false;
        if (argument.Length != 0)
            rawParameters = argument;
        return true;
    }

    private static bool IsAsciiDecimal(ReadOnlySpan<char> text, bool allowSign)
    {
        if (allowSign && !text.IsEmpty && text[0] is '+' or '-')
            text = text[1..];
        if (text.IsEmpty)
            return false;
        foreach (var character in text)
            if (character is < '0' or > '9')
                return false;
        return true;
    }
}
