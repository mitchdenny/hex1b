using Hex1b.Layout;

namespace Hex1b.Automation;

/// <summary>Configures playback, capture, and optional default-shell launch settings.</summary>
/// <remarks>Shell launch settings apply only to the player's default owned shell, not borrowed terminals or builder-configured workloads.</remarks>
public sealed class TapePlaybackOptions
{
    /// <summary>Gets the factory for a player-owned terminal. Null builds the default headless shell terminal.</summary>
    /// <remarks>
    /// Configure the supplied builder and return its <see cref="Hex1bTerminalBuilder.Build"/> result.
    /// Build it exactly once and do not start it: the player validates before construction, attaches capture,
    /// then starts and ultimately disposes the terminal and workload. The factory runs once per playback,
    /// never during validation, and is ignored when an existing terminal is supplied explicitly.
    /// </remarks>
    public Func<Hex1bTerminalBuilder, Hex1bTerminal>? TerminalFactory { get; init; }

    /// <summary>Gets the base directory for includes, output paths, and the default owned shell. Defaults to the current directory at preparation time.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Gets an explicit terminal size in columns and rows. Null preserves borrowed-terminal or builder dimensions.</summary>
    public Size? TerminalSize { get; init; }

    /// <summary>Gets the additional output artifacts to capture.</summary>
    public TapeCaptureOptions? Capture { get; init; }

    /// <summary>Gets the fallback VHS shell name for the default owned workload. Tape shell declarations take precedence.</summary>
    public string? DefaultShell { get; init; }

    /// <summary>Gets initial environment values for the default owned shell. Tape environment declarations take precedence.</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>Gets whether the default owned shell inherits the host environment without modifying it.</summary>
    public bool InheritEnvironment { get; init; } = true;
}
