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

    /// <summary>
    /// When true, runs in passthru mode: PTY bridges directly to the current terminal's
    /// stdin/stdout with no chrome. The outer terminal defines resolution and controls screen size.
    /// </summary>
    public bool Passthru { get; set; }

    /// <summary>
    /// Optional port for a WebSocket diagnostics listener. When set, the host exposes
    /// itself over HTTP WebSocket in addition to the Unix domain socket.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Bind address for the WebSocket listener. Defaults to loopback (127.0.0.1).
    /// Set to "0.0.0.0" or "*" to listen on all interfaces (e.g. for container scenarios).
    /// </summary>
    public string? BindAddress { get; set; }
}
