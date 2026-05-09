using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1bStory.Slides;

/// <summary>
/// "The samples graveyard" — the centerpiece of the deck. Lists every folder
/// under <c>samples/</c> in a focusable list on the left, and, on Enter,
/// spawns <c>dotnet run --project samples/&lt;name&gt;</c> inside an
/// embedded <see cref="TerminalWidget"/> on the right. The slide thus also
/// doubles as a live demo of Hex1b's terminal emulator.
/// </summary>
/// <remarks>
/// Owns the embedded <see cref="Hex1bTerminal"/>. Implements
/// <see cref="IAsyncDisposable"/> so the shell can dispose it on exit, and
/// overrides <see cref="ISlide.OnExit"/> so the embedded process is killed
/// the moment the presenter navigates away. Activation is Enter-only —
/// arrowing the list must NOT spawn dotnet, or we'd leak a process per
/// keypress.
/// </remarks>
internal sealed class SamplesGraveyardSlide : ISlide, IAsyncDisposable
{
    public string Title => "The samples graveyard";

    private readonly string? _repoRoot;
    private readonly IReadOnlyList<string> _samples;

    private CancellationTokenSource? _runCts;
    private Hex1bTerminal? _running;
    private TerminalWidgetHandle? _handle;
    private string? _runningName;
    private string? _statusMessage;

    // Cached so the OnItemActivated / R-key handlers can size a fresh
    // embedded terminal even though they don't have direct access to
    // the SlideContext.
    private int _lastTermWidth = 80;
    private int _lastTermHeight = 20;

    public SamplesGraveyardSlide()
    {
        _repoRoot = RepoRoot.Locate();
        _samples = LoadSampleNames(_repoRoot);
    }

    public Hex1bWidget Build(SlideContext context)
    {
        var ctx = context.Root;

        if (_repoRoot is null || _samples.Count == 0)
        {
            return ctx.VStack(v =>
            [
                v.Text("The samples graveyard"),
                v.Text("═════════════════════"),
                v.Text(""),
                v.Text("(Could not locate the samples/ directory at runtime.)"),
                v.Text(""),
                v.Text("Run this deck from inside a checkout of mitchdenny/hex1b"),
                v.Text("to see this slide light up — every sample listed there is"),
                v.Text("something we tried with the agent."),
            ]);
        }

        const int leftPaneWidth = 32;
        // Embedded terminal inner buffer size: leave room for left pane,
        // gap, two borders, slide padding, header and footer rows.
        _lastTermWidth = Math.Max(40, context.Width - leftPaneWidth - 12);
        _lastTermHeight = Math.Max(10, context.Height - 12);

        var status = _statusMessage ?? "↑↓ pick  ·  Enter run  ·  R restart  ·  K kill";
        var samplesSnapshot = _samples;

        return ctx.VStack(v =>
        [
            v.Text("The samples graveyard"),
            v.Text("═════════════════════"),
            v.Text(""),
            v.Text("Every one of these is something we tried with the agent."),
            v.Text(""),
            v.HStack(row =>
            [
                row.Border(row.List(samplesSnapshot)
                    .OnItemActivated(e => RunSample(samplesSnapshot[e.ActivatedIndex])))
                    .Title($" samples/ ({samplesSnapshot.Count}) ")
                    .FixedWidth(leftPaneWidth),
                row.Text(" "),
                row.Border(BuildRightPane(row))
                    .Title(_runningName is null ? " preview " : $" samples/{_runningName} ")
                    .Fill(),
            ]).Fill(),
            v.Text(""),
            v.Text(status),
        ]).InputBindings(b =>
        {
            b.Key(Hex1bKey.R).Global().Action(_ =>
            {
                if (_runningName is { } current) RunSample(current);
            }, "Restart sample");
            b.Key(Hex1bKey.K).Global().Action(_ => KillRunning(), "Kill sample");
        });
    }

