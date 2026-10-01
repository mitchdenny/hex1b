using System.Diagnostics;
using System.Text.Json;

namespace Hex1b.Tests.Diagnostics;

// Observes adapter calls, not physical PTY syscalls or application input consumption.
internal sealed class DuplexPtyCapture : IHex1bTerminalWorkloadAdapter
{
    private readonly IHex1bTerminalWorkloadAdapter _inner;
    private readonly long _maxBytes;
    private readonly int _maxEvents;
    private readonly object _gate = new();
    private readonly List<DuplexCaptureRecord> _records = [];
    private readonly CancellationTokenSource _failureSignal = new();
    private long _bytes;
    private long _operationId;
    private string? _failure;
    private string _status = "incomplete";
    private bool _complete;
    private int _disposed;

    public DuplexPtyCapture(IHex1bTerminalWorkloadAdapter inner, long maximumBytes, int maximumEvents)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEvents);
        _inner = inner;
        _maxBytes = maximumBytes;
        _maxEvents = maximumEvents;
        _inner.Disconnected += OnDisconnected;
    }

    public IReadOnlyList<DuplexCaptureRecord> Records
    {
        get { lock (_gate) return Snapshot(); }
    }

    public CancellationToken FailureToken => _failureSignal.Token;
    public string? Failure { get { lock (_gate) return _failure; } }
    public string Status { get { lock (_gate) return _status; } }
    public bool IsComplete { get { lock (_gate) return _complete; } }
    public event Action? Disconnected;

    public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
    {
        var operation = BeginOperation();
        ReadOnlyMemory<byte> bytes;
        try
        {
            bytes = await _inner.ReadOutputAsync(ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            RecordOutcome("output", operation, exception);
            throw;
        }
        Record("output", operation, bytes);
        return bytes;
    }

    public async ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var operation = BeginOperation();
        Record("input-start", operation, data);
        try
        {
            await _inner.WriteInputAsync(data, ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            RecordOutcome("input", operation, exception);
            throw;
        }
        Record("input-complete", operation);
    }

    public async ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
    {
        var operation = BeginOperation();
        Record("resize-start", operation, width: width, height: height);
        try
        {
            await _inner.ResizeAsync(width, height, ct).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            RecordOutcome("resize", operation, exception);
            throw;
        }
        Record("resize-completed", operation, width: width, height: height);
    }

    public void Complete(string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        lock (_gate)
        {
            if (_failure is not null)
                return;
            _status = status;
            _complete = true;
        }
    }

    public async Task SaveAsync(string path, object metadata, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ct.ThrowIfCancellationRequested();
        object transcript;
        lock (_gate)
        {
            transcript = new
            {
                Version = 1,
                StopwatchFrequency = Stopwatch.Frequency,
                Observation = "Adapter calls; not physical syscall boundaries or application input consumption.",
                Metadata = metadata,
                IsComplete = _complete,
                Status = _status,
                Failure = _failure,
                MaxBytes = _maxBytes,
                MaxEvents = _maxEvents,
                RecordedBytes = _bytes,
                Records = Snapshot()
            };
        }

        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous
        };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var stream = new FileStream(path, options);
        await JsonSerializer.SerializeAsync(stream, transcript, cancellationToken: ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _inner.Disconnected -= OnDisconnected;
        try
        {
            await _inner.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (Failure is null)
                RecordOutcome("dispose", Interlocked.Increment(ref _operationId), exception);
            throw;
        }
        // Cleanup must remain possible after a limit failure.
        if (Failure is null)
            Record("disposed", Interlocked.Increment(ref _operationId));
    }

    private long BeginOperation()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        lock (_gate)
        {
            if (_failure is not null)
                throw new InvalidOperationException(_failure);
        }
        return Interlocked.Increment(ref _operationId);
    }

    private void OnDisconnected()
    {
        try
        {
            Record("disconnected", Interlocked.Increment(ref _operationId));
        }
        finally
        {
            Disconnected?.Invoke();
        }
    }

    private void RecordOutcome(string kind, long operation, Exception exception) =>
        Record($"{kind}-{(exception is OperationCanceledException ? "canceled" : "faulted")}",
            operation, detail: exception.GetType().FullName);

    private void Record(string kind, long operation, ReadOnlyMemory<byte>? bytes = null,
        int? width = null, int? height = null, string? detail = null)
    {
        string? failure;
        lock (_gate)
        {
            if (_failure is null &&
                (_records.Count >= _maxEvents || (bytes?.Length ?? 0) > _maxBytes - _bytes))
            {
                _failure = "Capture limit exceeded; transcript is incomplete.";
                _status = "failed";
                _complete = false;
            }

            failure = _failure;
            if (failure is null)
            {
                var copy = bytes?.ToArray();
                _records.Add(new(_records.Count + 1L, Stopwatch.GetTimestamp(), kind,
                    operation, copy, width, height, detail));
                _bytes += copy?.Length ?? 0;
            }
        }
        if (failure is not null)
        {
            // Cancellation callbacks must not run under the recording lock.
            _ = _failureSignal.CancelAsync();
            throw new InvalidOperationException(failure);
        }
    }

    private DuplexCaptureRecord[] Snapshot() =>
        _records.Select(record => record with { Bytes = record.Bytes?.ToArray() }).ToArray();
}

internal sealed record DuplexCaptureRecord(
    long Sequence, long Timestamp, string Kind, long OperationId, byte[]? Bytes,
    int? Width, int? Height, string? Detail)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public byte[] Data => Bytes ?? [];
}
