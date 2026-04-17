using System.Diagnostics;
using System.Net.Sockets;
using Hex1b;
using Hex1b.Input;
using Hex1b.Muxer;
using Hex1b.Widgets;

// Well-known session directory
var sessionDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".hex1bsamples", "muxerdemo");
Directory.CreateDirectory(sessionDir);

if (args is ["--server", var socketPath])
{
    // Headless server mode: run a PTY process and serve it over UDS
    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithPtyProcess(options =>
        {
            options.FileName = GetShell();
            if (OperatingSystem.IsWindows())
                options.WindowsPtyMode = WindowsPtyMode.RequireProxy;
        })
        .WithMuxerServer(server => server.ListenUnixSocket(socketPath))
        .Build();

    await terminal.RunAsync();
    return;
}

// === Interactive session manager ===

Hex1bApp? app = null;
var view = "sessions"; // "sessions" or "terminal"
MuxerWorkloadAdapter? muxerAdapter = null;
Hex1bTerminal? embeddedTerminal = null;
TerminalWidgetHandle? terminalHandle = null;
CancellationTokenSource? embeddedCts = null;
string? connectedSessionName = null;
string? connectedSocketPath = null;
string? statusMessage = null;

await using var displayTerminal = Hex1bTerminal.CreateBuilder()
    .WithMouse()
    .WithHex1bApp((a, _) =>
    {
        app = a;
        return ctx =>
        {
            Hex1bWidget content;
            if (view == "terminal" && terminalHandle is not null)
                content = BuildTerminalView(ctx, terminalHandle);
            else
                content = BuildSessionListView(ctx);

            // Register chords on the root widget. Input bubbles up from focused
            // children, so these work like global shortcuts without triggering
            // the global binding conflict detector.
            return content.WithInputBindings(bindings =>
            {
                bindings.Ctrl().Key(Hex1bKey.B).Then().Key(Hex1bKey.D)
                    .OverridesCapture()
                    .Action(_ => app?.RequestStop(), "Detach");

                bindings.Ctrl().Key(Hex1bKey.B).Then().Key(Hex1bKey.S)
                    .OverridesCapture()
                    .Action(_ =>
                    {
                        view = "sessions";
                        statusMessage = null;
                        app?.Invalidate();
                    }, "Sessions");

                bindings.Ctrl().Key(Hex1bKey.B).Then().Key(Hex1bKey.X)
                    .OverridesCapture()
                    .Action(async _ => await KillCurrentSessionAsync(), "Kill session");
            });
        };
    })
    .Build();

await displayTerminal.RunAsync();

// Clean up embedded terminal on exit
await CleanupEmbeddedTerminalAsync();
return;

// === View builders ===

Hex1bWidget BuildSessionListView<TParent>(WidgetContext<TParent> ctx) where TParent : Hex1bWidget
{
    var sessions = DiscoverSessions();
    var items = sessions.Select(s => s.Name).Append("+ New Session").ToList();

    return ctx.VStack(v =>
    [
        v.Text(" Hex1b Muxer Demo "),
        v.Text(""),
        v.Text(statusMessage ?? "Select a session or create a new one:"),
        v.Text(""),
        v.List(items)
            .OnItemActivated(e =>
            {
                if (e.ActivatedIndex == sessions.Count)
                {
                    // "New Session" selected
                    _ = CreateAndConnectSessionAsync();
                }
                else
                {
                    _ = ConnectToSessionAsync(sessions[e.ActivatedIndex]);
                }
            })
            .FillHeight(),
        v.InfoBar(s =>
        [
            s.Section("Enter"),
            s.Section("Select"),
            s.Spacer(),
            s.Section("Ctrl+B D"),
            s.Section("Quit")
        ]).WithDefaultSeparator(" ")
    ]);
}

Hex1bWidget BuildTerminalView<TParent>(WidgetContext<TParent> ctx, TerminalWidgetHandle handle) where TParent : Hex1bWidget
{
    var dims = muxerAdapter is not null
        ? $"{muxerAdapter.RemoteWidth}\u00d7{muxerAdapter.RemoteHeight}"
        : "";

    return ctx.VStack(v =>
    [
        v.Terminal(handle).Fill(),
        v.InfoBar(s =>
        [
            s.Section("Ctrl+B S"),
            s.Section("Sessions"),
            s.Spacer(),
            s.Section("Ctrl+B X"),
            s.Section("Kill"),
            s.Spacer(),
            s.Section("Ctrl+B D"),
            s.Section("Detach"),
            s.Spacer(),
            s.Section(connectedSessionName ?? ""),
            s.Section(dims)
        ]).WithDefaultSeparator(" ")
    ]);
}

// === Session management ===

List<SessionInfo> DiscoverSessions()
{
    var sessions = new List<SessionInfo>();

    if (!Directory.Exists(sessionDir))
        return sessions;

    foreach (var file in Directory.GetFiles(sessionDir, "*.sock"))
    {
        var name = Path.GetFileNameWithoutExtension(file);
        // Parse timestamp from filename: session_yyyyMMdd_HHmmss
        var displayName = name.StartsWith("session_")
            ? FormatSessionName(name["session_".Length..])
            : name;
        sessions.Add(new SessionInfo(displayName, file));
    }

    sessions.Sort((a, b) => string.Compare(b.SocketPath, a.SocketPath, StringComparison.Ordinal));
    return sessions;
}

