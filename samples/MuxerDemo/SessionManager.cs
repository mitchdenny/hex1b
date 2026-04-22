using System.Diagnostics;
using System.Net.Sockets;
using Hex1b;

namespace MuxerDemo;

/// <summary>
/// Discovers, creates, connects to, and kills terminal sessions.
/// Sessions are stored as UDS socket files in a well-known directory.
/// </summary>
internal sealed class SessionManager
{
    private readonly string _sessionDir;
    private readonly Dictionary<string, Process> _serverProcesses = new();

    public SessionManager(string sessionDir)
    {
        _sessionDir = sessionDir;
        Directory.CreateDirectory(sessionDir);
    }

    // Current connection state
    public Hmp1WorkloadAdapter? Adapter { get; private set; }
    public Hex1bTerminal? EmbeddedTerminal { get; private set; }
    public TerminalWidgetHandle? Handle { get; private set; }
    public string? ConnectedSessionName { get; private set; }
    public string? ConnectedSocketPath { get; private set; }
    public string? StatusMessage { get; set; }
    public bool IsConnected => Handle is not null;

    private CancellationTokenSource? _embeddedCts;

    public List<SessionInfo> DiscoverSessions()
    {
        var sessions = new List<SessionInfo>();

        if (!Directory.Exists(_sessionDir))
            return sessions;

        foreach (var file in Directory.GetFiles(_sessionDir, "*.sock"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var displayName = name.StartsWith("session_")
                ? FormatSessionName(name["session_".Length..])
                : name;
            sessions.Add(new SessionInfo(displayName, file));
        }

        sessions.Sort((a, b) => string.Compare(b.SocketPath, a.SocketPath, StringComparison.Ordinal));
        return sessions;
    }

    public async Task CreateAndConnectSessionAsync(Action? onStateChanged = null)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var sockPath = Path.Combine(_sessionDir, $"session_{timestamp}.sock");

        StatusMessage = "Starting new session...";
        onStateChanged?.Invoke();

        var exe = Environment.ProcessPath ?? "dotnet";
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (!exe.EndsWith("MuxerDemo", StringComparison.OrdinalIgnoreCase) &&
            !exe.EndsWith("MuxerDemo.exe", StringComparison.OrdinalIgnoreCase))
        {
            psi.FileName = "dotnet";
            var demoAssembly = Path.Combine(AppContext.BaseDirectory, "MuxerDemo.dll");
            psi.ArgumentList.Add(demoAssembly);
        }

        psi.ArgumentList.Add("--server");
        psi.ArgumentList.Add(sockPath);

        var process = Process.Start(psi);
        if (process is null)
        {
            StatusMessage = "Failed to start server process.";
            onStateChanged?.Invoke();
            return;
        }

        _serverProcesses[sockPath] = process;

        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            if (File.Exists(sockPath))
                break;
        }

        if (!File.Exists(sockPath))
        {
            StatusMessage = "Server didn't start in time.";
            onStateChanged?.Invoke();
            return;
        }

        await ConnectToSessionAsync(new SessionInfo($"session_{timestamp}", sockPath), onStateChanged);
    }

    public async Task ConnectToSessionAsync(SessionInfo session, Action? onStateChanged = null)
    {
        StatusMessage = $"Connecting to {session.Name}...";
        onStateChanged?.Invoke();

        try
        {
            await DisconnectAsync();

            var stream = await Hmp1Transports.ConnectUnixSocket(session.SocketPath, CancellationToken.None);

            Adapter = new Hmp1WorkloadAdapter(stream);
            await Adapter.ConnectAsync(CancellationToken.None);

            _embeddedCts = new CancellationTokenSource();

            EmbeddedTerminal = Hex1bTerminal.CreateBuilder()
                .WithDimensions(Adapter.RemoteWidth, Adapter.RemoteHeight)
                .WithWorkload(Adapter)
                .WithScrollback()
                .WithTerminalWidget(out var handle)
                .Build();

            Handle = handle;
            ConnectedSessionName = session.Name;
            ConnectedSocketPath = session.SocketPath;

            handle.StateChanged += state =>
            {
                if (state == TerminalState.Completed)
                {
                    StatusMessage = $"Session '{session.Name}' exited.";
                    Handle = null;
                    ConnectedSessionName = null;
                    ConnectedSocketPath = null;
                    onStateChanged?.Invoke();
                }
            };

            _ = EmbeddedTerminal.RunAsync(_embeddedCts.Token);

            StatusMessage = null;
            onStateChanged?.Invoke();
        }
        catch (SocketException)
        {
            try { File.Delete(session.SocketPath); } catch { }
            StatusMessage = $"Session '{session.Name}' is no longer available (cleaned up).";
            Adapter = null;
            onStateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to connect: {ex.Message}";
            Adapter = null;
            onStateChanged?.Invoke();
        }
    }

    public async Task KillCurrentSessionAsync(Action? onStateChanged = null)
    {
        if (ConnectedSocketPath is null)
            return;

        var socketToDelete = ConnectedSocketPath;
        var sessionName = ConnectedSessionName;

        await DisconnectAsync();

        KillServerProcess(socketToDelete);
        try { File.Delete(socketToDelete); } catch { }

        StatusMessage = $"Killed session '{sessionName}'.";
        onStateChanged?.Invoke();
    }

    public async Task DisconnectAsync()
    {
        if (_embeddedCts is not null)
        {
            await _embeddedCts.CancelAsync();
            _embeddedCts.Dispose();
            _embeddedCts = null;
        }

        if (EmbeddedTerminal is not null)
        {
            await EmbeddedTerminal.DisposeAsync();
            EmbeddedTerminal = null;
        }

        if (Adapter is not null)
        {
            await Adapter.DisposeAsync();
            Adapter = null;
        }

        Handle = null;
        ConnectedSessionName = null;
        ConnectedSocketPath = null;
    }

    public void KillAllServerProcesses()
    {
        foreach (var (path, proc) in _serverProcesses)
        {
            try
            {
                if (!proc.HasExited)
                    proc.Kill(entireProcessTree: true);
            }
            catch { }
            proc.Dispose();
            try { File.Delete(path); } catch { }
        }
        _serverProcesses.Clear();
    }

    private void KillServerProcess(string socketPath)
    {
        if (_serverProcesses.TryGetValue(socketPath, out var proc))
        {
            _serverProcesses.Remove(socketPath);
            try
            {
                if (!proc.HasExited)
                    proc.Kill(entireProcessTree: true);
            }
            catch { }
            proc.Dispose();
        }
    }

    private static string FormatSessionName(string timestamp)
    {
        if (DateTime.TryParseExact(timestamp, "yyyyMMdd_HHmmss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt))
        {
            return dt.ToString("yyyy-MM-dd HH:mm:ss");
        }
        return timestamp;
    }
}

internal sealed record SessionInfo(string Name, string SocketPath);
