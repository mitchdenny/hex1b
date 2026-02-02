using System.Collections.Concurrent;

namespace Hex1b.McpServer;

/// <summary>
/// Manages multiple terminal sessions with thread-safe operations.
/// Supports both local sessions (launched by MCP server) and remote targets (connected via UDS).
/// </summary>
public sealed class TerminalSessionManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, TerminalSession> _sessions = new();
    private readonly ConcurrentDictionary<string, ITerminalTarget> _targets = new();
    private bool _disposed;

    /// <summary>
    /// Gets the number of active sessions.
    /// </summary>
    public int SessionCount => _sessions.Count;

    /// <summary>
    /// Gets the number of all targets (local + remote).
    /// </summary>
    public int TargetCount => _targets.Count;

    /// <summary>
    /// Gets a terminal target by ID (either local or remote).
    /// </summary>
    /// <param name="id">The target ID.</param>
    /// <returns>The target if found, null otherwise.</returns>
    public ITerminalTarget? GetTarget(string id)
    {
        _targets.TryGetValue(id, out var target);
        return target;
    }

    /// <summary>
    /// Lists all terminal targets (both local and remote).
    /// </summary>
    public IReadOnlyList<ITerminalTarget> ListTargets()
    {
        return [.. _targets.Values];
    }

    /// <summary>
    /// Connects to a remote Hex1b application by process ID.
    /// </summary>
    /// <param name="processId">The process ID of the Hex1b application.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connected remote target.</returns>
    public async Task<RemoteTerminalTarget> ConnectRemoteAsync(int processId, CancellationToken ct = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TerminalSessionManager));

        var target = await RemoteTerminalTarget.ConnectByPidAsync(processId, ct);
        
        if (!_targets.TryAdd(target.Id, target))
        {
            await target.DisposeAsync();
            throw new InvalidOperationException($"A target with ID '{target.Id}' already exists.");
        }

        return target;
    }

    /// <summary>
    /// Discovers and connects to all remote Hex1b applications with diagnostics enabled.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of newly connected remote targets.</returns>
    public async Task<IReadOnlyList<RemoteTerminalTarget>> DiscoverAndConnectRemotesAsync(CancellationToken ct = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TerminalSessionManager));

        var socketDir = GetSocketDirectory();
        var newTargets = new List<RemoteTerminalTarget>();

        if (!Directory.Exists(socketDir))
            return newTargets;

        var socketFiles = Directory.GetFiles(socketDir, "*.diagnostics.socket");

        foreach (var socketPath in socketFiles)
        {
            var fileName = Path.GetFileName(socketPath);
            var pidStr = fileName.Replace(".diagnostics.socket", "");
            
            if (!int.TryParse(pidStr, out var pid))
                continue;

            var targetId = $"remote-{pid}";
            
            // Skip if already connected
            if (_targets.ContainsKey(targetId))
                continue;

            // Check if process is still running
            if (!IsProcessRunning(pid))
            {
                // Clean up stale socket
                try { File.Delete(socketPath); }
                catch { /* ignore */ }
                continue;
            }

            try
            {
                var target = await RemoteTerminalTarget.ConnectAsync(socketPath, ct);
                if (_targets.TryAdd(target.Id, target))
                {
                    newTargets.Add(target);
                }
            }
            catch
            {
                // Failed to connect, skip
            }
        }

        return newTargets;
    }

    /// <summary>
    /// Disconnects a remote target.
    /// </summary>
    /// <param name="id">The target ID.</param>
    /// <returns>True if disconnected, false if not found.</returns>
    public async Task<bool> DisconnectRemoteAsync(string id)
    {
        if (!_targets.TryRemove(id, out var target))
            return false;

        await target.DisposeAsync();
        return true;
    }

    private static string GetSocketDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".hex1b", "sockets");
    }

    private static bool IsProcessRunning(int pid)
    {
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Starts a new terminal session with an auto-generated ID.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="arguments">Command arguments.</param>
    /// <param name="workingDirectory">Working directory for the process.</param>
    /// <param name="environment">Additional environment variables.</param>
    /// <param name="width">Terminal width in columns.</param>
    /// <param name="height">Terminal height in rows.</param>
    /// <param name="asciinemaFilePath">Optional path to save an asciinema recording.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created terminal session.</returns>
    public async Task<TerminalSession> StartSessionAsync(
        string command,
        string[] arguments,
        string? workingDirectory = null,
        Dictionary<string, string>? environment = null,
        int width = 80,
        int height = 24,
        string? asciinemaFilePath = null,
        CancellationToken ct = default)
    {
        var id = GenerateSessionId();
        return await StartSessionAsync(id, command, arguments, workingDirectory, environment, width, height, asciinemaFilePath, ct);
    }

    /// <summary>
    /// Starts a new terminal session with a specified ID.
    /// </summary>
    /// <param name="id">The session ID. Must be unique.</param>
    /// <param name="command">The command to execute.</param>
    /// <param name="arguments">Command arguments.</param>
    /// <param name="workingDirectory">Working directory for the process.</param>
    /// <param name="environment">Additional environment variables.</param>
    /// <param name="width">Terminal width in columns.</param>
    /// <param name="height">Terminal height in rows.</param>
    /// <param name="asciinemaFilePath">Optional path to save an asciinema recording.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created terminal session.</returns>
    /// <exception cref="InvalidOperationException">A session with the given ID already exists.</exception>
    public async Task<TerminalSession> StartSessionAsync(
        string id,
        string command,
        string[] arguments,
        string? workingDirectory = null,
        Dictionary<string, string>? environment = null,
        int width = 80,
        int height = 24,
        string? asciinemaFilePath = null,
        CancellationToken ct = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TerminalSessionManager));

        var session = await TerminalSession.StartAsync(
            id,
            command,
            arguments,
            workingDirectory,
            environment,
            width,
            height,
            asciinemaFilePath,
            ct);

        if (!_sessions.TryAdd(id, session))
        {
            await session.DisposeAsync();
            throw new InvalidOperationException($"A session with ID '{id}' already exists.");
        }

        // Also register as a target
        var target = new LocalTerminalTarget(session);
        _targets.TryAdd(id, target);

        return session;
    }

    /// <summary>
    /// Gets a session by ID.
    /// </summary>
    /// <param name="id">The session ID.</param>
    /// <returns>The session if found, null otherwise.</returns>
    public TerminalSession? GetSession(string id)
    {
        _sessions.TryGetValue(id, out var session);
        return session;
    }

    /// <summary>
    /// Gets all active sessions.
    /// </summary>
    /// <returns>A snapshot of all active sessions.</returns>
    public IReadOnlyList<TerminalSession> GetAllSessions()
    {
        return [.. _sessions.Values];
    }

    /// <summary>
    /// Lists all sessions with their basic information.
    /// </summary>
    /// <returns>Session information for all active sessions.</returns>
    public IReadOnlyList<SessionInfo> ListSessions()
    {
        return _sessions.Values.Select(s => new SessionInfo
        {
            Id = s.Id,
            Command = s.Command,
            Arguments = s.Arguments,
            WorkingDirectory = s.WorkingDirectory,
            Width = s.Width,
            Height = s.Height,
            StartedAt = s.StartedAt,
            HasExited = s.HasExited,
            ExitCode = s.HasExited ? s.ExitCode : null,
            ProcessId = s.ProcessId,
            AsciinemaFilePath = s.AsciinemaFilePath,
            IsRecording = s.IsRecording,
            ActiveRecordingPath = s.ActiveRecordingPath
        }).ToList();
    }

    /// <summary>
    /// Stops a session's process but keeps the session for inspection.
    /// </summary>
    /// <param name="id">The session ID.</param>
    /// <param name="signal">Signal to send when killing (Unix only). Default is SIGTERM (15).</param>
    /// <returns>True if the session was found, false if not found.</returns>
    public bool StopSession(string id, int signal = 15)
    {
        if (!_sessions.TryGetValue(id, out var session))
            return false;

        if (!session.HasExited)
        {
            session.Kill(signal);
        }
        return true;
    }

    /// <summary>
    /// Removes a session completely, disposing all resources.
    /// </summary>
    /// <param name="id">The session ID.</param>
    /// <returns>True if the session was found and removed, false if not found.</returns>
    public async Task<bool> RemoveSessionAsync(string id)
    {
        // Also remove from targets
        _targets.TryRemove(id, out _);

        if (!_sessions.TryRemove(id, out var session))
            return false;

        await session.DisposeAsync();
        return true;
    }

    /// <summary>
    /// Stops and removes a session.
    /// </summary>
    /// <param name="id">The session ID.</param>
    /// <param name="signal">Signal to send when killing (Unix only). Default is SIGTERM (15).</param>
    /// <returns>True if the session was found and stopped, false if not found.</returns>
    public async Task<bool> StopAndRemoveSessionAsync(string id, int signal = 15)
    {
        // Also remove from targets
        _targets.TryRemove(id, out _);

        if (!_sessions.TryRemove(id, out var session))
            return false;

        session.Kill(signal);
        await session.DisposeAsync();
        return true;
    }

    /// <summary>
    /// Stops all sessions.
    /// </summary>
    public async Task StopAllSessionsAsync()
    {
        var sessions = _sessions.Values.ToList();
        _sessions.Clear();
        _targets.Clear();

        foreach (var session in sessions)
        {
            session.Kill();
            await session.DisposeAsync();
        }
    }

    /// <summary>
    /// Removes all sessions where the process has exited.
    /// </summary>
    /// <returns>The IDs of sessions that were cleaned up.</returns>
    public async Task<IReadOnlyList<string>> CleanupExitedSessionsAsync()
    {
        var exitedSessions = _sessions
            .Where(kvp => kvp.Value.HasExited)
            .Select(kvp => kvp.Key)
            .ToList();

        var cleanedUp = new List<string>();
        foreach (var id in exitedSessions)
        {
            if (_sessions.TryRemove(id, out var session))
            {
                await session.DisposeAsync();
                cleanedUp.Add(id);
            }
        }

        return cleanedUp;
    }

    private static string GenerateSessionId()
    {
        // Generate a short, human-readable ID
        return $"term-{Guid.NewGuid():N}"[..12];
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await StopAllSessionsAsync();
    }
}

/// <summary>
/// Information about a terminal session.
/// </summary>
public class SessionInfo
{
    /// <summary>
    /// The unique session ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The command being executed.
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// The command arguments.
    /// </summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>
    /// The working directory.
    /// </summary>
    public required string? WorkingDirectory { get; init; }

    /// <summary>
    /// Terminal width in columns.
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// Terminal height in rows.
    /// </summary>
    public required int Height { get; init; }

    /// <summary>
    /// When the session was started.
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Whether the process has exited.
    /// </summary>
    public required bool HasExited { get; init; }

    /// <summary>
    /// The exit code if the process has exited.
    /// </summary>
    public required int? ExitCode { get; init; }

    /// <summary>
    /// The process ID of the child process.
    /// </summary>
    public required int ProcessId { get; init; }

    /// <summary>
    /// The path to the asciinema recording file specified at session start, if any.
    /// </summary>
    public string? AsciinemaFilePath { get; init; }

    /// <summary>
    /// Whether the session is currently recording to an asciinema file.
    /// </summary>
    public required bool IsRecording { get; init; }

    /// <summary>
    /// The path to the currently active asciinema recording, or null if not recording.
    /// </summary>
    public string? ActiveRecordingPath { get; init; }
}
