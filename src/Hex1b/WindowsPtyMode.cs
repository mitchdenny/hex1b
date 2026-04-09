namespace Hex1b;

/// <summary>
/// Controls how Hex1b chooses the Windows PTY backend for <see cref="Hex1bTerminalBuilder.WithPtyProcess(Action{Hex1bTerminalProcessOptions})"/>.
/// </summary>
/// <remarks>
/// This setting only applies on Windows. Linux and macOS always use the Unix PTY implementation.
/// </remarks>
public enum WindowsPtyMode
{
    /// <summary>
    /// Bypass <c>hex1bpty.exe</c> and use the in-process Windows PTY implementation directly.
    /// </summary>
    Direct = 0,

    /// <summary>
    /// Require the out-of-process <c>hex1bpty.exe</c> helper and fail if it cannot
    /// be resolved or started. This is the default.
    /// </summary>
    RequireProxy = 1
}