    private Hex1bWidget BuildRightPane<TParent>(WidgetContext<TParent> ctx)
        where TParent : Hex1bWidget
    {
        if (_handle is { } handle)
        {
            return ctx.Terminal(handle)
                .WhenNotRunning(args => ctx.VStack(v =>
                [
                    v.Text(""),
                    v.Text($"  Exited with code {args.ExitCode ?? 0}."),
                    v.Text(""),
                    v.Text("  Press R to restart, K to clear, or Enter another"),
                    v.Text("  sample on the left to switch."),
                ]));
        }

        return ctx.VStack(v =>
        [
            v.Text(""),
            v.Text("  Pick a sample on the left and press Enter to run it."),
            v.Text(""),
            v.Text("  That's literally `dotnet run --project samples/<name>`,"),
            v.Text("  inside an embedded TerminalWidget — which is the same"),
            v.Text("  TerminalWidget Aspire is about to start using to give"),
            v.Text("  the CLI multi-headed PTY connections."),
            v.Text(""),
            v.Text("  The samples folder is a graveyard of experiments."),
            v.Text("  And that's the point."),
        ]);
    }

    private void RunSample(string name)
    {
        if (_repoRoot is null) return;

        // Tear down the previous run BEFORE spawning a new one so we
        // don't accumulate dotnet processes if the user mashes Enter.
        TearDown();

        try
        {
            var sampleProj = Path.Combine(_repoRoot, "samples", name);
            var cts = new CancellationTokenSource();
            var nested = Hex1bTerminal.CreateBuilder()
                .WithDimensions(_lastTermWidth, _lastTermHeight)
                .WithPtyProcess(opts =>
                {
                    opts.FileName = "dotnet";
                    opts.Arguments = ["run", "--project", sampleProj, "--no-launch-profile"];
                    opts.WorkingDirectory = _repoRoot;
                })
                .WithTerminalWidget(out var handle)
                .Build();

            _runCts = cts;
            _running = nested;
            _handle = handle;
            _runningName = name;
            _statusMessage = $"running samples/{name}  ·  R restart  ·  K kill";

            _ = Task.Run(async () =>
            {
                try { await nested.RunAsync(cts.Token); }
                catch (OperationCanceledException) { /* normal shutdown */ }
                catch { /* don't crash the deck */ }
            });
        }
        catch (Exception ex)
        {
            _statusMessage = $"failed to launch: {ex.Message}";
        }
    }

    private void KillRunning()
    {
        var name = _runningName;
        TearDown();
        _statusMessage = name is null ? "nothing to kill" : $"killed samples/{name}";
    }

    private void TearDown()
    {
        var cts = _runCts;
        var terminal = _running;
        _runCts = null;
        _running = null;
        _handle = null;
        _runningName = null;

        if (cts is not null)
        {
            try { cts.Cancel(); } catch { /* ignore */ }
        }

        if (terminal is not null)
        {
            // Dispose off-thread so we don't block the render path. The
            // presenter has already moved on visually.
            _ = Task.Run(async () =>
            {
                try { await terminal.DisposeAsync(); }
                catch { /* ignore */ }
                try { cts?.Dispose(); }
                catch { /* ignore */ }
            });
        }
        else
        {
            try { cts?.Dispose(); } catch { /* ignore */ }
        }
    }

    public Task OnExit()
    {
        // Presenter navigated away — kill the embedded process so we
        // don't leave a dotnet child running invisibly behind the deck.
        TearDown();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        var cts = _runCts;
        var terminal = _running;
        _runCts = null;
        _running = null;
        _handle = null;
        _runningName = null;

        if (cts is not null)
        {
            try { cts.Cancel(); } catch { /* ignore */ }
        }

        if (terminal is not null)
        {
            try { await terminal.DisposeAsync(); } catch { /* ignore */ }
        }

        try { cts?.Dispose(); } catch { /* ignore */ }
    }

    private static IReadOnlyList<string> LoadSampleNames(string? repoRoot)
    {
        if (repoRoot is null) return [];

        try
        {
            var samplesDir = Path.Combine(repoRoot, "samples");
            if (!Directory.Exists(samplesDir)) return [];

            return Directory.GetDirectories(samplesDir)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToArray();
        }
        catch
        {
            return [];
        }
    }
}
