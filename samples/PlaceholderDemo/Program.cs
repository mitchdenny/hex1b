using System.Diagnostics;
using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

// PlaceholderDemo
//
// Self-contained demo of WithPlaceholderHex1bApp embedded inside a TUI.
//
// Architecture:
//   * Outer Hex1bTerminal = a TUI (Hex1bApp) that frames an embedded
//     TerminalWidget.
//   * Inner Hex1bTerminal = an HMP1 client (the "primary" workload),
//     decorated with WithPlaceholderHex1bApp(...) so that while the
//     producer's UDS server isn't reachable, the embedded widget
//     renders the placeholder UI ("Press R to launch shell"). When the
//     producer becomes reachable, the placeholder swaps out for the live
//     shell stream. When the shell exits, the OnDisconnect resume policy
//     puts the placeholder back so the user can relaunch.
//
// Modes (selected via argv):
//   --server <sockpath>   Run a PTY shell (bash/pwsh) and expose it over
//                         HMP1 on the given UDS path. Exits when shell exits.
//   (default)             Launch the TUI. Auto-discovers a temp socket path
//                         and spawns itself as the server when the user
//                         presses R in the placeholder.

if (args.Length >= 2 && args[0] == "--server")
{
    return await RunServerAsync(args[1]);
}

var sockPath = args.Length >= 1
    ? args[0]
    : Path.Combine(Path.GetTempPath(), $"hex1b-placeholder-{Environment.ProcessId}.sock");

return await RunClientAsync(sockPath);

static async Task<int> RunServerAsync(string sockPath)
{
    try { File.Delete(sockPath); } catch { }

    await using var terminal = Hex1bTerminal.CreateBuilder()
        .WithPtyProcess(options =>
        {
            options.FileName = GetShell();
            if (OperatingSystem.IsWindows())
                options.WindowsPtyMode = WindowsPtyMode.RequireProxy;
        })
        .WithHmp1UdsServer(sockPath)
        .Build();

    try
    {
        await terminal.RunAsync();
    }
    finally
    {
        try { File.Delete(sockPath); } catch { }
    }

    return 0;
}

static async Task<int> RunClientAsync(string sockPath)
{
    using var lifetime = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        lifetime.Cancel();
    };

    Process? shellProc = null;
    Hex1bApp? placeholderApp = null;
    Hex1bApp? outerApp = null;
    var status = "Press R to launch the shell.";

    void UpdateStatusFromShellState()
    {
        if (shellProc is null)
        {
            status = "Press R to launch the shell.";
        }
        else if (shellProc.HasExited)
        {
            status = $"Shell exited (code {shellProc.ExitCode}). Press R to relaunch.";
        }
        else
        {
            status = "Connecting to shell…";
        }

        placeholderApp?.Invalidate();
    }

    void LaunchShellServer()
    {
        if (shellProc is { HasExited: false }) return;

        var self = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot resolve current process path to relaunch as server.");

        var psi = new ProcessStartInfo(self)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("--server");
        psi.ArgumentList.Add(sockPath);

        var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Process.Start returned null.");
        proc.EnableRaisingEvents = true;
        proc.Exited += (_, _) => UpdateStatusFromShellState();

        shellProc = proc;
        UpdateStatusFromShellState();
    }

    // Inner terminal: HMP1 client (primary) decorated with a placeholder
    // Hex1bApp. The output is surfaced to the outer TUI via WithTerminalWidget,
    // so what the outer sees is whichever side (placeholder or shell) is
    // currently active.
    var innerTerminal = Hex1bTerminal.CreateBuilder()
        .WithDimensions(80, 24)
        .WithHmp1Client(
            Hmp1Transports.RetryingUnixSocket(sockPath, new RetryPolicy
            {
                InitialDelay = TimeSpan.FromMilliseconds(150),
                MaxDelay = TimeSpan.FromSeconds(1),
                Multiplier = 1.5,
                OnAttemptFailed = _ => UpdateStatusFromShellState(),
            }),
            opts =>
            {
                opts.DisplayName = "placeholder-demo";
                opts.DefaultRole = Hmp1Role.Secondary;
            })
        .WithPlaceholderHex1bApp(app =>
        {
            placeholderApp = app;
            return ctx =>
                ctx.Center(
                    ctx.VStack(v =>
                    [
                        v.Text("Shell not connected"),
                        v.Text(""),
                        v.Text(status),
                        v.Text(""),
                        v.Text("R = launch shell · Q = quit"),
                        v.Text("(While shell is live, type `exit` to return here.)"),
                    ]))
                .Fill()
                .InputBindings(b =>
                {
                    b.Key(Hex1bKey.R).Action(_ => LaunchShellServer(), "Launch shell");
                    b.Key(Hex1bKey.Q).Action(_ =>
                    {
                        try { lifetime.Cancel(); } catch { }
                    }, "Quit");
                });
        })
        .WithTerminalWidget(out var termHandle)
        .Build();

    var innerTask = innerTerminal.RunAsync(lifetime.Token);

    // Outer TUI: a thin frame around the embedded terminal.
    await using var outer = Hex1bTerminal.CreateBuilder()
        .WithMouse()
        .WithHex1bApp(_ => { }, app =>
        {
            outerApp = app;
            return ctx =>
                ctx.Border(
                    ctx.Terminal(termHandle).Fill())
                .Title($"PlaceholderDemo · {sockPath}")
                .Fill();
        })
        .Build();

    try
    {
        await outer.RunAsync(lifetime.Token);
    }
    catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
    {
        // Normal shutdown via Ctrl-C.
    }
    finally
    {
        try { lifetime.Cancel(); } catch { }

        try { await innerTask.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch { /* swallow on shutdown */ }

        await innerTerminal.DisposeAsync();

        if (shellProc is { HasExited: false } running)
        {
            try { running.Kill(entireProcessTree: true); } catch { }
            try { await running.WaitForExitAsync(); } catch { }
        }
    }

    return 0;
}

static string GetShell()
{
    if (OperatingSystem.IsWindows())
        return "pwsh.exe";

    var shell = Environment.GetEnvironmentVariable("SHELL");
    return !string.IsNullOrEmpty(shell) ? shell : "bash";
}
