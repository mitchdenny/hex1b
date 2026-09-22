using System.Diagnostics;
using System.Net.WebSockets;
using Hex1b.Automation;
using Hex1b.Widgets;

namespace Hex1b;

/// <summary>
/// Options for configuring a child process workload.
/// </summary>
public sealed class Hex1bTerminalProcessOptions
{
    private TimeSpan _unixPtyStartupTimeout = UnixPtyStartupOptions.DefaultTimeout;

    /// <summary>
    /// Gets or sets the Unix PTY startup-handshake timeout. Defaults to 10 seconds.
    /// </summary>
    /// <remarks>
    /// Accepts any positive duration or <see cref="Timeout.InfiniteTimeSpan"/>.
    /// Infinite waiting remains cancellable. This bounds the Unix pre-exec/exec handshake,
    /// not shell-prompt or application readiness. It does not affect Windows or redirected
    /// processes. Cleanup may outlast the deadline when kernel operations block.
    /// The builder snapshots this value when process configuration completes.
    /// This experimental option's name, scope, and policy may change.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero or negative other than infinite.</exception>
    [System.Diagnostics.CodeAnalysis.Experimental("HEX1B_UNIX_PTY_STARTUP")]
    public TimeSpan UnixPtyStartupTimeout
    {
        get => _unixPtyStartupTimeout;
        set => _unixPtyStartupTimeout = UnixPtyStartupOptions.ValidateTimeout(value);
    }

    /// <summary>
    /// Gets or sets the executable to run.
    /// </summary>
    public string FileName { get; set; } = "";

    /// <summary>
    /// Gets or sets the command-line arguments for the process.
    /// </summary>
    public IList<string>? Arguments { get; set; }

    /// <summary>
    /// Gets or sets the working directory for the process.
    /// If null, uses the current directory.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Gets or sets additional environment variables for the process.
    /// </summary>
    public IDictionary<string, string>? Environment { get; set; }

    /// <summary>
    /// Gets or sets whether to inherit the parent's environment variables.
    /// Defaults to true.
    /// </summary>
    public bool InheritEnvironment { get; set; } = true;

    /// <summary>
    /// Gets or sets how Hex1b should choose the Windows PTY backend.
    /// </summary>
    /// <remarks>
    /// This only applies on Windows. Other platforms always use the Unix PTY implementation.
    /// The default is <see cref="Hex1b.WindowsPtyMode.RequireProxy"/>, which uses the
    /// out-of-process <c>hex1bpty.exe</c> helper and fails if it is unavailable.
    /// Set this to <see cref="Hex1b.WindowsPtyMode.Direct"/> to bypass the helper and
    /// use the in-process ConPTY implementation directly.
    /// </remarks>
    public WindowsPtyMode WindowsPtyMode { get; set; } = WindowsPtyMode.RequireProxy;

    /// <summary>
    /// Gets or sets an explicit path to the Windows PTY host executable (<c>hex1bpty.exe</c>).
    /// </summary>
    /// <remarks>
    /// This only applies when <see cref="WindowsPtyMode"/> is <see cref="Hex1b.WindowsPtyMode.RequireProxy"/>.
    /// When null, Hex1b searches the application output and packaged runtime locations automatically.
    /// </remarks>
    public string? WindowsPtyHostPath { get; set; }
}
