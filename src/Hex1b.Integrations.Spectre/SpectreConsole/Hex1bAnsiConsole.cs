using Hex1b;
using Spectre.Console;

namespace Hex1b.Integrations.Spectre.SpectreConsole;

/// <summary>
/// Factory for creating an <see cref="IAnsiConsole"/> wired to a Hex1b
/// <see cref="IHex1bAppTerminalWorkloadAdapter"/>.
/// </summary>
/// <remarks>
/// <para>
/// The returned console writes its ANSI output through the workload adapter
/// (which Hex1b can record, mux, present, or embed) and reads its key input
/// from the same adapter's input channel.
/// </para>
/// <para>
/// In most cases callers should use the <c>WithSpectreConsole</c> extension
/// on a <see cref="Hex1bTerminalBuilder"/> instead of calling this factory
/// directly — the builder takes care of creating the workload adapter,
/// running the TUI mode sequences, and integrating with the rest of the
/// terminal pipeline.
/// </para>
/// </remarks>
public static class Hex1bAnsiConsole
{
    /// <summary>
    /// Creates an <see cref="IAnsiConsole"/> backed by the supplied
    /// <see cref="IHex1bAppTerminalWorkloadAdapter"/>.
    /// </summary>
    /// <param name="adapter">The workload adapter to bridge.</param>
    /// <param name="settings">
    /// Optional settings overrides. <see cref="AnsiConsoleSettings.Out"/> will
    /// be replaced with the bridge output regardless of what the caller
    /// supplied; <see cref="AnsiConsoleSettings.Ansi"/> defaults to
    /// <see cref="AnsiSupport.Yes"/> and
    /// <see cref="AnsiConsoleSettings.Interactive"/> defaults to
    /// <see cref="InteractionSupport.Yes"/> when not specified.
    /// </param>
    public static IAnsiConsole Create(
        IHex1bAppTerminalWorkloadAdapter adapter,
        AnsiConsoleSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(adapter);

        var output = new Hex1bAnsiConsoleOutput(adapter);
        var input = new Hex1bAnsiConsoleInput(adapter);

        var effectiveSettings = settings ?? new AnsiConsoleSettings();
        effectiveSettings.Out = output;
        if (effectiveSettings.Ansi == AnsiSupport.Detect)
        {
            effectiveSettings.Ansi = AnsiSupport.Yes;
        }

        if (effectiveSettings.Interactive == InteractionSupport.Detect)
        {
            effectiveSettings.Interactive = InteractionSupport.Yes;
        }

        var inner = AnsiConsole.Create(effectiveSettings);
        return new Hex1bAnsiConsoleFacade(inner, input);
    }
}
