using Hex1b;
using Microsoft.Extensions.Logging;

namespace WebTerminalDemo;

internal sealed class TerminalInstance
{
    private readonly object _gate = new();
    private readonly Dictionary<string, TerminalView> _views = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _stop;
    private readonly CancellationTokenRegistration _applicationStopping;
    private readonly Hex1bTerminal _terminal;
    private readonly DemoWorkload? _demo;
    private readonly DemoTape[] _tapes;
    private readonly TerminalTapePlayback _tapePlayback;
    private readonly ILogger _logger;
    private readonly Action<TerminalInstance> _onCompleted;
    private readonly string _name;
    private readonly string _scene;
    private readonly DateTimeOffset _createdAt = DateTimeOffset.UtcNow;
    private Task _completion = Task.CompletedTask;
    private bool _stopping;
    private BrowserCloseRequest? _closeReason;

    public TerminalInstance(CreateTerminalRequest request, DemoTapeCatalog tapeCatalog, ILogger logger,
        CancellationToken applicationStopping, Action<TerminalInstance> onCompleted)
    {
        Id = Guid.NewGuid().ToString("N");
        _scene = request.Scene;
        _tapes = tapeCatalog.ForScene(_scene);
        _name = request.Name?.Trim() ?? $"{char.ToUpperInvariant(_scene[0])}{_scene[1..]} {Id[..6]}";
        _logger = logger;
        _onCompleted = onCompleted;
        _stop = new CancellationTokenSource();
        Stopping = _stop.Token;
        Presentation = new Hmp1PresentationAdapter(request.Columns, request.Rows);
        _demo = _scene == "shell" ? null : new DemoWorkload(_scene, request.Columns, request.Rows);
        var child = _demo is null ? new Hex1bTerminalChildProcess(
            OperatingSystem.IsWindows() ? "cmd.exe" : Environment.GetEnvironmentVariable("SHELL") ?? "/bin/sh",
            [], environment: OperatingSystem.IsWindows() ? null : new()
            {
                ["TERM"] = "xterm-256color",
                ["COLORTERM"] = "truecolor"
            }, initialWidth: request.Columns, initialHeight: request.Rows) : null;
        _terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            Width = request.Columns,
            Height = request.Rows,
            WorkloadAdapter = (IHex1bTerminalWorkloadAdapter?)_demo ?? child!,
            PresentationAdapter = Presentation,
            ScrollbackCapacity = 1000,
            RunCallback = async ct =>
            {
                if (_demo is not null)
                {
                    await _demo.RunAsync(ct);
                    return 0;
                }
                await child!.StartAsync(ct);
                return await child.WaitForExitAsync(ct);
            }
        });
        _tapePlayback = new TerminalTapePlayback(_terminal, Id, logger, Stopping);
        _applicationStopping = applicationStopping.Register(() => RequestStop(BrowserCloseRequest.ApplicationStopping));
    }

    public string Id { get; }
    public Hmp1PresentationAdapter Presentation { get; }
    public CancellationToken Stopping { get; }
    public bool IsStopping { get { lock (_gate) return _stopping || Stopping.IsCancellationRequested; } }
    public BrowserCloseRequest? CloseReason { get { lock (_gate) return _closeReason; } }

    public TerminalInstanceInfo GetInfo()
    {
        lock (_gate)
            return new(Id, _name, _scene, Presentation.Width, Presentation.Height,
                Presentation.ClientCount, Presentation.PrimaryPeerId, _createdAt,
                _demo?.Paused, _demo?.Rate, _demo?.Batch,
                _tapes.Select(tape => tape.Info).ToArray(), _tapePlayback.Status);
    }

    public void Start() => _completion = Task.Run(RunLifetimeAsync);

    public (TerminalView? View, int Status) TryOpenView(string? id, Action onClosed)
    {
        lock (_gate)
        {
            if (_stopping || Stopping.IsCancellationRequested)
                return (null, 404);
            id ??= Guid.NewGuid().ToString("D");
            if (_views.ContainsKey(id))
                return (null, 409);
            var view = new TerminalView(this, id, onClosed);
            _views.Add(id, view);
            return (view, 200);
        }
    }

    public void CloseView(TerminalView view)
    {
        lock (_gate)
            _views.Remove(view.Id);
    }

    public int RequestViewFailure(string viewId, BrowserCloseRequest failure)
    {
        lock (_gate)
            return _stopping || !_views.TryGetValue(viewId, out var view) ? 404 : view.RequestFailure(failure);
    }

    public int UpdateControls(TerminalControlsRequest request)
    {
        lock (_gate)
        {
            if (_stopping || Stopping.IsCancellationRequested)
                return 404;
            if (_demo is null)
                return 409;
            if (request.Paused is { } paused) _demo.Paused = paused;
            if (request.Rate is { } rate) _demo.Rate = rate;
            if (request.Batch is { } batch) _demo.Batch = batch;
            return 204;
        }
    }

    public Task StopAsync()
    {
        RequestStop(BrowserCloseRequest.OwnerStopped);
        return _completion;
    }

    public int StartTape(string tapeId)
    {
        var tape = _tapes.FirstOrDefault(tape => tape.Info.Id == tapeId);
        return tape is null ? 400 : _tapePlayback.Start(tape);
    }

    public int CancelTape() => _tapePlayback.Cancel();

    private void RequestStop(BrowserCloseRequest reason)
    {
        lock (_gate)
        {
            if (_stopping)
                return;
            _closeReason = reason;
            _stopping = true;
            _stop.Cancel();
        }
    }

    private async Task RunLifetimeAsync()
    {
        try
        {
            var exitCode = await _terminal.RunAsync(Stopping);
            _logger.LogInformation("Terminal {Instance} ({Scene}) exited with code {ExitCode}", Id, _scene, exitCode);
            RequestStop(new((System.Net.WebSockets.WebSocketCloseStatus)4000, $"Workload exited with code {exitCode}"));
        }
        catch (OperationCanceledException) when (Stopping.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Terminal {Instance} ({Scene}) failed", Id, _scene);
            RequestStop(BrowserCloseRequest.WorkloadFailed);
        }
        finally
        {
            RequestStop(BrowserCloseRequest.ApplicationStopping);
            await _tapePlayback.DisposeAsync();
            TerminalView[] views;
            lock (_gate)
                views = _views.Values.ToArray();
            // Let producer-backed views release their HMP peers before disposing the producer.
            await Task.WhenAll(views.Select(view => view.Completion));
            try { await _terminal.DisposeAsync(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not cleanly dispose terminal {Instance}", Id); }
            finally
            {
                await _applicationStopping.DisposeAsync();
                lock (_gate)
                    _stop.Dispose();
                _onCompleted(this);
            }
        }
    }
}
