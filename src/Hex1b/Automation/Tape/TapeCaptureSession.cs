using System.Runtime.ExceptionServices;
using System.Text;
using Hex1b.Tokens;

namespace Hex1b.Automation;

internal sealed class TapeCaptureSession(Hex1bTerminal terminal, TapePreparedRun prepared)
{
    private readonly List<TapeArtifact> _created = [];
    private readonly Dictionary<string, StreamWriter> _golden = new();
    private readonly HashSet<string> _enabledGolden = new(prepared.InitiallyEnabledGoldenPaths);
    private TerminalCaptureScope? _scope;
    private AsciinemaRecorder? _recorder;
    private bool _initialized;
    private bool _visible = true;
    private int _width;
    private int _height;
    private TimeSpan? _hiddenStarted;
    private TimeSpan _hiddenDuration;
    private bool _finished;

    internal IReadOnlyList<TapeArtifact> CreatedArtifacts => _created;

    internal async Task StartAsync(CancellationToken ct)
    {
        foreach (var artifact in prepared.Artifacts)
        {
            ct.ThrowIfCancellationRequested();
            var stream = new FileStream(artifact.Path, prepared.Overwrite ? FileMode.Create : FileMode.CreateNew,
                FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous);
            _created.Add(artifact);
            if (artifact.Kind == TapeArtifactKind.Asciicast)
            {
                _recorder = new AsciinemaRecorder(stream, new AsciinemaRecorderOptions
                {
                    AutoFlush = true,
                    CaptureEnvironment = false
                });
                using var snapshot = terminal.CreateSnapshot();
                await ((IHex1bTerminalWorkloadFilter)_recorder).OnSessionStartAsync(snapshot.Width, snapshot.Height,
                    terminal.AutomationTimeProvider.GetUtcNow(), ct);
            }
            else
                _golden.Add(artifact.Path, new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n" });
        }
        if (prepared.Artifacts.Count > 0)
            _scope = await terminal.BeginCaptureAsync(ObserveAsync, ct);
    }

    private async ValueTask ObserveAsync(TerminalCaptureEvent observation, CancellationToken ct)
    {
        if (observation.Kind == TerminalCaptureEventKind.State && _hiddenStarted is { } hidden)
        {
            _hiddenDuration += observation.Elapsed - hidden;
            _hiddenStarted = null;
        }
        if (_recorder is null)
            return;
        var filter = (IHex1bTerminalWorkloadFilter)_recorder;
        var elapsed = observation.Elapsed - _hiddenDuration;
        if (!_initialized)
        {
            await filter.OnSessionStartAsync(observation.Width, observation.Height,
                terminal.AutomationTimeProvider.GetUtcNow(), ct);
            _width = observation.Width;
            _height = observation.Height;
            _initialized = true;
        }
        else if (_width != observation.Width || _height != observation.Height)
        {
            await filter.OnResizeAsync(observation.Width, observation.Height, elapsed, ct);
            _width = observation.Width;
            _height = observation.Height;
        }
        if (observation.Kind is TerminalCaptureEventKind.State or TerminalCaptureEventKind.Output)
            await filter.OnOutputAsync([new TextToken(observation.Output)], elapsed, ct);
    }

    internal async Task HideAsync(CancellationToken ct)
    {
        if (!_visible)
            return;
        if (_scope is not null)
        {
            using var boundary = await _scope.SetVisibilityAsync(false, ct);
            _hiddenStarted = boundary.Elapsed;
        }
        _visible = false;
    }

    internal async Task ShowAsync(CancellationToken ct)
    {
        if (_visible)
            return;
        if (_scope is not null)
        {
            using var boundary = await _scope.SetVisibilityAsync(true, ct);
        }
        _visible = true;
    }

    internal async Task CheckpointAsync(string? enableGoldenPath, CancellationToken ct)
    {
        if (enableGoldenPath is not null)
            _enabledGolden.Add(enableGoldenPath);
        if (_scope is null)
            return;
        using var boundary = await _scope.BarrierAsync(ct);
        if (!_visible || _enabledGolden.Count == 0)
            return;
        var snapshot = boundary.BufferStartSnapshot;
        var text = string.Join('\n', Enumerable.Range(0, snapshot.Height)
            .Select(row => TapeTextSnapshot.TrimRow(snapshot.GetLine(row)))) + "\n" + new string('\u2500', 80) + "\n";
        foreach (var path in _enabledGolden)
        {
            await _golden[path].WriteAsync(text.AsMemory(), ct);
            await _golden[path].FlushAsync(ct);
        }
    }

    internal async Task<Hex1bTerminalSnapshot> FinishAsync(CancellationToken ct)
    {
        if (_finished)
            throw new InvalidOperationException("Capture has already been finalized.");
        _finished = true;
        Hex1bTerminalSnapshot? snapshot = null;
        var errors = new List<Exception>();
        try
        {
            if (_scope is not null)
            {
                var boundary = await _scope.DetachAsync(ct);
                snapshot = boundary.Snapshot;
                boundary.BufferStartSnapshot.Dispose();
            }
            else
                snapshot = terminal.CreateSnapshot();
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }
        finally
        {
            if (_scope is not null)
                await _scope.DisposeAsync();
        }
        if (_recorder is not null)
        {
            try
            {
                await _recorder.DisposeAsync();
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }
        foreach (var writer in _golden.Values)
        {
            try
            {
                await writer.DisposeAsync();
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }
        if (errors.Count > 0)
        {
            snapshot?.Dispose();
            ExceptionDispatchInfo.Capture(errors.Count == 1 ? errors[0] : new AggregateException(errors)).Throw();
        }
        return snapshot ?? throw new InvalidOperationException("Capture produced no final snapshot.");
    }
}
