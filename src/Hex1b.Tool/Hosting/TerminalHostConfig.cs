namespace Hex1b.Tool.Hosting;

/// <summary>
/// Configuration for a terminal host process.
/// </summary>
internal sealed class TerminalHostConfig
{
    /// <summary>
    /// The command to run (e.g., "/bin/bash").
    /// </summary>
    public string Command { get; set; } = "/bin/bash";

    /// <summary>
    /// Arguments for the command.
    /// </summary>
    public string[] Arguments { get; set; } = [];

    /// <summary>
    /// Terminal width in columns.
    /// </summary>
    public int Width { get; set; } = 120;

    /// <summary>
    /// Terminal height in rows.
    /// </summary>
    public int Height { get; set; } = 30;

    /// <summary>
    /// Working directory for the child process.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Optional path to record the session as asciinema .cast file.
    /// </summary>
    public string? RecordPath { get; set; }
}
