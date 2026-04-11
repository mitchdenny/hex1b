namespace Hex1b;

/// <summary>
/// Allows replacing Hex1b's built-in terminal emulator with an external implementation.
/// When set via <see cref="Hex1bTerminalBuilder.WithTerminalEmulator"/>, the backend
/// processes raw workload output instead of the built-in AnsiTokenizer + ApplyTokens pipeline.
/// </summary>
public interface ITerminalEmulatorBackend : IDisposable
{
    /// <summary>
    /// Process raw output bytes from the workload.
    /// Called instead of the built-in AnsiTokenizer + ApplyTokens pipeline.
    /// </summary>
    void ProcessOutput(ReadOnlySpan<byte> data);

    /// <summary>
    /// Handle terminal resize.
    /// </summary>
    void Resize(int width, int height);

    /// <summary>
    /// Get the current screen buffer state as a grid of <see cref="TerminalCell"/>.
    /// Called by <see cref="Hex1bTerminal.CreateSnapshot()"/> and related methods.
    /// </summary>
    TerminalCell[,] GetScreenBuffer(int width, int height);

    /// <summary>Current cursor column (0-based).</summary>
    int CursorX { get; }

    /// <summary>Current cursor row (0-based).</summary>
    int CursorY { get; }

    /// <summary>Whether the terminal is in alternate screen mode.</summary>
    bool InAlternateScreen { get; }

    /// <summary>The terminal title (set via OSC 0/2). Null if not set.</summary>
    string? Title { get; }
}
