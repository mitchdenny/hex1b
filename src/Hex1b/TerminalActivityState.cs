using System.Globalization;
using Hex1b.Tokens;

namespace Hex1b;

/// <summary>Immutable activity pair and shared reducer for authoritative and standalone terminals.</summary>
internal sealed record TerminalActivityState(
    TerminalProgress Progress,
    TerminalShellIntegration ShellIntegration)
{
    internal static TerminalActivityState Default { get; } =
        new(TerminalProgress.Default, TerminalShellIntegration.Default);

    internal TerminalActivityState Apply(AnsiToken token) => token switch
    {
        OscToken osc => ApplyOsc(osc.Command, osc.Parameters, osc.Payload),
        RisToken => Default,
        _ => this
    };

    internal TerminalActivityState ApplyOsc(string command, string parameters, string payload)
    {
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
            // The tokenizer places a bare marker in Payload, but a marker with an
            // argument in Parameters (including D with an explicitly empty argument).
            var marker = parameters.Length == 0 ? payload : parameters;
            if (marker == "D")
            {
                int? exitCode = null;
                if (parameters.Length != 0 && payload.Length != 0)
                {
                    if (!IsAsciiDecimal(payload, allowSign: true) ||
                        !int.TryParse(payload, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
                        return this;
                    exitCode = value;
                }
                return this with
                {
                    ShellIntegration = new TerminalShellIntegration(TerminalShellIntegrationPhase.Finished, exitCode)
                };
            }

            if (parameters.Length != 0)
                return this;
            var phase = marker switch
            {
                "A" => TerminalShellIntegrationPhase.Prompt,
                "B" => TerminalShellIntegrationPhase.CommandLine,
                "C" => TerminalShellIntegrationPhase.Executing,
                _ => TerminalShellIntegrationPhase.Unknown
            };
            if (phase != TerminalShellIntegrationPhase.Unknown)
                return this with
                {
                    ShellIntegration = new TerminalShellIntegration(phase, ShellIntegration.LastExitCode)
                };
        }

        return this;
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