static string FormatSessionName(string timestamp)
{
    // Try to parse yyyyMMdd_HHmmss into a readable format
    if (DateTime.TryParseExact(timestamp, "yyyyMMdd_HHmmss",
        System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.None, out var dt))
    {
        return dt.ToString("yyyy-MM-dd HH:mm:ss");
    }
    return timestamp;
}

async Task CreateAndConnectSessionAsync()
{
    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    var sockPath = Path.Combine(sessionDir, $"session_{timestamp}.sock");

    statusMessage = "Starting new session...";
    app?.Invalidate();

    // Launch server process in background
    var exe = Environment.ProcessPath ?? "dotnet";
    var psi = new ProcessStartInfo
    {
        FileName = exe,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };

    // If running via `dotnet run`, we need to pass the DLL
    if (!exe.EndsWith("MuxerDemo", StringComparison.OrdinalIgnoreCase) &&
        !exe.EndsWith("MuxerDemo.exe", StringComparison.OrdinalIgnoreCase))
    {
        psi.FileName = "dotnet";
        var assemblyPath = typeof(Hex1bTerminal).Assembly.Location;
        // Find MuxerDemo.dll relative to current assembly
        var demoAssembly = Path.Combine(AppContext.BaseDirectory, "MuxerDemo.dll");
        psi.ArgumentList.Add(demoAssembly);
    }

    psi.ArgumentList.Add("--server");
    psi.ArgumentList.Add(sockPath);

    var process = Process.Start(psi);
    if (process is null)
    {
        statusMessage = "Failed to start server process.";
        app?.Invalidate();
        return;
    }

    // Wait for the socket file to appear
    for (var i = 0; i < 50; i++)
    {
        await Task.Delay(100);
        if (File.Exists(sockPath))
            break;
    }

    if (!File.Exists(sockPath))
    {
        statusMessage = "Server didn't start in time.";
        app?.Invalidate();
        return;
    }

    await ConnectToSessionAsync(new SessionInfo($"session_{timestamp}", sockPath));
}

async Task ConnectToSessionAsync(SessionInfo session)
{
    statusMessage = $"Connecting to {session.Name}...";
    app?.Invalidate();

    try
    {
        // Clean up any existing embedded terminal
        await CleanupEmbeddedTerminalAsync();

        // Connect to the muxer server
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(session.SocketPath));
        var stream = new NetworkStream(socket, ownsSocket: true);

        muxerAdapter = new MuxerWorkloadAdapter(stream);
        await muxerAdapter.ConnectAsync(CancellationToken.None);

        // Create embedded terminal
        embeddedCts = new CancellationTokenSource();

        embeddedTerminal = Hex1bTerminal.CreateBuilder()
            .WithDimensions(muxerAdapter.RemoteWidth, muxerAdapter.RemoteHeight)
            .WithWorkload(muxerAdapter)
            .WithScrollback()
            .WithTerminalWidget(out var handle)
            .Build();

        terminalHandle = handle;
        connectedSessionName = session.Name;
        connectedSocketPath = session.SocketPath;

        _ = embeddedTerminal.RunAsync(embeddedCts.Token);

        // Switch to terminal view
        view = "terminal";
        statusMessage = null;
        app?.Invalidate();
    }
    catch (SocketException)
    {
        // Stale socket — clean it up
        try { File.Delete(session.SocketPath); } catch { }
        statusMessage = $"Session '{session.Name}' is no longer available (cleaned up).";
        muxerAdapter = null;
        app?.Invalidate();
    }
    catch (Exception ex)
    {
        statusMessage = $"Failed to connect: {ex.Message}";
        muxerAdapter = null;
        app?.Invalidate();
    }
}

async Task CleanupEmbeddedTerminalAsync()
{
    if (embeddedCts is not null)
    {
        await embeddedCts.CancelAsync();
        embeddedCts.Dispose();
        embeddedCts = null;
    }

    if (embeddedTerminal is not null)
    {
        await embeddedTerminal.DisposeAsync();
        embeddedTerminal = null;
    }

    if (muxerAdapter is not null)
    {
        await muxerAdapter.DisposeAsync();
        muxerAdapter = null;
    }

    terminalHandle = null;
    connectedSessionName = null;
    connectedSocketPath = null;
}

async Task KillCurrentSessionAsync()
{
    if (connectedSocketPath is null)
        return;

    var socketToDelete = connectedSocketPath;
    var sessionName = connectedSessionName;

    await CleanupEmbeddedTerminalAsync();

    // Delete the socket file to kill the server
    try { File.Delete(socketToDelete); } catch { }

    view = "sessions";
    statusMessage = $"Killed session '{sessionName}'.";
    app?.Invalidate();
}

static string GetShell()
{
    if (OperatingSystem.IsWindows())
        return "pwsh.exe";
    
    var shell = Environment.GetEnvironmentVariable("SHELL");
    return !string.IsNullOrEmpty(shell) ? shell : "bash";
}

record SessionInfo(string Name, string SocketPath);
