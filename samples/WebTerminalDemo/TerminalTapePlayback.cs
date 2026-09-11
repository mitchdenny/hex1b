using Hex1b;
using Hex1b.Automation;
using Microsoft.Extensions.Logging;

namespace WebTerminalDemo;

internal sealed class TerminalTapePlayback(
    Hex1bTerminal terminal, string instanceId, ILogger logger, CancellationToken stopping) : IAsyncDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cancellation;
    private Task _completion = Task.CompletedTask;
    private TerminalTapeStatus? _status;
    private bool _cancelRequested;
    private bool _disposed;

    public TerminalTapeStatus? Status { get { lock (_gate) return _status; } }

    public int Start(DemoTape tape)
    {
        lock (_gate)
        {
            if (_disposed || stopping.IsCancellationRequested)
                return 404;
            if (_cancellation is not null)
                return 409;
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stopping);
            cancellation.CancelAfter(TimeSpan.FromMinutes(2));
            _cancellation = cancellation;
            _cancelRequested = false;
            var status = new TerminalTapeStatus(tape.Info.Id, tape.Info.Name, "running", DateTimeOffset.UtcNow, null, null, []);
            _status = status;
            _completion = Task.Run(() => PlayAsync(tape, status, cancellation));
            return 202;
        }
    }

    public int Cancel()
    {
        lock (_gate)
        {
            if (_disposed || stopping.IsCancellationRequested)
                return 404;
            if (_cancellation is null)
                return 409;
            _cancelRequested = true;
            _status = _status! with { State = "cancelling" };
            _cancellation.Cancel();
            return 202;
        }
    }

    private async Task PlayAsync(DemoTape tape, TerminalTapeStatus status, CancellationTokenSource cancellation)
    {
        try
        {
            using var result = await new TapePlayer().PlayAsync(tape.Document, terminal, cancellationToken: cancellation.Token);
            status = status with
            {
                State = "completed",
                CompletedCommands = result.CompletedCommandCount,
                Diagnostics = result.Diagnostics.Select(FormatDiagnostic).ToArray()
            };
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            lock (_gate)
                status = status with
                {
                    State = _cancelRequested || stopping.IsCancellationRequested ? "cancelled" : "failed",
                    Error = _cancelRequested || stopping.IsCancellationRequested ? null : "Tape exceeded the two-minute demo limit."
                };
        }
        catch (TapeValidationException ex)
        {
            logger.LogWarning(ex, "Tape {Tape} was rejected for terminal {Instance}", tape.Info.Id, instanceId);
            status = status with { State = "failed", Error = ex.Message, Diagnostics = ex.Diagnostics.Select(FormatDiagnostic).ToArray() };
        }
        catch (TapePlaybackException ex)
        {
            logger.LogWarning(ex, "Tape {Tape} failed on terminal {Instance}", tape.Info.Id, instanceId);
            status = status with { State = "failed", Error = ex.Message, CompletedCommands = ex.CompletedCommandCount };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected tape failure on terminal {Instance}", instanceId);
            status = status with { State = "failed", Error = "Tape playback failed; see server log." };
        }
        finally
        {
            lock (_gate)
            {
                _status = status;
                _cancellation = null;
                cancellation.Dispose();
            }
        }
    }

    private static string FormatDiagnostic(TapeDiagnostic diagnostic) =>
        $"{diagnostic.Span.SourceName}:{diagnostic.Span.Line}:{diagnostic.Span.Column}: {diagnostic.Message}";

    public async ValueTask DisposeAsync()
    {
        Task completion;
        lock (_gate)
        {
            _disposed = true;
            _cancelRequested = true;
            _cancellation?.Cancel();
            completion = _completion;
        }
        await completion;
    }
}
